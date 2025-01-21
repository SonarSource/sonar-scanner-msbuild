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
public class PropertyTests
{
    [TestMethod]
    public void TryGetProperty_WhenKeyIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Property.TryGetProperty(null, Enumerable.Empty<Property>(), out var property);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void TryGetProperty_WhenKeyIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Property.TryGetProperty("", Enumerable.Empty<Property>(), out var property);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void TryGetProperty_WhenKeyIsWhitespaces_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Property.TryGetProperty("   ", Enumerable.Empty<Property>(), out var property);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void TryGetProperty_WhenPropertiesIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => Property.TryGetProperty("foo", null, out var property);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("properties");
    }
}
