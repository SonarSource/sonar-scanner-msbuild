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
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class DefaultPropertiesFileTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void DefaultProperties_AreValid()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string propertiesFile = TestUtils.EnsureDefaultPropertiesFileExists(testDir, this.TestContext);

            // Act - will error if the file is badly-formed
            AnalysisProperties defaultProps = AnalysisProperties.Load(propertiesFile);

            // Assert - check the default properties
            AssertPropertyHasValue(SonarProperties.HostUrl, "http://localhost:9000", defaultProps);

            Assert.AreEqual(1, defaultProps.Count, "Unexpected number of properties defined in the default properties file");
        }

        #endregion

        #region Checks

        private static void AssertPropertyHasValue(string key, string expectedValue, AnalysisProperties properties)
        {
            Property match;
            bool found = Property.TryGetProperty(key, properties, out match);

            Assert.IsTrue(found, "Expected property was not found. Key: {0}", key);
            Assert.AreEqual(expectedValue, match.Value, "Property does not have the expected value. Key: {0}", key);
        }

        #endregion

    }
}
