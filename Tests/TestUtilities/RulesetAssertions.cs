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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using SonarScanner.MSBuild.Common;

namespace TestUtilities;

public static class RuleSetAssertions
{
    public const string WarningActionValue = "Warning";

    private static readonly XName IncludeElementName = "Include";
    private static readonly XName PathAttrName = "Path";
    private static readonly XName ActionAttrName = "Action";

    public static void AssertExpectedIncludeFilesAndDefaultAction(string rulesetFilePath, params string[] expectedIncludePaths)
    {
        var doc = XDocument.Load(rulesetFilePath);
        var includeElements = doc.Descendants(IncludeElementName);
        foreach (var expected in expectedIncludePaths)
        {
            var includeElement = AssertSingleIncludeExists(includeElements, expected);

            // We expect the Include Action to always be "Warning"
            AssertExpectedIncludeAction(includeElement, WarningActionValue);
        }

        includeElements.Should().HaveCount(expectedIncludePaths.Length, "Unexpected number of Includes");
    }

    private static void AssertExpectedIncludeAction(XElement includeElement, string expectedAction)
    {
        var actionAttr = includeElement.Attribute(ActionAttrName);

        actionAttr.Should().NotBeNull("Include element does not have an Action attribute: {0}", includeElement);
        actionAttr.Value.Should().Be(expectedAction, "Unexpected Action value");
    }

    private static XElement AssertSingleIncludeExists(IEnumerable<XElement> includeElements, string expectedPath)
    {
        var matches = includeElements.Where(i => HasIncludePath(i, expectedPath));
        matches.Should().ContainSingle("Expecting one and only Include with Path '{0}'", expectedPath);
        return matches.First();
    }

    private static bool HasIncludePath(XElement includeElement, string includePath)
    {
        XAttribute attr;
        attr = includeElement.Attributes(PathAttrName).Single();

        return attr != null && string.Equals(attr.Value, includePath, StringComparison.OrdinalIgnoreCase);
    }

    public static string CheckMergedRulesetFile(string outputDirectory, string originalRulesetFullPath)
    {
        var expectedMergedRulesetFilePath = Path.Combine(outputDirectory, "merged.ruleset");

        File.Exists(expectedMergedRulesetFilePath).Should().BeTrue();

        // Check the file contents
        var actual = RuleSet.Load(expectedMergedRulesetFilePath);
        actual.Includes.Should().NotBeNull();
        actual.Includes.Count.Should().Be(1);
        CheckInclude(actual.Includes[0], originalRulesetFullPath, "Default");

        return expectedMergedRulesetFilePath;
    }

    private static void CheckInclude(Include actual, string expectedPath, string expectedAction)
    {
        actual.Path.Should().Be(expectedPath);
        actual.Action.Should().Be(expectedAction);
    }
}
