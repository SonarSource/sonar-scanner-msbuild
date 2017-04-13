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
 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TestUtilities
{
    public static class RuleSetAssertions
    {
        public const string WarningActionValue = "Warning";

        private static readonly XName IncludeElementName = "Include";
        private static readonly XName PathAttrName = "Path";
        private static readonly XName ActionAttrName = "Action";

        public static void AssertExpectedIncludeFilesAndDefaultAction(string rulesetFilePath, params string[] expectedIncludePaths)
        {
            XDocument doc = XDocument.Load(rulesetFilePath);
            IEnumerable<XElement> includeElements = doc.Descendants(IncludeElementName);
            foreach (string expected in expectedIncludePaths)
            {
                XElement includeElement = AssertSingleIncludeExists(includeElements, expected);
                
                // We expect the Include Action to always be "Warning"
                AssertExpectedIncludeAction(includeElement, WarningActionValue);
            }

            Assert.AreEqual(expectedIncludePaths.Length, includeElements.Count(), "Unexpected number of Includes");
        }

        private static void AssertExpectedIncludeAction(XElement includeElement, string expectedAction)
        {
            XAttribute actionAttr = includeElement.Attribute(ActionAttrName);

            Assert.IsNotNull(actionAttr, "Include element does not have an Action attribute: {0}", includeElement);
            Assert.AreEqual(expectedAction, actionAttr.Value, "Unexpected Action value");
        }

        private static XElement AssertSingleIncludeExists(IEnumerable<XElement> includeElements, string expectedPath)
        {
            IEnumerable<XElement> matches = includeElements.Where(i => HasIncludePath(i, expectedPath));
            Assert.AreEqual(1, matches.Count(), "Expecting one and only Include with Path '{0}'", expectedPath);
            return matches.First();
        }

        private static bool HasIncludePath(XElement includeElement, string includePath)
        {
            XAttribute attr;
            attr = includeElement.Attributes(PathAttrName).Single();

            return attr != null && string.Equals(attr.Value, includePath, StringComparison.OrdinalIgnoreCase);
        }

    }
}
