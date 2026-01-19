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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class AnalysisPropertyProviderExtensionsTests
{
    [TestMethod]
    public void TryGetValue_ProviderIsNull_Throws()
    {
        // Arrange
        Action action = () => AnalysisPropertyProviderExtensions.TryGetValue(null, "name", out var _);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("provider");
    }

    [TestMethod]
    public void TryGetValue_NameIsNull_Throws()
    {
        // Arrange
        Action action = () => AnalysisPropertyProviderExtensions.TryGetValue(new ListPropertiesProvider(), null, out var _);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("name");
    }

    [TestMethod]
    public void TryGetValue_NameIsEmpty_Throws()
    {
        // Arrange
        Action action = () => AnalysisPropertyProviderExtensions.TryGetValue(new ListPropertiesProvider(), string.Empty, out var _);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("name");
    }

    [TestMethod]
    public void TryGetValue_PropertyExists_ReturnsTrueAndValue()
    {
        // Arrange
        var properties = new ListPropertiesProvider();
        properties.AddProperty("prop1", "value1");

        // Act
        var exists = AnalysisPropertyProviderExtensions.TryGetValue(properties, "prop1", out var actual);

        // Assert
        exists.Should().BeTrue();
        actual.Should().Be("value1");
    }

    [TestMethod]
    public void TryGetValue_PropertyDoesNotExist_ReturnsFalseAndNull()
    {
        // Arrange
        var properties = new ListPropertiesProvider();
        properties.AddProperty("prop1", "value1");

        // Act - should be case-sensitive
        var exists = AnalysisPropertyProviderExtensions.TryGetValue(properties, "PROP1", out var actual);

        // Assert
        exists.Should().BeFalse();
        actual.Should().BeNull();
    }

    [TestMethod]
    public void HasProperty_ProviderIsNull_Throws()
    {
        // Arrange
        Action action = () => AnalysisPropertyProviderExtensions.HasProperty(null, "key");

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("provider");
    }

    [TestMethod]
    public void HasProperty_KeyIsNull_Throws()
    {
        // Arrange
        Action action = () => AnalysisPropertyProviderExtensions.HasProperty(new ListPropertiesProvider(), null);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void HasProperty_KeyIsEmpty_Throws()
    {
        // Arrange
        Action action = () => AnalysisPropertyProviderExtensions.HasProperty(new ListPropertiesProvider(), string.Empty);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void HasProperty_PropertyExists_ReturnsTrue()
    {
        // Arrange
        var properties = new ListPropertiesProvider();
        properties.AddProperty("prop1", "value1");

        // Act & Assert
        AnalysisPropertyProviderExtensions.HasProperty(properties, "prop1").Should().BeTrue();
    }

    [TestMethod]
    public void HasProperty_PropertyDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var properties = new ListPropertiesProvider();
        properties.AddProperty("prop1", "value1");

        // Act & Assert - comparison should be case-sensitive
        AnalysisPropertyProviderExtensions.HasProperty(properties, "PROP1").Should().BeFalse();
    }
}
