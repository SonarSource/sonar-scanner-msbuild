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
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockRoslynAnalyzerProvider : IAnalyzerProvider
    {

        #region Test helpers

        public AnalyzerSettings SettingsToReturn { get; set; }

        #endregion

        #region IAnalyzerProvider methods

        AnalyzerSettings IAnalyzerProvider.SetupAnalyzer(TeamBuildSettings settings, IDictionary<string, string> serverSettings, 
            IEnumerable<ActiveRule> activeRules, IEnumerable<string> inactiveRules, string pluginKey)
        {
            Assert.IsNotNull(settings);
            Assert.IsNotNull(serverSettings);
            Assert.IsFalse(string.IsNullOrWhiteSpace(pluginKey));

            return SettingsToReturn;
        }

        #endregion
    }
}
