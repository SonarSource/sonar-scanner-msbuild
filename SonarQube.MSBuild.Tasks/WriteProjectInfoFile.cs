/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SonarQube.MSBuild.Tasks
{
    /// <summary>
    /// MSBuild task to write a ProjectInfo file to disk in XML format
    /// </summary>
    /// <remarks>The task does not make any assumptions about the type of project from which it is
    /// being called so it should work for projects of any type - C#, VB, UML, C++, and any new project types
    /// that are created.</remarks>
    public class WriteProjectInfoFile : Task
    {
        #region Input properties

        // TODO: we can get this from this.BuildEngine.ProjectFileOfTaskNode; we don't need the caller to supply it. Same for the full path
        [Required]
        public string ProjectName { get; set; }
        
        [Required]
        public string FullProjectPath { get; set; }

        /// <summary>
        /// Optional, in case we are imported into a project type that does not have a language specified
        /// </summary>
        public string ProjectLanguage { get; set; }

        public string ProjectGuid { get; set; }

        public bool IsTest { get; set; }

        public bool IsExcluded { get; set; }

        public ITaskItem[] AnalysisResults { get; set; }

        public ITaskItem[] AnalysisSettings { get; set; }

        public ITaskItem[] GlobalAnalysisSettings { get; set; }

        /// <summary>
        /// The folder in which the file should be written
        /// </summary>
        [Required]
        public string OutputFolder { get; set; }

        #endregion

        #region Overrides

        public override bool Execute()
        {
            ProjectInfo pi = new ProjectInfo();
            pi.ProjectType = this.IsTest ? ProjectType.Test : ProjectType.Product;
            pi.IsExcluded = this.IsExcluded;

            pi.ProjectName = this.ProjectName;
            pi.FullPath = this.FullProjectPath;
            pi.ProjectLanguage = this.ProjectLanguage;

            Guid projectId;
            if (Guid.TryParse(this.ProjectGuid, out projectId))
            {
                pi.ProjectGuid = projectId;
                pi.AnalysisResults = TryCreateAnalysisResults(this.AnalysisResults);
                pi.AnalysisSettings = TryCreateAnalysisSettings(this.AnalysisSettings);

                string outputFileName = Path.Combine(this.OutputFolder, FileConstants.ProjectInfoFileName);
                pi.Save(outputFileName);
            }
            else
            {
                this.Log.LogWarning(Resources.WPIF_MissingOrInvalidProjectGuid, this.FullProjectPath);
            }
            return true;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Attempts to convert the supplied task items into a list of <see cref="AnalysisResult"/> objects
        /// </summary>
        private List<AnalysisResult> TryCreateAnalysisResults(ITaskItem[] resultItems)
        {
            List<AnalysisResult> results = new List<AnalysisResult>();

            if (resultItems != null)
            {
                foreach (ITaskItem resultItem in resultItems)
                {
                    AnalysisResult result = TryCreateResultFromItem(resultItem);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// Attempts to create an <see cref="AnalysisResult"/> from the supplied task item.
        /// Returns null if the task item does not have the required metadata.
        /// </summary>
        private AnalysisResult TryCreateResultFromItem(ITaskItem taskItem)
        {
            Debug.Assert(taskItem != null, "Supplied task item should not be null");
            
            AnalysisResult result = null;

            string id = taskItem.GetMetadata(BuildTaskConstants.ResultMetadataIdProperty);

            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(taskItem.ItemSpec))
            {
                string path = taskItem.ItemSpec;
                if (!Path.IsPathRooted(path))
                {
                    this.Log.LogMessage(MessageImportance.Low, Resources.WPIF_ResolvingRelativePath, id, path);
                    string projectDir = Path.GetDirectoryName(this.FullProjectPath);
                    string absPath = Path.Combine(projectDir, path);
                    if (File.Exists(absPath))
                    {
                        this.Log.LogMessage(MessageImportance.Low, Resources.WPIF_ResolvedPath, absPath);
                        path = absPath;
                    }
                    else
                    {
                        this.Log.LogMessage(MessageImportance.Low, Resources.WPIF_FailedToResolvePath, taskItem.ItemSpec);
                    }
                }

                result = new AnalysisResult()
                {
                    Id = id,
                    Location = path
                };
            }
            return result;
        }

        /// <summary>
        /// Attempts to convert the supplied task items into a list of <see cref="ConfigSetting"/> objects
        /// </summary>
        private AnalysisProperties TryCreateAnalysisSettings(ITaskItem[] resultItems)
        {
            AnalysisProperties settings = new AnalysisProperties();

            if (resultItems != null)
            {
                foreach (ITaskItem resultItem in resultItems)
                {
                    Property result = TryCreateSettingFromItem(resultItem);
                    if (result != null)
                    {
                        settings.Add(result);
                    }
                }
            }
            return settings;
        }

        /// <summary>
        /// Attempts to create an <see cref="ConfigSetting"/> from the supplied task item.
        /// Returns null if the task item does not have the required metadata.
        /// </summary>
        private Property TryCreateSettingFromItem(ITaskItem taskItem)
        {
            Debug.Assert(taskItem != null, "Supplied task item should not be null");

            Property setting = null;

            string settingId;

            if (TryGetSettingId(taskItem, out settingId))
            {
                // No validation for the value: can be anything, but the
                // "Value" metadata item must exist
                string settingValue;

                if (TryGetSettingValue(taskItem, out settingValue))
                {
                    setting = new Property()
                    {
                        Id = settingId,
                        Value = settingValue
                    };
                }
            }
            return setting;
        }

        /// <summary>
        /// Attempts to extract the setting id from the supplied task item.
        /// Logs warnings if the task item does not contain valid data.
        /// </summary>
        private bool TryGetSettingId(ITaskItem taskItem, out string settingId)
        {
            settingId = null;

            string possibleKey = taskItem.ItemSpec;

            bool isValid = Property.IsValidKey(possibleKey);
            if (isValid)
            {
                settingId = possibleKey;
            }
            else
            {
                this.Log.LogWarning(Resources.WPIF_WARN_InvalidSettingKey, possibleKey);
            }
            return isValid;
        }

        /// <summary>
        /// Attempts to return the value to use for the setting.
        /// Logs warnings if the task item does not contain valid data.
        /// </summary>
        /// <remarks>The task should have a "Value" metadata item</remarks>
        private bool TryGetSettingValue(ITaskItem taskItem, out string metadataValue)
        {
            bool success;

            metadataValue  = taskItem.GetMetadata(BuildTaskConstants.SettingValueMetadataName);
            Debug.Assert(metadataValue != null, "Not expecting the metadata value to be null even if the setting is missing");

            if (metadataValue == string.Empty)
            {
                this.Log.LogWarning(Resources.WPIF_WARN_MissingValueMetadata, taskItem.ItemSpec);
                success = false;
            }
            else
            {
                success = true;
            }
            return success;
        }

        #endregion
    }
}
