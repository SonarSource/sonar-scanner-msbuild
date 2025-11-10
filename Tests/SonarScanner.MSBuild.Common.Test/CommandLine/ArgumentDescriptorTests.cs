/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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
public class ArgumentDescriptorTests
{
    [TestMethod]
    public void Ctor_WhenIdIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ArgumentDescriptor(null, new[] { "" }, true, "description", true);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("id");
    }

    [TestMethod]
    public void Ctor_WhenIdIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ArgumentDescriptor("", new[] { "" }, true, "description", true);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("id");
    }

    [TestMethod]
    public void Ctor_WhenIdIsWhitespaces_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ArgumentDescriptor("   ", new[] { "" }, true, "description", true);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("id");
    }

    [TestMethod]
    public void Ctor_WhenPrefixesIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ArgumentDescriptor("id", null, true, "description", true);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("prefixes");
    }

    [TestMethod]
    public void Ctor_WhenPrefixesIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ArgumentDescriptor("id", new string[0], true, "description", true);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("prefixes");
    }

    [TestMethod]
    public void Ctor_WhenDescriptionIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ArgumentDescriptor("id", new[] { "" }, true, null, true);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("description");
    }

    [TestMethod]
    public void Ctor_WhenDescriptionIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ArgumentDescriptor("id", new[] { "" }, true, "", true);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("description");
    }

    [TestMethod]
    public void Ctor_WhenDescriptionIsWhitespaces_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ArgumentDescriptor("id", new[] { "" }, true, "   ", true);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("description");
    }
}
