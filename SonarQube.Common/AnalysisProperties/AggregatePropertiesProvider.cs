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
using System.Diagnostics;
using System.Linq;

namespace SonarQube.Common
{
    /// <summary>
    /// Properties provider that aggregates the properties from multiple "child" providers.
    /// The child providers are checked in order until one of them returns a value.
    /// </summary>
    public class AggregatePropertiesProvider : IAnalysisPropertyProvider
    {
        /// <summary>
        /// Ordered list of child providers
        /// </summary>
        private readonly IAnalysisPropertyProvider[] providers;

        #region Public methods

        public AggregatePropertiesProvider(params IAnalysisPropertyProvider[] providers)
        {
            if (providers == null)
            {
                throw new ArgumentNullException("providers");
            }

            this.providers = providers;
        }

        #endregion

        #region IAnalysisPropertyProvider interface

        public IEnumerable<Property> GetAllProperties()
        {
            HashSet<string> allKeys = new HashSet<string>(this.providers.SelectMany(p => p.GetAllProperties().Select(s => s.Id)));

            IList<Property> allProperties = new List<Property>();
            foreach (string key in allKeys)
            {
                Property property;
                bool match = this.TryGetProperty(key, out property);

                Debug.Assert(match, "Expecting to find value for all keys. Key: " + key);
                allProperties.Add(property);
            }

            return allProperties;
        }

        public bool TryGetProperty(string key, out Property property)
        {
            property = null;

            foreach (IAnalysisPropertyProvider current in this.providers)
            {
                if (current.TryGetProperty(key, out property))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}