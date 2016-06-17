//-----------------------------------------------------------------------
// <copyright file="MockAnalyzerInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

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
