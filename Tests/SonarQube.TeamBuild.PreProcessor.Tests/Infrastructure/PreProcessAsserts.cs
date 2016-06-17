//-----------------------------------------------------------------------
// <copyright file="PreProcessAsserts.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal static class PreProcessAsserts
    {
        #region Public methods

        public static void AssertRuleSetContainsRules(string filePath, params string[] expectedRuleIds)
        {
            Assert.IsTrue(File.Exists(filePath), "Expected ruleset file does not exist: {0}", filePath);

            XDocument doc = XDocument.Load(filePath);

            foreach (string ruleId in expectedRuleIds)
            {
                AssertRuleIdExists(doc, ruleId);
            }

            AssertExpectedRuleCount(doc, expectedRuleIds.Length);
        }

        #endregion

        #region Private methods

        private static void AssertRuleIdExists(XDocument doc, string ruleId)
        {
            Debug.WriteLine(doc.ToString());
            XElement element = doc.Descendants().Single(e => e.Name == "Rule" && HasRuleIdAttribute(e, ruleId));
            Assert.IsNotNull(element, "Could not find ruleId with expected id: {0}", ruleId);
        }

        private static bool HasRuleIdAttribute(XElement element, string ruleId)
        {
            return element.Attributes().Any(a => a.Name == "Id" && a.Value == ruleId);
        }

        private static void AssertExpectedRuleCount(XDocument doc, int expectedCount)
        {
            IEnumerable<XElement> rules = doc.Descendants().Where(e => e.Name == "Rule");
            Assert.AreEqual(expectedCount, rules.Count(), "Unexpected number of rules");
        }

        #endregion
    }
}
