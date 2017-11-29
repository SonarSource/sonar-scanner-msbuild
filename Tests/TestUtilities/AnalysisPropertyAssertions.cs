/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;

namespace TestUtilities
{
    public static class AnalysisPropertyAssertions
    {
        public static void AssertExpectedPropertyCount(this IAnalysisPropertyProvider provider, int expected)
        {
            var allProperties = provider.GetAllProperties();
            Assert.IsNotNull(allProperties, "Returned list of properties should not be null");
            Assert.AreEqual(expected, allProperties.Count(), "Unexpected number of properties returned");
        }

        public static void AssertExpectedPropertyValue(this IAnalysisPropertyProvider provider, string key, string expectedValue)
        {
            var found = provider.TryGetProperty(key, out Property property);

            Assert.IsTrue(found, "Expected property was not found. Key: {0}", key);
            Assert.AreEqual(expectedValue, property.Value, "");
        }

        public static void AssertPropertyDoesNotExist(this IAnalysisPropertyProvider provider, string key)
        {
            var found = provider.TryGetProperty(key, out Property property);

            Assert.IsFalse(found, "Not expecting the property to exist. Key: {0}, value: {1}", key);
        }
    }
}
