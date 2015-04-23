//-----------------------------------------------------------------------
// <copyright file="ProjectInfoAnalysisResult.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System.Collections.Generic;
using System.Linq;

namespace SonarRunner.Shim
{
    public class ProjectInfoAnalysisResult
    {
        #region Constructor(s)
        
        public ProjectInfoAnalysisResult()
        {
            this.Projects = new Dictionary<ProjectInfo, ProjectInfoValidity>();
        }

        #endregion

        #region Public properties

        public IDictionary<ProjectInfo, ProjectInfoValidity> Projects { get; private set; }

        public bool RanToCompletion { get; set; }

        public string FullPropertiesFilePath { get; set; }

        #endregion

        #region Public methods

        public IEnumerable<ProjectInfo> GetProjectsByStatus(ProjectInfoValidity status)
        {
            return this.Projects.Where(p => p.Value == status).Select(p => p.Key).ToArray();
        }

        #endregion

    }
}
