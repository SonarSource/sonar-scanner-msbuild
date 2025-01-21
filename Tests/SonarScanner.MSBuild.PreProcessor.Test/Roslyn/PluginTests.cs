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

using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.PreProcessor.Roslyn;

namespace SonarScanner.MSBuild.PreProcessor.Test.Roslyn;

[TestClass]
public class PluginTests
{
    [TestMethod]
    public void Ctor_WhenKeyIsInvalid_Throws()
    {
        // 1. Null
        Action act = () => new Plugin(null, "v1", "resourceName");
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");

        // 2. Empty
        act = () => new Plugin(string.Empty, "v1", "resourceName");
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");

        // 3. Whitespace
        act = () => new Plugin("\r ", "v1", "resourceName");
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void Ctor_WhenVersionIsInvalid_Throws()
    {
        // 1. Null
        Action act = () => new Plugin("key", null, "resourceName");
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("version");

        // 2. Empty
        act = () => new Plugin("key", string.Empty, "resourceName");
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("version");

        // 3. Whitespace
        act = () => new Plugin("key", "\r\n ", "resourceName");
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("version");
    }

    [TestMethod]
    public void Ctor_WhenStaticResourceNameIsNull_Throws()
    {
        // 1. Null
        Action act = () => new Plugin("key", "version", null);
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("staticResourceName");

        // 2. Empty
        act = () => new Plugin("key", "version", string.Empty);
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("staticResourceName");

        // 3. Whitespace
        act = () => new Plugin("key", "version", "\r\n ");
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("staticResourceName");
    }

    [TestMethod]
    public void XmlSerialization_SaveAndReload()
    {
        // Arrange
        var tempFileName = Path.GetTempFileName();
        var original = new Plugin("my key", "MY VERSION", "my resource");

        // Act - save and reload
        SonarScanner.MSBuild.Common.Serializer.SaveModel<Plugin>(original, tempFileName);
        var reloaded = SonarScanner.MSBuild.Common.Serializer.LoadModel<Plugin>(tempFileName);

        // Assert
        reloaded.Key.Should().Be("my key");
        reloaded.Version.Should().Be("MY VERSION");
        reloaded.StaticResourceName.Should().Be("my resource");
    }
}
