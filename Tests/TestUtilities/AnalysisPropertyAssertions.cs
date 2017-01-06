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
using SonarQube.Common;
using System.Collections.Generic;
using System.Linq;

namespace TestUtilities
{
    public static class AnalysisPropertyAssertions
    {
        public static void AssertExpectedPropertyCount(this IAnalysisPropertyProvider provider, int expected)
        {
            IEnumerable<Property> allProperties = provider.GetAllProperties();
            Assert.IsNotNull(allProperties, "Returned list of properties should not be null");
            Assert.AreEqual(expected, allProperties.Count(), "Unexpected number of properties returned");
        }

        public static void AssertExpectedPropertyValue(this IAnalysisPropertyProvider provider, string key, string expectedValue)
        {
            Property property;
            bool found = provider.TryGetProperty(key, out property);

            Assert.IsTrue(found, "Expected property was not found. Key: {0}", key);
            Assert.AreEqual(expectedValue, property.Value, "");
        }

        public static void AssertPropertyDoesNotExist(this IAnalysisPropertyProvider provider, string key)
        {
            Property property;
            bool found = provider.TryGetProperty(key, out property);

            Assert.IsFalse(found, "Not expecting the property to exist. Key: {0}, value: {1}", key);
        }
    }
}
