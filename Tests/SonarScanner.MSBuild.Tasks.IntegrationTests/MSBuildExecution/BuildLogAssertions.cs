/*
 * SonarScanner for MSBuild
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
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// Assertion methods that check the content of the custom XML build log
    /// </summary>
    internal static class BuildLogAssertions
    {
        /// <summary>
        /// Checks that building the specified target succeeded.
        /// </summary>
        public static void AssertTargetSucceeded(this BuildLog log, string target)
        {
            AssertTargetExecuted(log, target);
            log.BuildSucceeded.Should().BeTrue();
        }

        /// <summary>
        /// Checks that building the specified target failed.
        /// </summary>
        public static void AssertTargetFailed(this BuildLog log, string target)
        {
            AssertTargetExecuted(log, target);
            log.BuildSucceeded.Should().BeFalse();
        }

        public static void AssertExpectedPropertyValue(this BuildLog log, string propertyName, string expectedValue)
        {
            if (expectedValue == null)
            {
                AssertPropertyDoesNotExist(log, propertyName);
            }
            else
            {
                var propertyValue = AssertPropertyExists(log, propertyName);
                propertyValue.Should().Be(expectedValue, "Property '{0}' does not have the expected value", propertyName);
            }
        }

        public static string AssertPropertyExists(this BuildLog log, string propertyName)
        {
            var exists = log.TryGetPropertyValue(propertyName, out var propertyValue);
            exists.Should().BeTrue(
                "Expecting the property to exist. Property: {0}, Value: {1}", propertyName, propertyValue);

            return propertyValue;
        }

        public static void AssertPropertyDoesNotExist(this BuildLog log, string propertyName)
        {
            var exists = log.TryGetPropertyValue(propertyName, out var propertyValue);
            exists.Should().BeFalse(
                "Not expecting the property to exist. Property: {0}, Value: {1}", propertyName, propertyValue);
        }

        public static void AssertTargetExecuted(this BuildLog log, string targetName)
        {
            var found = log.Targets.FirstOrDefault(t => t.Equals(targetName, StringComparison.InvariantCulture));
            found.Should().NotBeNull("Specified target was not executed: {0}", targetName);
        }

        public static void AssertTargetNotExecuted(this BuildLog log, string targetName)
        {
            var found = log.Targets.FirstOrDefault(t => t.Equals(targetName, StringComparison.InvariantCulture));
            found.Should().BeNull("Not expecting the target to have been executed: {0}", targetName);
        }

        public static void AssertTaskExecuted(this BuildLog log, string taskName)
        {
            var found = log.Tasks.FirstOrDefault(t => t.Equals(taskName, StringComparison.InvariantCulture));
            found.Should().NotBeNull("Specified task was not executed: {0}", taskName);
        }

        public static void AssertTaskNotExecuted(this BuildLog log, string taskName)
        {
            var found = log.Tasks.FirstOrDefault(t => t.Equals(taskName, StringComparison.InvariantCulture));
            found.Should().BeNull("Not expecting the task to have been executed: {0}", taskName);
        }

        /// <summary>
        /// Checks that the expected tasks were executed in the specified order
        /// </summary>
        public static void AssertExpectedTargetOrdering(this BuildLog log, params string[] expected)
        {
            foreach (var target in expected)
            {
                AssertTargetExecuted(log,target);
            }

            var actual = log.Targets.Where(t => expected.Contains(t, StringComparer.Ordinal)).ToArray();

            Console.WriteLine("Expected target order: {0}", string.Join(", ", expected));
            Console.WriteLine("Actual target order: {0}", string.Join(", ", actual));

            CollectionAssert.AreEqual(expected, actual, "Targets were not executed in the expected order");
        }

        public static void AssertNoWarningsOrErrors(this BuildLog log)
        {
            AssertExpectedErrorCount(log, 0);
            AssertExpectedWarningCount(log, 0);
        }

        public static void AssertExpectedErrorCount(this BuildLog log, int expected)
        {
            log.Errors.Count.Should().Be(expected);
        }

        public static void AssertExpectedWarningCount(this BuildLog log, int expected)
        {
            log.Warnings.Count.Should().Be(expected);
        }
    }
}
