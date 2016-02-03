//-----------------------------------------------------------------------
// <copyright file="MockTargetsInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.PreProcessor.Roslyn;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockRoslynAnalyzerProvider : IAnalyzerProvider
    {

        #region Test helpers

        public AnalyzerSettings SettingsToReturn { get; set; }

        #endregion

        #region IAnalyzerProvider methods

        AnalyzerSettings IAnalyzerProvider.SetupAnalyzers(ISonarQubeServer server, TeamBuildSettings settings, string projectKey, string projectBranch)
        {
            Assert.IsNotNull(server);
            Assert.IsNotNull(settings);
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectKey));
            // projectBranch can be null

            return this.SettingsToReturn;
        }

        #endregion
    }
}
