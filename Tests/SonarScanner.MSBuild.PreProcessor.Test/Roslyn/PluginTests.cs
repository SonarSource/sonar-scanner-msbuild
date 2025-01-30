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

namespace SonarScanner.MSBuild.PreProcessor.Test.Roslyn;

[TestClass]
public class PluginTests
{
    public void Plugin_IsValid()
    {
        var plugin = new Plugin() { Key = "pluginKey", Version = "42", StaticResourceName = "test.zip" };
        plugin.IsValid.Should().BeTrue();
    }

    [DataTestMethod]
    [DataRow("pluginKey", "42", null)]
    [DataRow("pluginKey", null, "test.zip")]
    [DataRow(null, "42", "test.zip")]
    [DataRow("pluginKey", null, null)]
    [DataRow(null, "42", null)]
    [DataRow(null, null, "test.zip")]
    [DataRow(null, null, null)]
    public void Plugin_Is_InValid(string key, string version, string resourceName)
    {
        var plugin = new Plugin() { Key = key, Version = version, StaticResourceName = resourceName };
        plugin.IsValid.Should().BeFalse();
    }

    [TestMethod]
    public void Plugin_AddProperty_Populates_Correctly()
    {
        var plugin = new Plugin();
        plugin.AddProperty("pluginKey", "someKey");
        plugin.Key.Should().Be("someKey");
        plugin.AddProperty("pluginVersion", "42.0.0");
        plugin.Version.Should().Be("42.0.0");
        plugin.Key.Should().Be("someKey");
        plugin.AddProperty("staticResourceName", "test.zip");
        plugin.StaticResourceName.Should().Be("test.zip");
    }

    [TestMethod]
    public void XmlSerialization_SaveAndReload()
    {
        var tempFileName = Path.GetTempFileName();
        var original = new Plugin() { Key = "my key", Version = "MY VERSION", StaticResourceName = "my resource" };
        SonarScanner.MSBuild.Common.Serializer.SaveModel(original, tempFileName);
        var reloaded = SonarScanner.MSBuild.Common.Serializer.LoadModel<Plugin>(tempFileName);
        reloaded.Key.Should().Be("my key");
        reloaded.Version.Should().Be("MY VERSION");
        reloaded.StaticResourceName.Should().Be("my resource");
    }
}
