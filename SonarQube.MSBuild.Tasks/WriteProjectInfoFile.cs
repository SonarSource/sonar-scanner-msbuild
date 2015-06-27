//-----------------------------------------------------------------------
// <copyright file="WriteProjectInfoFile.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SonarQube.MSBuild.Tasks
{
    /// <summary>
    /// MSBuild task to write a ProjectInfo file to disk in XML format
    /// </summary>
    public class WriteProjectInfoFile : Task
    {
        #region Input properties

        // TODO: we can get this from this.BuildEngine.ProjectFileOfTaskNode; we don't need the caller to supply it. Same for the full path
        [Required]
        public string ProjectName { get; set; }
        
        [Required]
        public string FullProjectPath { get; set; }

        public string ProjectGuid { get; set; }

        [Required]
        public bool IsTest { get; set; }

        [Required]
        public bool IsExcluded { get; set; }

        [Required]
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
        /// Attempts to convert the supplied task items into a list of <see cref="AnalysisSetting"/> objects
        /// </summary>
        private List<AnalysisSetting> TryCreateAnalysisSettings(ITaskItem[] resultItems)
        {
            List<AnalysisSetting> settings = new List<AnalysisSetting>();

            if (resultItems != null)
            {
                foreach (ITaskItem resultItem in resultItems)
                {
                    AnalysisSetting result = TryCreateSettingFromItem(resultItem);
                    if (result != null)
                    {
                        settings.Add(result);
                    }
                }
            }
            return settings;
        }

        /// <summary>
        /// Attempts to create an <see cref="AnalysisSetting"/> from the supplied task item.
        /// Returns null if the task item does not have the required metadata.
        /// </summary>
        private AnalysisSetting TryCreateSettingFromItem(ITaskItem taskItem)
        {
            Debug.Assert(taskItem != null, "Supplied task item should not be null");

            AnalysisSetting setting = null;

            string settingId;

            if (TryGetSettingId(taskItem, out settingId))
            {
                // No validation for the value: can be anything, but the
                // "Value" metadata item must exist
                string settingValue;

                if (TryGetSettingValue(taskItem, out settingValue))
                {
                    setting = new AnalysisSetting()
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

            bool isValid = AnalysisSetting.IsValidKey(possibleKey);
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
