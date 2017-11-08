/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
 
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SonarQube.Common;
using SonarQube.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text;

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
        #region Fields

        private readonly IEncodingProvider encodingProvider;

        #endregion // Fields

        #region Constructors

        public WriteProjectInfoFile()
            : this(new EncodingProvider())
        {
        }

        public WriteProjectInfoFile(IEncodingProvider encodingProvider)
        {
            this.encodingProvider = encodingProvider ?? throw new ArgumentNullException(nameof(encodingProvider));
        }

        #endregion // Constructors

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

        public string SolutionConfigurationContents { get; set; }

        public bool IsTest { get; set; }

        public bool IsExcluded { get; set; }

        public string CodePage { get; set; }

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
            ProjectInfo pi = new ProjectInfo
            {
                ProjectType = this.IsTest ? ProjectType.Test : ProjectType.Product,
                IsExcluded = this.IsExcluded,

                ProjectName = this.ProjectName,
                FullPath = this.FullProjectPath,
                ProjectLanguage = this.ProjectLanguage,
                Encoding = ComputeEncoding(this.CodePage)?.WebName
            };

            string guid = null;
            if (!string.IsNullOrEmpty(this.ProjectGuid))
            {
                guid = this.ProjectGuid;
            }
            else if (!string.IsNullOrEmpty(this.SolutionConfigurationContents))
            {
                // Try to get GUID from the Solution
                guid = XDocument.Parse(this.SolutionConfigurationContents)
                        .Descendants("ProjectConfiguration")
                        .Where(element => element.Attribute("AbsolutePath")?.Value == this.FullProjectPath)
                        .Select(element => element.Attribute("Project")?.Value)
                        .FirstOrDefault();
            }
            if (guid != null && Guid.TryParse(guid, out Guid projectId))
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

        private Encoding ComputeEncoding(string codePage)
        {
            string cleanedCodePage = (codePage ?? string.Empty)
                .Replace("\\", string.Empty)
                .Replace("\"", string.Empty);

            // Try to return the CodePage specified into the .xxproj
            if (!string.IsNullOrWhiteSpace(cleanedCodePage) &&
                long.TryParse(cleanedCodePage, NumberStyles.None, CultureInfo.InvariantCulture, out long codepageValue) &&
                codepageValue > 0)
            {
                try
                {
                    return encodingProvider.GetEncoding((int)codepageValue);
                }
                catch (Exception)
                {
                    // encoding doesn't exist
                }
            }
            return null;
        }

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


            if (TryGetSettingId(taskItem, out string settingId))
            {
                // No validation for the value: can be anything, but the
                // "Value" metadata item must exist

                if (TryGetSettingValue(taskItem, out string settingValue))
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

            metadataValue = taskItem.GetMetadata(BuildTaskConstants.SettingValueMetadataName);
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
