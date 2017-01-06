/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

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
