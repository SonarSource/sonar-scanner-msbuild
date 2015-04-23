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

            // TODO: handle failures and missing values.
            Guid projectId;
            if (Guid.TryParse(this.ProjectGuid, out projectId))
            {
                pi.ProjectGuid = projectId;
                pi.AnalysisResults = TryCreateAnalysisResults(this.AnalysisResults);

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

        #endregion
    }
}
