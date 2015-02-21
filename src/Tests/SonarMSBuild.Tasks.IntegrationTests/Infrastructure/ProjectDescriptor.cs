//-----------------------------------------------------------------------
// <copyright file="ProjectDescriptor.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SonarMSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// Describes the expected contents of a single project
    /// </summary>
    internal class ProjectDescriptor
    {

        #region Public properties

        public string ProjectPath { get; set; }

        public Guid ProjectGuid { get; set; }

        public string ProjectName { get; set; }

        public string[] CompileInputs { get; set; }

        public bool IsTestProject { get; set; }

        public string[] GeneratedFiles { get; set; }

        #endregion

        #region Public methods

        public ProjectInfo CreateProjectInfo()
        {
            ProjectInfo info = new ProjectInfo()
            {
                FullPath = ProjectPath,
                ProjectGuid = ProjectGuid,
                ProjectName = ProjectName,
                ProjectType = this.IsTestProject ? ProjectType.Test : ProjectType.Product
            };
            return info;
        }

        #endregion

    }
}
