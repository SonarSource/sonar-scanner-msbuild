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

using SonarScanner.MSBuild.PreProcessor.Roslyn;

namespace SonarScanner.MSBuild.PreProcessor.Test;

internal class MockAnalyzerInstaller : IAnalyzerInstaller
{
    public List<Plugin> SuppliedPlugins = [];

    public IList<AnalyzerPlugin> AnalyzerPluginsToReturn { get; set; }

    public MockAnalyzerInstaller(IList<AnalyzerPlugin> analyzerPluginsToReturn = null) =>
        AnalyzerPluginsToReturn = analyzerPluginsToReturn;

    public void AssertOnlyExpectedPluginsRequested(IEnumerable<Plugin> plugins)
    {
        SuppliedPlugins.Should().HaveSameCount(plugins);
        foreach (var plugin in plugins)
        {
            AssertExpectedPluginRequested(plugin);
        }
    }

    public void AssertExpectedPluginRequested(Plugin plugin)
    {
        SuppliedPlugins.Should().NotBeEmpty("No plugins have been requested");
        var suppliedPlugin = SuppliedPlugins.SingleOrDefault(x => x.Key == plugin.Key);
        suppliedPlugin.Should().NotBeNull("Expected plugin was not requested. Id: {0}", plugin.Key);
        suppliedPlugin.Version.Should().Be(plugin.Version);
        suppliedPlugin.StaticResourceName.Should().Be(plugin.StaticResourceName);
    }

    IEnumerable<AnalyzerPlugin> IAnalyzerInstaller.InstallAssemblies(IEnumerable<Plugin> plugins)
    {
        plugins.Should().NotBeNull("Supplied list of plugins should not be null");
        foreach (var plugin in plugins)
        {
            Debug.WriteLine(plugin.StaticResourceName);
        }
        SuppliedPlugins.AddRange(plugins);
        return AnalyzerPluginsToReturn;
    }
}
