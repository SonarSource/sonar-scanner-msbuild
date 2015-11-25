//-----------------------------------------------------------------------
// <copyright file="RuleSetAssertions.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TestUtilities
{
    public static class RuleSetAssertions
    {
        public const string DefaultActionValue = "Default";

        private static readonly XName IncludeElementName = "Include";
        private static readonly XName PathAttrName = "Path";
        private static readonly XName ActionAttrName = "Action";

        public static void AssertExpectedIncludeFiles(string rulesetFilePath, params string[] expectedIncludePaths)
        {
            XDocument doc = XDocument.Load(rulesetFilePath);
            IEnumerable<XElement> includeElements = doc.Descendants(IncludeElementName);
            foreach (string expected in expectedIncludePaths)
            {
                AssertSingleIncludeExists(includeElements, expected);
            }

            Assert.AreEqual(expectedIncludePaths.Length, includeElements.Count(), "Unexpected number of Includes");
        }

        public static void AssertExpectedIncludeAction(string rulesetFilePath, string includePath, string expectedAction)
        {
            XDocument doc = XDocument.Load(rulesetFilePath);
            IEnumerable<XElement> includeElements = doc.Descendants(IncludeElementName);
            XElement includeElement = AssertSingleIncludeExists(includeElements, includePath);

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
