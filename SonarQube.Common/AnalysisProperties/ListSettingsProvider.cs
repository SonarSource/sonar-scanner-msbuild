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

using System;
using System.Collections.Generic;

namespace SonarQube.Common
{
    /// <summary>
    /// Simple settings provider that returns values from a list
    /// </summary>
    public class ListPropertiesProvider : IAnalysisPropertyProvider
    {
        private readonly IList<Property> properties;

        #region Public methods

        public ListPropertiesProvider()
        {
            this.properties = new List<Property>();
        }

        public ListPropertiesProvider(IEnumerable<Property> properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }

            this.properties = new List<Property>(properties);
        }

        public Property AddProperty(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException("key");
            }

            Property existing;
            if (this.TryGetProperty(key, out existing))
            {
                throw new ArgumentOutOfRangeException("key");
            }

            Property newProperty = new Property() { Id = key, Value = value };
            this.properties.Add(newProperty);
            return newProperty;
        }

        #endregion

        #region IAnalysisProperiesProvider interface

        public IEnumerable<Property> GetAllProperties()
        {
            return properties;
        }

        public bool TryGetProperty(string key, out Property property)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException("key");
            }

            return Property.TryGetProperty(key, this.properties, out property);
        }

        #endregion
    }
}