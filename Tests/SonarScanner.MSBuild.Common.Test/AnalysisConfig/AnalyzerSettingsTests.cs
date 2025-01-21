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
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class AnalyzerSettingsTests
{
    [TestMethod]
    public void Ctor_WhenRulesetPathIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new AnalyzerSettings("language", null, "path", Enumerable.Empty<AnalyzerPlugin>(), Enumerable.Empty<string>());

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("rulesetPath");
    }

    [TestMethod]
    public void Ctor_WhenDeactivatedRulesetPathIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new AnalyzerSettings("language", "path", null, Enumerable.Empty<AnalyzerPlugin>(), Enumerable.Empty<string>());

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("deactivatedRulesetPath");
    }

    [TestMethod]
    public void Ctor_WhenRulesetPathIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new AnalyzerSettings("language", "", "path", Enumerable.Empty<AnalyzerPlugin>(), Enumerable.Empty<string>());

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("rulesetPath");
    }

    [TestMethod]
    public void Ctor_WhenRulesetPathIsWhitespaces_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new AnalyzerSettings("language", "   ", "path", Enumerable.Empty<AnalyzerPlugin>(), Enumerable.Empty<string>());

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("rulesetPath");
    }

    [TestMethod]
    public void Ctor_WhenAnalyzerAssembliesIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new AnalyzerSettings("language", "foo", "path", null, Enumerable.Empty<string>());

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("analyzerPlugins");
    }

    [TestMethod]
    public void Ctor_WhenAdditionalFilesIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new AnalyzerSettings("language", "foo", "path", Enumerable.Empty<AnalyzerPlugin>(), null);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("additionalFiles");
    }
}
