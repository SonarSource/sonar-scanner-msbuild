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
    /// Data class to describe the output of a single type of analyzer
    /// </summary>
    /// <remarks>The class is XML-serializable.
    /// Examples of types of analyzer: FxCop, StyleCode, CodeCoverage, Roslyn Analyzers...</remarks>
    public class AnalysisResult
    {
        #region Data

        /// <summary>
        /// The identifier for the type of analyzer
        /// </summary>
        /// <remarks>Each type </remarks>
        [XmlAttribute]
        public string Id { get; set; }

        /// <summary>
        /// The location of the output produced by the analyzer
        /// </summary>
        /// <remarks>This will normally be an absolute file path</remarks>
        [XmlAttribute]
        public string Location { get; set; }

        #endregion

        #region Static helpers

        /// <summary>
        /// Comparer to use when comparing keys of analysis results
        /// </summary>
        public static readonly IEqualityComparer<string> ResultKeyComparer = StringComparer.Ordinal;

        #endregion


    }
}
