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

using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.PreProcessor.Roslyn;

namespace SonarScanner.MSBuild.PreProcessor.Test.Roslyn;

[TestClass]
public class PluginTests
{
    [DataTestMethod]
    [DataRow("pluginKey", "42", "test.zip", true)]
    [DataRow("pluginKey", "42", null, false)]
    [DataRow("pluginKey", null, "test.zip", false)]
    [DataRow(null, "42", "test.zip", false)]
    [DataRow("pluginKey", null, null, false)]
    [DataRow(null, "42", null, false)]
    [DataRow(null, null, "test.zip", false)]
    [DataRow(null, null, null, false)]
    public void Plugin_IsValid(string key, string version, string resourceName, bool isValid)
    {
        var plugin = new Plugin() { Key = key, Version = version, StaticResourceName = resourceName };
        plugin.IsValid.Should().Be(isValid);
    }

    [DataTestMethod]
    [DataRow("pluginKey", "someValue")]
    [DataRow("pluginVersion", "42.0.0")]
    [DataRow("staticResourceName", "test.zip")]
    [DataRow("someOtherProperty", "someOtherValue")]
    public void AddProperty_Populates_Correctly(string property, string value)
    {
        var plugin = new Plugin();
        plugin.AddProperty(property, value);
        switch (property)
        {
            case "pluginKey":
                plugin.Key.Should().Be(value);
                plugin.Version.Should().BeNull();
                plugin.StaticResourceName.Should().BeNull();
                break;
            case "pluginVersion":
                plugin.Key.Should().BeNull();
                plugin.Version.Should().Be(value);
                plugin.StaticResourceName.Should().BeNull();
                break;
            case "staticResourceName":
                plugin.Key.Should().BeNull();
                plugin.Version.Should().BeNull();
                plugin.StaticResourceName.Should().Be(value);
                break;
            default:
                plugin.Key.Should().BeNull();
                plugin.Version.Should().BeNull();
                plugin.StaticResourceName.Should().BeNull();
                break;
        }
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
