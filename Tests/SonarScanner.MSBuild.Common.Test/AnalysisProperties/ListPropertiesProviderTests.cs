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
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class ListPropertiesProviderTests
{
    [TestMethod]
    public void Ctor_WhenPropertiesIsNull_ThrowsArgumentNullException()
    {
        // 1. IEnumerable<Property> constructor
        Action action = () => new ListPropertiesProvider((IEnumerable<Property>)null);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("properties");

        // 2. Dictionary constructor
        action = () => new ListPropertiesProvider((IDictionary<string, string>)null);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("keyValuePairs");
    }

    [TestMethod]
    public void Ctor_InitializeFromProperties()
    {
        // Arrange
        var properties = new List<Property>
        {
            new("id1", "value1"),
            new("id2", "value2")
        };

        // Act
        var listPropertiesProvider = new ListPropertiesProvider(properties);

        // Assert
        listPropertiesProvider.GetAllProperties().Count().Should().Be(2);

        CheckPropertyExists(listPropertiesProvider, "id1", "value1");
        CheckPropertyExists(listPropertiesProvider, "id2", "value2");
    }

    [TestMethod]
    public void Ctor_InitializeFromDictionary()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            { "id1", "value1" },
            { "id2", "value2" }
        };

        // Act
        var listPropertiesProvider = new ListPropertiesProvider(dict);

        // Assert
        listPropertiesProvider.GetAllProperties().Count().Should().Be(2);

        CheckPropertyExists(listPropertiesProvider, "id1", "value1");
        CheckPropertyExists(listPropertiesProvider, "id2", "value2");
    }

    [TestMethod]
    public void AddProperty_WhenKeyIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ListPropertiesProvider().AddProperty(null, "foo");

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void AddProperty_WhenKeyIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ListPropertiesProvider().AddProperty("", "foo");

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void AddProperty_WhenKeyIsWhitespaces_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ListPropertiesProvider().AddProperty("   ", "foo");

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void AddProperty_WhenKeyAlreadyExist_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var testSubject = new ListPropertiesProvider();
        testSubject.AddProperty("foo", "foo");

        Action action = () => testSubject.AddProperty("foo", "foo");

        // Act & Assert
        action.Should().ThrowExactly<ArgumentOutOfRangeException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void TryGetProperty_WhenKeyIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ListPropertiesProvider().TryGetProperty(null, out var property);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void TryGetProperty_WhenKeyIsEmpty_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ListPropertiesProvider().TryGetProperty("", out var property);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void TryGetProperty_WhenKeyIsWhitespaces_ThrowsArgumentNullException()
    {
        // Arrange
        Action action = () => new ListPropertiesProvider().TryGetProperty("   ", out var property);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void Add_DictionaryInitializer_Works()
    {
        var provider = new ListPropertiesProvider
        {
            { "a", "1" },
            { "b", "2" }
        };
        provider.GetAllProperties().Should().BeEquivalentTo([new Property("a", "1"), new("b", "2")]);
    }

    [TestMethod]
    public void GenericEnumeration_Works()
    {
        var provider = new ListPropertiesProvider();
        provider.AddProperty("a", "1");
        provider.AddProperty("b", "2");
        provider.Should().BeEquivalentTo([new Property("a", "1"), new("b", "2")]);
    }

    [TestMethod]
    public void Enumeration_Works()
    {
        var provider = new ListPropertiesProvider();
        provider.AddProperty("a", "1");
        provider.AddProperty("b", "2");
        var enumerator = (provider as System.Collections.IEnumerable).GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().BeEquivalentTo(new Property("a", "1"));
    }

    private static void CheckPropertyExists(IAnalysisPropertyProvider provider, string id, string value)
    {
        provider.TryGetProperty(id, out Property foundProp).Should().BeTrue();
        foundProp.Id.Should().Be(id);
        foundProp.Value.Should().Be(value);
    }
}
