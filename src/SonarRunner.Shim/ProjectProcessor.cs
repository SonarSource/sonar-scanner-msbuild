//-----------------------------------------------------------------------
// <copyright file="ProjectProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SonarRunner.Shim
{
    internal class ProjectClassifier
    {
        #region Public methods

        public IDictionary<ProjectInfo, ProcessingStatus> Process(IEnumerable<ProjectInfo> projects, ILogger logger)
        {
            IDictionary<ProjectInfo, ProcessingStatus> classifiedProjects = new Dictionary<ProjectInfo, ProcessingStatus>();

            // 2 product project(s), 1 test project(s), 3 excluded project(s), 0 invalid project(s), 1 project(s) with no files to analyze
            foreach(ProjectInfo projectInfo in projects)
            {
                ProcessingStatus status = ClassifyProject(projectInfo, projects);
                classifiedProjects.Add(projectInfo, status);
            }

            return classifiedProjects;
        }

        #endregion

        #region Private methods

        private static ProcessingStatus ClassifyProject(ProjectInfo projectInfo, IEnumerable<ProjectInfo> projects)
        {
            if (projectInfo.IsExcluded)
            {
                return ProcessingStatus.ExcludeFlagSet;
            }

            if (!IsProjectGuidValue(projectInfo))
            {
                return ProcessingStatus.InvalidGuid;
            }

            if (HasDuplicateGuid(projectInfo, projects))
            {
                return ProcessingStatus.DuplicateGuid;
            }

            if (!projectInfo.GetFilesToAnalyze().Any())
            {
                return ProcessingStatus.NoFilesToAnalyze;
            }

            return ProcessingStatus.Valid;
        }

        private static bool IsProjectGuidValue(ProjectInfo project)
        {
            return project.ProjectGuid != Guid.Empty;
        }

        private static bool HasDuplicateGuid(ProjectInfo projectInfo, IEnumerable<ProjectInfo> projects)
        {
            return projects.Count(p => p.ProjectGuid == projectInfo.ProjectGuid) > 1;
        }

        #endregion

    }
}
