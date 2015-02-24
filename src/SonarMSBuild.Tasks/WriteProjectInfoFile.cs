//-----------------------------------------------------------------------
// <copyright file="WriteProjectInfoFile.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Sonar.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SonarMSBuild.Tasks
{
    /// <summary>
    /// MSBuild task to write a ProjectInfo file to disk in XML format
    /// </summary>
    public class WriteProjectInfoFile : Task
    {
        #region Input properties

        [Required]
        public string ProjectName { get; set; }

        [Required]
        public string FullProjectPath { get; set; }

        [Required]
        public string ProjectGuid { get; set; }

        [Required]
        public bool IsTest { get; set; }

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

            pi.ProjectName = this.ProjectName;
            pi.FullPath = this.FullProjectPath;

            // TODO: handle failures and missing values.
            Guid projectId = Guid.Parse(this.ProjectGuid);
            pi.ProjectGuid = projectId;

            pi.AnalysisResults = TryCreateAnalysisResults(this.AnalysisResults);

            string outputFileName = Path.Combine(this.OutputFolder, BuildTaskConstants.ProjectInfoFileName);
            pi.Save(outputFileName);

            return true;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Attempts to convert the supplied task items into a list of <see cref="AnalysisResult"/> objects
        /// </summary>
        private static List<AnalysisResult> TryCreateAnalysisResults(ITaskItem[] resultItems)
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
        private static AnalysisResult TryCreateResultFromItem(ITaskItem taskItem)
        {
            Debug.Assert(taskItem != null, "Supplied task item should not be null");
            Debug.Assert(taskItem.ItemSpec.Equals(BuildTaskConstants.ResultItemName),
                string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    "Supplied task item does not have the expected name. Expected: {0}, Actual: {1}",
                    BuildTaskConstants.ResultItemName, taskItem.ItemSpec));

            AnalysisResult result = null;

            string id = taskItem.GetMetadata(BuildTaskConstants.ResultMetadataIdProperty);
            string location = taskItem.GetMetadata(BuildTaskConstants.ResultMetadataLocationProperty);

            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(location))
            {
                result = new AnalysisResult()
                {
                    Id = id,
                    Location = location
                };
            }
            return result;
        }

        #endregion
    }
}
