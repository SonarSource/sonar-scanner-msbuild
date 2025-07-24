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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class ListPropertyProviderTests
{
    [TestMethod]
    public void Ctor_NullEnumerable_Throws()
    {
        var act = () => new ListPropertiesProvider(null);
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("properties");
    }

    [TestMethod]
    public void Ctor_NullDictionary_Throws()
    {
        var act = () => new ListPropertiesProvider((IDictionary<string, string>)null);
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("keyValuePairs");
    }

    [TestMethod]
    public void Ctor_Empty_SetsUnknownProviderType()
    {
        var provider = new ListPropertiesProvider();
        provider.ProviderType.Should().Be(PropertyProviderKind.UNKNOWN);
        provider.GetAllProperties().Should().BeEmpty();
    }

    [TestMethod]
    public void Ctor_WithProviderType_SetsType()
    {
        var provider = new ListPropertiesProvider(PropertyProviderKind.CLI);
        provider.ProviderType.Should().Be(PropertyProviderKind.CLI);
    }

    [TestMethod]
    public void Ctor_FromEnumerable_AddsProperties()
    {
        var props = new[] { new Property("a", "1"), new Property("b", "2") };
        var provider = new ListPropertiesProvider(props);
        provider.GetAllProperties().Should().BeEquivalentTo(props);
    }

    [TestMethod]
    public void Ctor_FromDictionary_AddsProperties()
    {
        var dict = new Dictionary<string, string> { { "a", "1" }, { "b", "2" } };
        var provider = new ListPropertiesProvider(dict, PropertyProviderKind.CLI);
        provider.GetAllProperties().Select(x => x.Id).Should().BeEquivalentTo(dict.Keys);
        provider.ProviderType.Should().Be(PropertyProviderKind.CLI);
    }

    [TestMethod]
    public void AddProperty_AddsAndReturnsProperty()
    {
        var provider = new ListPropertiesProvider();
        var prop = provider.AddProperty("key", "val");
        prop.Id.Should().Be("key");
        prop.Value.Should().Be("val");
        provider.GetAllProperties().Should().ContainSingle(x => x.Id == "key" && x.Value == "val");
    }

    [TestMethod]
    public void AddProperty_DuplicateKey_Throws()
    {
        var provider = new ListPropertiesProvider();
        provider.AddProperty("key", "val");
        Action act = () => provider.AddProperty("key", "other");
        act.Should().ThrowExactly<ArgumentOutOfRangeException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void AddProperty_NullOrWhitespaceKey_Throws(string key)
    {
        var provider = new ListPropertiesProvider();
        Action act = () => provider.AddProperty(key!, "val");
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
    }

    [TestMethod]
    public void TryGetProperty_FindsExisting()
    {
        var provider = new ListPropertiesProvider();
        provider.AddProperty("key", "val");
        provider.TryGetProperty("key", out var prop).Should().BeTrue();
        prop.Should().NotBeNull();
        prop.Id.Should().Be("key");
        prop.Value.Should().Be("val");
    }

    [TestMethod]
    public void TryGetProperty_Missing_ReturnsFalse()
    {
        var provider = new ListPropertiesProvider();
        provider.AddProperty("key", "val");
        provider.TryGetProperty("other", out var prop).Should().BeFalse();
        prop.Should().BeNull();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void TryGetProperty_NullOrWhitespaceKey_Throws(string key)
    {
        var provider = new ListPropertiesProvider();
        Action act = () => provider.TryGetProperty(key!, out _);
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("key");
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
    public void Enumeration_Works()
    {
        var provider = new ListPropertiesProvider();
        provider.AddProperty("a", "1");
        provider.AddProperty("b", "2");
        provider.Should().BeEquivalentTo([new Property("a", "1"), new("b", "2")]);
    }
}
