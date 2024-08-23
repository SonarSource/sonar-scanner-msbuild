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

using FluentAssertions;
using SonarScanner.MSBuild.Common;

namespace TestUtilities;

public static class AnalysisPropertyAssertions
{
    public static void AssertExpectedPropertyCount(this IAnalysisPropertyProvider provider, int expected)
    {
        var allProperties = provider.GetAllProperties();
        allProperties.Should().NotBeNull("Returned list of properties should not be null");
        allProperties.Should().HaveCount(expected, "Unexpected number of properties returned");
    }

    public static void AssertExpectedPropertyValue(this IAnalysisPropertyProvider provider, string key, string expectedValue)
    {
        var found = provider.TryGetProperty(key, out var property);

        found.Should().BeTrue("Expected property was not found. Key: {0}", key);
        property.Value.Should().Be(expectedValue, "");
    }

    public static void AssertPropertyDoesNotExist(this IAnalysisPropertyProvider provider, string key)
    {
        var found = provider.TryGetProperty(key, out _);

        found.Should().BeFalse("Not expecting the property to exist. Key: {0}", key);
    }
}
