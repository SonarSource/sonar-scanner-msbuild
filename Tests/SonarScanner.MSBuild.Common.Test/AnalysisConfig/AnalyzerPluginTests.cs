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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Common.Test;


[TestClass]
public class AnalyzerPluginTests
{

    [TestMethod]
    public void Ctor_InvalidArgs()
    {
        Action action = () => new AnalyzerPlugin(null, "version", "resource", new string[] { "asm.dll" });
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");

        action = () => new AnalyzerPlugin("key", null, "resource", new string[] { "asm.dll" });
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("version");

        action = () => new AnalyzerPlugin("key", "version", null, new string[] { "asm.dll" });
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("staticResourceName");

        action = () => new AnalyzerPlugin("key", "version", "resource", null);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("assemblies");
    }
}
