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
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class to describe an additional analysis configuration setting
    /// /// </summary>
    /// <remarks>The class is XML-serializable</remarks>
    public class ConfigSetting
    {
        #region Data

        /// <summary>
        /// The identifier for the setting
        /// </summary>
        [XmlAttribute]
        public string Id { get; set; }

        /// <summary>
        /// The value of the setting
        /// </summary>
        [XmlAttribute]
        public string Value { get; set; }

        #endregion

        #region Static helper methods

        /// <summary>
        /// Comparer to use when comparing keys of analysis settings
        /// </summary>
        public static readonly IEqualityComparer<string> SettingKeyComparer = StringComparer.Ordinal;

        /// <summary>
        /// Comparer to use when comparing keys of analysis settings
        /// </summary>
        public static readonly IEqualityComparer<string> SettingValueComparer = StringComparer.Ordinal;

        #endregion
    }
}
