/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn;

namespace SonarScanner.MSBuild.PreProcessor.Test;

internal class MockAnalyzerInstaller : IAnalyzerInstaller
{
    #region Test helpers

    public IList<AnalyzerPlugin> AnalyzerPluginsToReturn { get; set; }

    public List<Plugin> SuppliedPlugins = new List<Plugin>();

    #endregion Test helpers

    #region Checks

    public void AssertExpectedPluginsRequested(IEnumerable<string> plugins)
    {
        foreach(var plugin in plugins)
        {
            AssertExpectedPluginRequested(plugin);
        }
    }

    public void AssertExpectedPluginRequested(string key)
    {
        this.SuppliedPlugins.Should().NotBeEmpty("No plugins have been requested");

        var found = this.SuppliedPlugins.Any(p => string.Equals(key, p.Key, System.StringComparison.Ordinal));
        found.Should().BeTrue("Expected plugin was not requested. Id: {0}", key);
    }

    #endregion Checks

    #region IAnalyzerInstaller methods

    IEnumerable<AnalyzerPlugin> IAnalyzerInstaller.InstallAssemblies(IEnumerable<Plugin> plugins)
    {
        plugins.Should().NotBeNull("Supplied list of plugins should not be null");
        foreach(var p in plugins)
        {
            Debug.WriteLine(p.StaticResourceName);
        }
        this.SuppliedPlugins.AddRange(plugins);

        return AnalyzerPluginsToReturn;
    }

    #endregion IAnalyzerInstaller methods
}
