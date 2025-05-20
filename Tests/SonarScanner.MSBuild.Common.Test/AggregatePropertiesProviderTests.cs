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
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class AggregatePropertiesProviderTests
{
    public TestContext TestContext { get; set; }

    #region Tests

    [TestMethod]
    public void AggProperties_NullOrEmptyList()
    {
        // 1. Null -> error
        Action act = () => new AggregatePropertiesProvider(null);
        act.Should().ThrowExactly<ArgumentNullException>();

        // 2. Empty list of providers -> valid but returns nothing
        var provider = new AggregatePropertiesProvider([]);

        provider.GetAllProperties().Should().BeEmpty();
        var success = provider.TryGetProperty("any key", out var actualProperty);

        success.Should().BeFalse("Not expecting a property to be returned");
        actualProperty.Should().BeNull("Returned property should be null");
    }

    [TestMethod]
    public void AggProperties_Aggregation()
    {
        // Checks the aggregation works as expected

        // 0. Setup
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

        // 1. Ordering
        var aggProvider = new AggregatePropertiesProvider(provider1, provider2, provider3);

        aggProvider.AssertExpectedPropertyCount(5);

        aggProvider.AssertExpectedPropertyValue("shared.key.A", "value A from one");
        aggProvider.AssertExpectedPropertyValue("shared.key.B", "value B from one");

        aggProvider.AssertExpectedPropertyValue("p1.unique.key.1", "p1 unique value 1");
        aggProvider.AssertExpectedPropertyValue("p2.unique.key.1", "p2 unique value 1");
        aggProvider.AssertExpectedPropertyValue("p3.unique.key.1", "p3 unique value 1");

        // 2. Reverse the order and try again
        aggProvider = new AggregatePropertiesProvider(provider3, provider2, provider1);

        aggProvider.AssertExpectedPropertyCount(5);

        aggProvider.AssertExpectedPropertyValue("shared.key.A", "value A from three");
        aggProvider.AssertExpectedPropertyValue("shared.key.B", "value B from two");

        aggProvider.AssertExpectedPropertyValue("p1.unique.key.1", "p1 unique value 1");
        aggProvider.AssertExpectedPropertyValue("p2.unique.key.1", "p2 unique value 1");
        aggProvider.AssertExpectedPropertyValue("p3.unique.key.1", "p3 unique value 1");
    }

    [TestMethod]
    public void AggProperties_GetAllPropertiesPerProvider()
    {
        var listPropertiesProvider = new ListPropertiesProvider();
        listPropertiesProvider.AddProperty("shared.key.A", "value A from one");
        listPropertiesProvider.AddProperty("shared.key.B", "value B from one");
        listPropertiesProvider.AddProperty("p1.unique.key.1", "p1 unique value 1");

        var args = new List<ArgumentInstance>
        {
            new(CmdLineArgPropertyProvider.Descriptor, "shared.key.A=value A from one"),
            new(CmdLineArgPropertyProvider.Descriptor, "p2.unique.key.1=p2 unique value 1")
        };
        _ = CmdLineArgPropertyProvider.TryCreateProvider(args, new TestLogger(), out var commandLineProvider);
        var aggProvider = new AggregatePropertiesProvider(listPropertiesProvider, commandLineProvider);
        var expected = new Dictionary<PropertyProviderKind, IList<string>>
        {
            { PropertyProviderKind.SQ_SERVER_SETTINGS, ["shared.key.A", "shared.key.B", "p1.unique.key.1"] },
            { PropertyProviderKind.CLI, ["shared.key.A", "p2.unique.key.1"] }
        };

        aggProvider.AssertExpectedPropertyCount(4);
        aggProvider.GetAllPropertiesPerProvider().Select(x => x.Value.Select(x => x.Value)).Equals(expected);
    }

    #endregion Tests
}
