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
 
using System.Collections.Generic;

namespace SonarQube.MSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// Dictionary with strongly-typed accessors for some well-known properties
    /// </summary>
    internal class WellKnownProjectProperties : Dictionary<string, string>
    {
        #region Public properties

        public string SonarQubeExclude
        {
            get { return this.GetValueOrNull(TargetProperties.SonarQubeExcludeMetadata); }
            set { this[TargetProperties.SonarQubeExcludeMetadata] = value; }
        }

        public string SonarQubeTargetsPath
        {
            get { return this.GetValueOrNull(TargetProperties.SonarQubeTargetsPath); }
            set { this[TargetProperties.SonarQubeTargetsPath] = value; }
        }

        public string SonarQubeOutputPath
        {
            get { return this.GetValueOrNull(TargetProperties.SonarQubeOutputPath); }
            set { this[TargetProperties.SonarQubeOutputPath] = value; }
        }

        public string SonarQubeConfigPath
        {
            get { return this.GetValueOrNull(TargetProperties.SonarQubeConfigPath); }
            set { this[TargetProperties.SonarQubeConfigPath] = value; }
        }

        public string SonarQubeTempPath
        {
            get { return this.GetValueOrNull(TargetProperties.SonarQubeTempPath); }
            set { this[TargetProperties.SonarQubeTempPath] = value; }
        }

        public string RunCodeAnalysis
        {
            get { return this.GetValueOrNull(TargetProperties.RunCodeAnalysis); }
            set { this[TargetProperties.RunCodeAnalysis] = value; }
        }

        public string CodeAnalysisLogFile
        {
            get { return this.GetValueOrNull(TargetProperties.CodeAnalysisLogFile); }
            set { this[TargetProperties.CodeAnalysisLogFile] = value; }
        }

        public string CodeAnalysisRuleset
        {
            get { return this.GetValueOrNull(TargetProperties.CodeAnalysisRuleset); }
            set { this[TargetProperties.CodeAnalysisRuleset] = value; }
        }

        public string ResolvedCodeAnalysisRuleset
        {
            get { return this.GetValueOrNull(TargetProperties.ResolvedCodeAnalysisRuleset); }
            set { this[TargetProperties.ResolvedCodeAnalysisRuleset] = value; }
        }

        public string ErrorLog
        {
            get { return this.GetValueOrNull(TargetProperties.ErrorLog); }
            set { this[TargetProperties.ErrorLog] = value; }
        }

        public string WarningsAsErrors
        {
            get { return this.GetValueOrNull(TargetProperties.WarningsAsErrors); }
            set { this[TargetProperties.WarningsAsErrors] = value; }
        }

        public string TreatWarningsAsErrors
        {
            get { return this.GetValueOrNull(TargetProperties.TreatWarningsAsErrors); }
            set { this[TargetProperties.TreatWarningsAsErrors] = value; }
        }

        public string WarningLevel
        {
            get { return this.GetValueOrNull(TargetProperties.WarningLevel); }
            set { this[TargetProperties.WarningLevel] = value; }
        }

        public string AssemblyName
        {
            get { return this.GetValueOrNull(TargetProperties.AssemblyName); }
            set { this[TargetProperties.AssemblyName] = value; }
        }

        public string TeamBuildLegacyBuildDirectory
        {
            get { return this.GetValueOrNull(TargetProperties.BuildDirectory_Legacy); }
            set { this[TargetProperties.BuildDirectory_Legacy] = value; }
        }

        public string TeamBuild2105BuildDirectory
        {
            get { return this.GetValueOrNull(TargetProperties.BuildDirectory_TFS2015); }
            set { this[TargetProperties.BuildDirectory_TFS2015] = value; }
        }

        public string BuildingInsideVS
        {
            get { return this.GetValueOrNull(TargetProperties.BuildingInsideVS); }
            set { this[TargetProperties.BuildingInsideVS] = value; }
        }

        public string MSBuildExtensionsPath
        {
            get { return this.GetValueOrNull(TargetProperties.MSBuildExtensionsPath); }
            set { this[TargetProperties.MSBuildExtensionsPath] = value; }
        }

        public string SonarTestProject
        {
            get { return this.GetValueOrNull(TargetProperties.SonarQubeTestProject); }
            set { this[TargetProperties.SonarQubeTestProject] = value; }
        }

        public string ProjectTypeGuids
        {
            get { return this.GetValueOrNull(TargetProperties.ProjectTypeGuids); }
            set { this[TargetProperties.ProjectTypeGuids] = value; }
        }

        #endregion

        #region Private methods

        private string GetValueOrNull(string key)
        {
            this.TryGetValue(key, out string value);
            return value;
        }

        #endregion
    }
}
