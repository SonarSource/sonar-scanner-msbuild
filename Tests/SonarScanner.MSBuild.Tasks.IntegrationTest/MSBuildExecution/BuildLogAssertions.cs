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
using System.Linq;
using FluentAssertions;

namespace SonarScanner.MSBuild.Tasks.IntegrationTest;

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

    public static void AssertTaskExecuted(this BuildLog log, string taskName) =>
        log.ContainsTask(taskName).Should().BeTrue("Specified task was not executed: {0}", taskName);

    public static void AssertTaskNotExecuted(this BuildLog log, string taskName) =>
        log.ContainsTask(taskName).Should().BeFalse("Not expecting the task to have been executed: {0}", taskName);

    /// <summary>
    /// Checks that the expected tasks were executed in the specified order
    /// </summary>
    public static void AssertTargetOrdering(this BuildLog log, params string[] expected)
    {
        foreach (var target in expected)
        {
            AssertTargetExecuted(log, target);
        }

        var actual = log.Targets.Where(t => expected.Contains(t, StringComparer.Ordinal)).ToArray();

        Console.WriteLine("Expected target order: {0}", string.Join(", ", expected));
        Console.WriteLine("Actual target order: {0}", string.Join(", ", actual));

        actual.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
    }

    public static void AssertNoWarningsOrErrors(this BuildLog log)
    {
        AssertErrorCount(log, 0);
        AssertWarningCount(log, 0);
    }

    public static void AssertErrorCount(this BuildLog log, int expected) =>
        log.Errors.Should().HaveCount(expected);

    public static void AssertWarningCount(this BuildLog log, int expected) =>
        log.Warnings.Should().HaveCount(expected);

    public static string GetPropertyValue(this BuildLog log, string propertyName, bool allowMissing = false)
    {
        log.TryGetPropertyValue(propertyName, out var value);
        if (!allowMissing)
        {
            value.Should().NotBeNull($"Test logger error: failed to find captured property '{propertyName}'");
        }
        return value;
    }

    public static void AssertPropertyValue(this BuildLog log, string propertyName, string expectedValue)
    {
        var capturedValue = GetPropertyValue(log, propertyName, expectedValue == null);
        capturedValue.Should().Be(expectedValue, "Captured property '{0}' does not have the expected value", propertyName);
    }

    public static void AssertSingleItemExists(this BuildLog log, string itemName, string expectedValue)
    {
        var data = log.GetItem(itemName).SingleOrDefault(x => x.Text.Equals(expectedValue, StringComparison.Ordinal));
        data.Should().NotBeNull($"Test logger error: Failed to find expected item value. Item name: '{itemName}', expected value: {expectedValue}");
    }

    public static void AssertItemGroupCount(this BuildLog log, string itemName, int expectedCount) =>
        log.GetItem(itemName).Count().Should().Be(expectedCount);
}
