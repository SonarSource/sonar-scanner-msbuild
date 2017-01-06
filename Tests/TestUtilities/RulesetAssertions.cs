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
