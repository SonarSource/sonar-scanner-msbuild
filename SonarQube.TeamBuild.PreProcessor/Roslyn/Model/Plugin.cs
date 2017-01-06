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
using System.Xml.Serialization;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    /// <summary>
    /// XML-serializable data class for a single SonarQube plugin containing an analyzer
    /// </summary>
    public class Plugin
    {
        public Plugin()
        {
        }

        public Plugin(string key, string version, string staticResourceName)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException("key");
            }
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentNullException("version");
            }
            if (string.IsNullOrWhiteSpace(staticResourceName))
            {
                throw new ArgumentNullException("staticResourceName");
            }
            this.Key = key;
            this.Version = version;
            this.StaticResourceName = staticResourceName;
        }

        [XmlAttribute("Key")]
        public string Key { get; set; }

        [XmlAttribute("Version")]
        public string Version { get; set; }

        /// <summary>
        /// Name of the static resource in the plugin that contains the analyzer artefacts
        /// </summary>
        [XmlAttribute("StaticResourceName")]
        public string StaticResourceName { get; set; }

    }
}
