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
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace SonarQube.MSBuild.Tasks
{
    /// <summary>
    /// Build task to return the Roslyn analyzer settings from the analysis config file
    /// </summary>
    public class GetAnalyzerSettings : Task
    {
        private const string DllExtension = ".dll";

        #region Input properties

        /// <summary>
        /// The directory containing the analysis config settings file
        /// </summary>
        [Required]
        public string AnalysisConfigDir { get; set; }

        /// <summary>
        /// List of additional files that would be passed to the compiler if
        /// no SonarQube analysis was happening.
        /// </summary>
        [Required]
        public string[] OriginalAdditionalFiles { get; set; }

        /// <summary>
        /// The language for which we are gettings the settings
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Path to the generated ruleset file to use
        /// </summary>
        [Output]
        public string RuleSetFilePath { get; private set; }

        /// <summary>
        /// List of analyzer assemblies and dependencies to pass to the compiler as analyzers
        /// </summary>
        [Output]
        public string[] AnalyzerFilePaths { get; private set; }

        /// <summary>
        /// List of additional files to pass to the compiler
        /// </summary>
        [Output]
        public string[] AdditionalFiles { get; private set; }

        /// <summary>
        /// List of additional files that are originally passed to the compiler,
        /// but <see cref="AdditionalFiles"/> contains an explicit override for them.
        /// </summary>
        [Output]
        public string[] AdditionalFilesToRemove { get; private set; }

        #endregion Input properties

        #region Overrides

        public override bool Execute()
        {
            AnalysisConfig config = TaskUtilities.TryGetConfig(this.AnalysisConfigDir, new MSBuildLoggerAdapter(this.Log));
            ExecuteAnalysis(config);

            return !this.Log.HasLoggedErrors;
        }

        private void ExecuteAnalysis(AnalysisConfig config)
        {
            if (config == null || Language == null)
            {
                return;
            }

            IList<AnalyzerSettings> analyzers = config.AnalyzersSettings;
            if (analyzers == null)
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_NotSpecifiedInConfig, Language);
                return;
            }

            AnalyzerSettings settings = analyzers.SingleOrDefault(s => Language.Equals(s.Language));
            if (settings == null)
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_NotSpecifiedInConfig, Language);
                return;
            }

            this.RuleSetFilePath = settings.RuleSetFilePath;

            if (settings.AnalyzerAssemblyPaths != null)
            {
                this.AnalyzerFilePaths = settings.AnalyzerAssemblyPaths.Where(f => IsAssemblyLibraryFileName(f)).ToArray();
            }

            if (settings.AdditionalFilePaths != null)
            {
                this.AdditionalFiles = settings.AdditionalFilePaths.ToArray();

                HashSet<string> additionalFileNames = new HashSet<string>(
                    this.AdditionalFiles
                        .Select(af => GetFileName(af))
                        .Where(n => !string.IsNullOrEmpty(n)));

                this.AdditionalFilesToRemove = (this.OriginalAdditionalFiles ?? Enumerable.Empty<string>())
                    .Where(original => additionalFileNames.Contains(GetFileName(original)))
                    .ToArray();
            }
        }

        #endregion Overrides

        #region Private methods

        private static string GetFileName(string path)
        {
            try
            {
                return Path.GetFileName(path)?.ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Returns whether the supplied string is an assembly library (i.e. dll)
        /// </summary>
        private static bool IsAssemblyLibraryFileName(string filePath)
        {
            // Not expecting .winmd or .exe files to contain Roslyn analyzers
            // so we'll ignore them
            return filePath.EndsWith(DllExtension, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
