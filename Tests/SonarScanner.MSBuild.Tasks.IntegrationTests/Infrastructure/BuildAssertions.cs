/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
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

using FluentAssertions;
using Microsoft.Build.Execution;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests
{
    public static class BuildAssertions
    {
        #region Assertions

        public static void AssertExpectedPropertyValue(ProjectInstance projectInstance, string propertyName, string expectedValue)
        {
            var propertyInstance = projectInstance.GetProperty(propertyName);
            if (expectedValue == null &&
                propertyInstance == null)
            {
                return;
            }

            propertyInstance.Should().NotBeNull("The expected property does not exist: {0}", propertyName);
            propertyInstance.EvaluatedValue.Should().Be(expectedValue, "Property '{0}' does not have the expected value", propertyName);
        }

        public static void AssertPropertyDoesNotExist(ProjectInstance projectInstance, string propertyName)
        {
            var propertyInstance = projectInstance.GetProperty(propertyName);

            var value = propertyInstance?.EvaluatedValue;

            propertyInstance.Should().BeNull("Not expecting the property to exist. Property: {0}, Value: {1}", propertyName, value);
        }

        #endregion Assertions
    }
}
