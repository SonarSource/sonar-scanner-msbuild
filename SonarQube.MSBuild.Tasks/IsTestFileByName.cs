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

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SonarQube.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace SonarQube.MSBuild.Tasks
{
    /// <summary>
    /// MSBuild task that determines whether a file is should be treated as a
    /// test file for analysis purposes based on its path and file name
    /// </summary>
    /// <remarks>The task applies a regular expression to the file name being tested to determine whether
    /// the file is test file or not. The regular expression used is read from the analysis config file.</remarks>
    public sealed class IsTestFileByName : Task
    {
        /// <summary>
        /// Id of the SonarQube test setting that specifies the RegEx to use when determining
        /// if a project is a test project or not
        /// </summary>
        public const string TestRegExSettingId = "sonar.msbuild.testProjectPattern";

        #region Input properties

        /// <summary>
        /// The directory containing the analysis config settings file
        /// </summary>
        [Required]
        public string AnalysisConfigDir { get; set; }

        /// <summary>
        /// The full path and file name of the file being checked
        /// </summary>
        [Required]
        public string FullFilePath { get; set; }

        /// <summary>
        /// Return value - true or false
        /// </summary>
        [Output]
        public bool IsTest { get; private set; }

        #endregion Input properties

        #region Overrides

        public override bool Execute()
        {
            bool taskSuccess = true;

            AnalysisConfig config = TaskUtilities.TryGetConfig(this.AnalysisConfigDir, new MSBuildLoggerAdapter(this.Log));

            if (config != null)
            {
                string regEx = TryGetRegularExpression(config);

                try
                {
                    this.IsTest = !string.IsNullOrEmpty(regEx) && Regex.IsMatch(this.FullFilePath, regEx, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException ex) // thrown for invalid regular expressions
                {
                    taskSuccess = false;
                    this.Log.LogError(Resources.IsTest_InvalidRegularExpression, regEx, ex.Message, TestRegExSettingId);
                }
            }

            return !this.Log.HasLoggedErrors && taskSuccess;
        }

        #endregion Overrides

        #region Private methods

        private string TryGetRegularExpression(AnalysisConfig config)
        {
            Debug.Assert(config != null, "Not expecting the supplied config to be null");

            string regEx;
            config.GetAnalysisSettings(true).TryGetValue(TestRegExSettingId, out regEx);
            
            if (!string.IsNullOrWhiteSpace(regEx))
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.IsTest_UsingRegExFromConfig, regEx);
            }

            return regEx;
        }
        
        #endregion Private methods
    }
}