//-----------------------------------------------------------------------
// <copyright file="WriteProjectInfoFile.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
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

            string outputFileName = Path.Combine(this.OutputFolder, FileConstants.ProjectInfoFileName);
            Serializer.SaveModel(pi, outputFileName);

            return true;
        }

        #endregion
    }
}
