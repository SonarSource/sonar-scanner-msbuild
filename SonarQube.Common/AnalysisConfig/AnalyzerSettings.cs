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
using System.Xml.Serialization;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class containing the information required to configure
    /// the compiler for Roslyn analysis
    /// </summary>
    /// <remarks>This class is XML-serializable</remarks>
    public class AnalyzerSettings
    {
        public AnalyzerSettings()
        {
        }

        public AnalyzerSettings(string language, string ruleSetFilePath, IEnumerable<string> analyzerAssemblies, IEnumerable<string> additionalFiles)
        {
            if (string.IsNullOrWhiteSpace(ruleSetFilePath))
            {
                throw new ArgumentNullException("ruleSetFilePath");
            }
            if (analyzerAssemblies == null)
            {
                throw new ArgumentNullException("analyzerAssemblies");
            }
            if (additionalFiles == null)
            {
                throw new ArgumentNullException("additionalFiles");
            }

            this.Language = language;
            this.RuleSetFilePath = ruleSetFilePath;
            this.AnalyzerAssemblyPaths = new List<string>(analyzerAssemblies);
            this.AdditionalFilePaths = new List<string>(additionalFiles);
        }

        /// <summary>
        /// Language which this settings refers to
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Path to the ruleset for the Roslyn analyzers
        /// </summary>
        public string RuleSetFilePath { get; set; }

        /// <summary>
        /// File paths for all of the assemblies to pass to the compiler as analyzers
        /// </summary>
        /// <remarks>This includes analyzer assemblies and their dependencies</remarks>
        [XmlArray]
        [XmlArrayItem("Path")]
        public List<string> AnalyzerAssemblyPaths { get; set; }

        /// <summary>
        /// File paths for all files to pass as "AdditionalFiles" to the compiler
        /// </summary>
        [XmlArray]
        [XmlArrayItem("Path")]
        public List<string> AdditionalFilePaths { get; set; }
    }
}
