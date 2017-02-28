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
using System.Diagnostics;
using System.IO;

namespace TestUtilities
{
    /// <summary>
    /// Utility class that reads properties from a standard format SonarQube properties file (e.g. sonar-scanner.properties)
    /// </summary>
    public class SQPropertiesFileReader
    {


        /// <summary>
        /// Mapping of property names to values
        /// </summary>
        private JavaProperties properties;

        private readonly string propertyFilePath;

        #region Public methods

        /// <summary>
        /// Creates a new provider that reads properties from the
        /// specified properties file
        /// </summary>
        /// <param name="fullPath">The full path to the SonarQube properties file. The file must exist.</param>
        public SQPropertiesFileReader(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                throw new ArgumentNullException("fullPath");
            }
                        
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException();
            }

            this.propertyFilePath = fullPath;
            this.ExtractProperties(fullPath);
        }

        public void AssertSettingExists(string key, string expectedValue)
        {
            string actualValue = this.properties.GetProperty(key);
            bool found = actualValue != null;

            Assert.IsTrue(found, "Expected setting was not found. Key: {0}", key);
            Assert.AreEqual(expectedValue, actualValue, "Property does not have the expected value. Key: {0}", key);
        }

        public void AssertSettingDoesNotExist(string key)
        {
            string actualValue = this.properties.GetProperty(key);
            bool found = actualValue != null;

            Assert.IsFalse(found, "Not expecting setting to be found. Key: {0}, value: {1}", key, actualValue);
        }

        #endregion

        #region FilePropertiesProvider

        private void ExtractProperties(string fullPath)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(fullPath), "fullPath should be specified");

            this.properties = new JavaProperties();
            this.properties.Load(File.Open(fullPath, FileMode.Open));
        }

        #endregion
    }
}
