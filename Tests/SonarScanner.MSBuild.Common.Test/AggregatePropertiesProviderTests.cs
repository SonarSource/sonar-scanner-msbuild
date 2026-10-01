/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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
public class AggregatePropertiesProviderTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_Null() =>
        FluentActions.Invoking(() => new AggregatePropertiesProvider(null)).Should().ThrowExactly<ArgumentNullException>();

    [TestMethod]
    public void Empty()
    {
        var provider = new AggregatePropertiesProvider([]);
        provider.GetAllProperties().Should().BeEmpty();
        provider.TryGetProperty("any key", out var actualProperty).Should().BeFalse();
        actualProperty.Should().BeNull();
    }

    [TestMethod]
    public void Aggregation()
    {
        var provider1 = new ListPropertiesProvider();
        provider1.AddProperty("shared.key.A", "value A from one");
        provider1.AddProperty("shared.key.B", "value B from one");
        provider1.AddProperty("p1.unique.key.1", "p1 unique value 1");

        var provider2 = new ListPropertiesProvider();
        provider2.AddProperty("shared.key.A", "value A from two");
        provider2.AddProperty("shared.key.B", "value B from two");
        provider2.AddProperty("p2.unique.key.1", "p2 unique value 1");

        var provider3 = new ListPropertiesProvider();
        provider3.AddProperty("shared.key.A", "value A from three"); // this provider only has one of the shared values
        provider3.AddProperty("p3.unique.key.1", "p3 unique value 1");

        new AggregatePropertiesProvider(provider1, provider2, provider3).GetAllProperties().Should().BeEquivalentTo([
            new Property("shared.key.A", "value A from one"),
            new Property("shared.key.B", "value B from one"),
            new Property("p1.unique.key.1", "p1 unique value 1"),
            new Property("p2.unique.key.1", "p2 unique value 1"),
            new Property("p3.unique.key.1", "p3 unique value 1")]);

        // Reverse the order and try again
        new AggregatePropertiesProvider(provider3, provider2, provider1).GetAllProperties().Should().BeEquivalentTo([
            new Property("shared.key.A", "value A from three"),
            new Property("shared.key.B", "value B from two"),
            new Property("p1.unique.key.1", "p1 unique value 1"),
            new Property("p2.unique.key.1", "p2 unique value 1"),
            new Property("p3.unique.key.1", "p3 unique value 1")]);
    }

    [TestMethod]
    public void GetAllPropertiesWithProvider()
    {
        var listPropertiesProvider = new ListPropertiesProvider(PropertyProviderKind.SQ_SERVER_SETTINGS);
        listPropertiesProvider.AddProperty("shared.key.A", "value A from one");
        listPropertiesProvider.AddProperty("key.B", "value B from one");
        listPropertiesProvider.AddProperty("p1.unique.key.1", "p1 unique value 1");
        var args = new List<ArgumentInstance>
        {
            new(CmdLineArgPropertyProvider.Descriptor, "shared.key.A=value A from one"),
            new(CmdLineArgPropertyProvider.Descriptor, "p2.unique.key.1=p2 unique value 1")
        };
        CmdLineArgPropertyProvider.TryCreateProvider(args, new TestLogger(), out var commandLineProvider);

        var aggProvider = new AggregatePropertiesProvider(commandLineProvider, listPropertiesProvider);
        aggProvider.GetAllPropertiesWithProvider().Should().SatisfyRespectively(
            x =>
            {
                x.Key.Id.Should().Be("shared.key.A");
                x.Key.Value.Should().Be("value A from one");
                x.Value.ProviderType.Should().Be(PropertyProviderKind.CLI);
            },
            x =>
            {
                x.Key.Id.Should().Be("p2.unique.key.1");
                x.Key.Value.Should().Be("p2 unique value 1");
                x.Value.ProviderType.Should().Be(PropertyProviderKind.CLI);
            },
            x =>
            {
                x.Key.Id.Should().Be("key.B");
                x.Key.Value.Should().Be("value B from one");
                x.Value.ProviderType.Should().Be(PropertyProviderKind.SQ_SERVER_SETTINGS);
            },
            x =>
            {
                x.Key.Id.Should().Be("p1.unique.key.1");
                x.Key.Value.Should().Be("p1 unique value 1");
                x.Value.ProviderType.Should().Be(PropertyProviderKind.SQ_SERVER_SETTINGS);
            });
    }

    [TestMethod]
    public void NestedAggregate_ReturnsLeafProvider()
    {
        new AggregatePropertiesProvider(new ListPropertiesProvider(), new AggregatePropertiesProvider(new ListPropertiesProvider([new("key", "value")])))
            .TryGetProperty("key", out _, out var provider)
            .Should().BeTrue();
        provider.Should().BeOfType<ListPropertiesProvider>().Which.HasProperty("key");
    }
}
