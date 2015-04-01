//-----------------------------------------------------------------------
// <copyright file="WellKnownProjectProperties.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;

namespace SonarQube.MSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// Dictionary with strongly-typed accessors for some well-known properties
    /// </summary>
    internal class WellKnownProjectProperties : Dictionary<string, string>
    {
        #region Public properties

        public string RunSonarQubeAnalysis
        {
            get { return this[TargetProperties.RunSonarQubeAnalysis]; }
            set { this[TargetProperties.RunSonarQubeAnalysis] = value; }
        }

        public string SonarQubeExclude
        {
            get { return this[TargetProperties.SonarQubeExcludeMetadata]; }
            set { this[TargetProperties.SonarQubeExcludeMetadata] = value; }
        }

        public string SonarQubeTargetsPath
        {
            get { return this[TargetProperties.SonarQubeTargetsPath]; }
            set { this[TargetProperties.SonarQubeTargetsPath] = value; }
        }

        public string SonarQubeOutputPath
        {
            get { return this[TargetProperties.SonarQubeOutputPath]; }
            set { this[TargetProperties.SonarQubeOutputPath] = value; }
        }

        public string SonarQubeConfigPath
        {
            get { return this[TargetProperties.SonarQubeConfigPath]; }
            set { this[TargetProperties.SonarQubeConfigPath] = value; }
        }

        public string SonarQubeTempPath
        {
            get { return this[TargetProperties.SonarQubeTempPath]; }
            set { this[TargetProperties.SonarQubeTempPath] = value; }
        }

        public string RunCodeAnalysis
        {
            get { return this[TargetProperties.RunCodeAnalysis]; }
            set { this[TargetProperties.RunCodeAnalysis] = value; }
        }

        public string CodeAnalysisLogFile
        {
            get { return this[TargetProperties.CodeAnalysisLogFile]; }
            set { this[TargetProperties.CodeAnalysisLogFile] = value; }
        }

        public string CodeAnalysisRuleset
        {
            get { return this[TargetProperties.CodeAnalysisRuleset]; }
            set { this[TargetProperties.CodeAnalysisRuleset] = value; }
        }

        public string TeamBuildBuildDirectory
        {
            get { return this[TargetProperties.TeamBuildBuildDirectory]; }
            set { this[TargetProperties.TeamBuildBuildDirectory] = value; }
        }

        public string MSBuildExtensionsPath
        {
            get { return this[TargetProperties.MSBuildExtensionsPath]; }
            set { this[TargetProperties.MSBuildExtensionsPath] = value; }
        }

        public string SonarTestProject
        {
            get { return this[TargetProperties.SonarQubeTestProject]; }
            set { this[TargetProperties.SonarQubeTestProject] = value; }
        }

        public string TestProjectNameRegex
        {
            get { return this[TargetProperties.SonarQubeTestProjectNameRegex]; }
            set { this[TargetProperties.SonarQubeTestProjectNameRegex] = value; }
        }

        public string ProjectTypeGuids
        {
            get { return this[TargetProperties.ProjectTypeGuid]; }
            set { this[TargetProperties.ProjectTypeGuid] = value; }
        }

        #endregion
    }
}
