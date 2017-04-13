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
 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.PreProcessor.Roslyn;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockAnalyzerInstaller : IAnalyzerInstaller
    {
        #region Test helpers

        public ISet<string> AssemblyPathsToReturn { get; set; }

        public List<Plugin> SuppliedPlugins = new List<Plugin>();

        #endregion

        #region Checks

        public void AssertExpectedPluginsRequested(IEnumerable<string> plugins)
        {
            foreach(string plugin in plugins)
            {
                AssertExpectedPluginRequested(plugin);
            }
        }

        public void AssertExpectedPluginRequested(string key)
        {
            Assert.IsFalse(this.SuppliedPlugins == null || !this.SuppliedPlugins.Any(), "No plugins have been requested");
            bool found = this.SuppliedPlugins.Any(p => string.Equals(key, p.Key, System.StringComparison.Ordinal));
            Assert.IsTrue(found, "Expected plugin was not requested. Id: {0}", key);
        }

        #endregion

        #region IAnalyzerInstaller methods

        IEnumerable<string> IAnalyzerInstaller.InstallAssemblies(IEnumerable<Plugin> plugins)
        {
            Assert.IsNotNull(plugins, "Supplied list of plugins should not be null");
            foreach(Plugin p in plugins)
            {
                Debug.WriteLine(p.StaticResourceName);
            }
            this.SuppliedPlugins.AddRange(plugins);

            return this.AssemblyPathsToReturn;
        }

        #endregion
    }
}
