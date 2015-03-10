//-----------------------------------------------------------------------
// <copyright file="WellKnownProjectProperties.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;

namespace SonarMSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// Dictionary with strongly-typed accessors for some well-known properties
    /// </summary>
    internal class WellKnownProjectProperties : Dictionary<string, string>
    {

        #region Public properties

        public string RunSonarAnalysis
        {
            get { return this[TargetProperties.RunSonarAnalysis]; }
            set { this[TargetProperties.RunSonarAnalysis] = value; }
        }

        public string SonarBinPath
        {
            get { return this[TargetProperties.SonarBinPath]; }
            set { this[TargetProperties.SonarBinPath] = value; }
        }

        public string SonarOutputPath
        {
            get { return this[TargetProperties.SonarOutputPath]; }
            set { this[TargetProperties.SonarOutputPath] = value; }
        }

        public string SonarConfigPath
        {
            get { return this[TargetProperties.SonarConfigPath]; }
            set { this[TargetProperties.SonarConfigPath] = value; }
        }

        public string SonarTempPath
        {
            get { return this[TargetProperties.SonarTempPath]; }
            set { this[TargetProperties.SonarTempPath] = value; }
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

        #endregion
    }
}
