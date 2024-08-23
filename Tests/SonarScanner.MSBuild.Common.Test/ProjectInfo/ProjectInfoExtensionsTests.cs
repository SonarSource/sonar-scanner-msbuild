/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
public class ProjectInfoExtensionsTests
{
    [TestMethod]
    public void TryGetAnalysisSetting_WhenProjectInfoIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.TryGetAnalyzerResult(null, "foo", out var result);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectInfo");
    }

    [TestMethod]
    public void TryGetAnalyzerResult_WhenProjectInfoIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.TryGetAnalysisSetting(null, "foo", out var result);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectInfo");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenProjectInfoIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(null, "foo", "bar");

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectInfo");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenIdIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(new ProjectInfo(), null, "bar");

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("id");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenIdIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(new ProjectInfo(), "", "bar");

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("id");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenIdIsWhitespaces_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(new ProjectInfo(), "   ", "bar");

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("id");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenLocationIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(new ProjectInfo(), "foo", null);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("location");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenLocationIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(new ProjectInfo(), "foo", "");

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("location");
    }

    [TestMethod]
    public void AddAnalyzerResult_WhenLocationIsWhitespaces_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.AddAnalyzerResult(new ProjectInfo(), "foo", "   ");

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("location");
    }

    [TestMethod]
    public void GetDirectory_WhenProjectInfoIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.GetDirectory(null);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectInfo");
    }

    [TestMethod]
    public void GetProjectGuidAsString_WhenProjectInfoIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ProjectInfoExtensions.GetProjectGuidAsString(null);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("projectInfo");
    }
}
