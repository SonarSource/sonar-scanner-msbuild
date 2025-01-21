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
public class ArgumentInstanceTests
{
    [TestMethod]
    public void Ctor_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ArgumentInstance(null, "foo");

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("descriptor");
    }

    [TestMethod]
    public void TryGetArgument_WhenIdIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ArgumentInstance.TryGetArgument(null, Enumerable.Empty<ArgumentInstance>(), out var instance);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("id");
    }

    [TestMethod]
    public void TryGetArgument_WhenIdIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ArgumentInstance.TryGetArgument("", Enumerable.Empty<ArgumentInstance>(), out var instance);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("id");
    }

    [TestMethod]
    public void TryGetArgument_WhenIdIsWhitespaces_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ArgumentInstance.TryGetArgument("   ", Enumerable.Empty<ArgumentInstance>(), out var instance);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("id");
    }

    [TestMethod]
    public void TryGetArgument_WhenArgumentsIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => ArgumentInstance.TryGetArgument("foo", null, out var instance);

        // Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("arguments");
    }
}
