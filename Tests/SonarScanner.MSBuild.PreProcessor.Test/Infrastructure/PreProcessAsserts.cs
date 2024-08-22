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

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;

namespace SonarScanner.MSBuild.PreProcessor.Test;

internal static class PreProcessAsserts
{
    #region Public methods

    public static void AssertRuleSetContainsRules(string filePath, params string[] expectedRuleIds)
    {
        File.Exists(filePath).Should().BeTrue("Expected ruleset file does not exist: {0}", filePath);

        var doc = XDocument.Load(filePath);

        foreach (var ruleId in expectedRuleIds)
        {
            AssertRuleIdExists(doc, ruleId);
        }

        AssertExpectedRuleCount(doc, expectedRuleIds.Length);
    }

    #endregion Public methods

    #region Private methods

    private static void AssertRuleIdExists(XDocument doc, string ruleId)
    {
        Debug.WriteLine(doc.ToString());
        var element = doc.Descendants().Single(e => e.Name == "Rule" && HasRuleIdAttribute(e, ruleId));
        element.Should().NotBeNull("Could not find ruleId with expected id: {0}", ruleId);
    }

    private static bool HasRuleIdAttribute(XElement element, string ruleId)
    {
        return element.Attributes().Any(a => a.Name == "Id" && a.Value == ruleId);
    }

    private static void AssertExpectedRuleCount(XDocument doc, int expectedCount)
    {
        var rules = doc.Descendants().Where(e => e.Name == "Rule");
        rules.Should().HaveCount(expectedCount, "Unexpected number of rules");
    }

    #endregion Private methods
}
