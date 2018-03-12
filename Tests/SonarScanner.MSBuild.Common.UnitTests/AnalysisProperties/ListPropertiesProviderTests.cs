/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
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

namespace SonarScanner.MSBuild.Common.UnitTests
{
    [TestClass]
    public class ListPropertiesProviderTests
    {
        [TestMethod]
        public void Ctor_WhenPropertiesIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => new ListPropertiesProvider(null);

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("properties");
        }

        [TestMethod]
        public void AddProperty_WhenKeyIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => new ListPropertiesProvider().AddProperty(null, "foo");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
        }

        [TestMethod]
        public void AddProperty_WhenKeyIsEmpty_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => new ListPropertiesProvider().AddProperty("", "foo");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
        }

        [TestMethod]
        public void AddProperty_WhenKeyIsWhitespaces_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => new ListPropertiesProvider().AddProperty("   ", "foo");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
        }

        [TestMethod]
        public void AddProperty_WhenKeyAlreadyExist_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var testSubject = new ListPropertiesProvider();
            testSubject.AddProperty("foo", "foo");

            Action action = () => testSubject.AddProperty("foo", "foo");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentOutOfRangeException>().And.ParamName.Should().Be("key");
        }

        [TestMethod]
        public void TryGetProperty_WhenKeyIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => new ListPropertiesProvider().TryGetProperty(null, out var property);

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
        }

        [TestMethod]
        public void TryGetProperty_WhenKeyIsEmpty_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => new ListPropertiesProvider().TryGetProperty("", out var property);

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
        }

        [TestMethod]
        public void TryGetProperty_WhenKeyIsWhitespaces_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => new ListPropertiesProvider().TryGetProperty("   ", out var property);

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
        }
    }
}
