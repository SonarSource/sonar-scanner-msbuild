/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.TFS;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.Tests
{
    internal class MockRoslynAnalyzerProvider : IAnalyzerProvider
    {
        #region Test helpers

        public AnalyzerSettings SettingsToReturn { get; set; }

        #endregion Test helpers

        #region IAnalyzerProvider methods

        AnalyzerSettings IAnalyzerProvider.SetupAnalyzer(TeamBuildSettings settings, IDictionary<string, string> serverSettings,
            IEnumerable<ActiveRule> activeRules, IEnumerable<string> inactiveRules, string pluginKey)
        {
            Assert.IsNotNull(settings);
            Assert.IsNotNull(serverSettings);
            Assert.IsFalse(string.IsNullOrWhiteSpace(pluginKey));

            return SettingsToReturn;
        }

        #endregion IAnalyzerProvider methods
    }
}
