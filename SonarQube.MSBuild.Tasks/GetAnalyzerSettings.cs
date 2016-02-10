//-----------------------------------------------------------------------
// <copyright file="GetAnalyzerSettings.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SonarQube.Common;
using System;
using System.Linq;

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
        public string[] AdditionalFiles{ get; private set; }

        #endregion Input properties

        #region Overrides

        public override bool Execute()
        {
            bool taskSuccess = true;

            AnalysisConfig config = TaskUtilities.TryGetConfig(this.AnalysisConfigDir, new MSBuildLoggerAdapter(this.Log));
            
            if (config != null)
            {
                AnalyzerSettings settings = config.AnalyzerSettings;
                if (settings == null)
                {
                    this.Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_NotSpecifiedInConfig);
                }
                else
                {
                    this.RuleSetFilePath = settings.RuleSetFilePath;

                    if (settings.AnalyzerAssemblyPaths != null)
                    {
                        this.AnalyzerFilePaths = settings.AnalyzerAssemblyPaths.Where(f => IsAssemblyLibraryFileName(f)).ToArray();
                    }

                    if (settings.AdditionalFilePaths != null)
                    {
                        this.AdditionalFiles = settings.AdditionalFilePaths.ToArray();
                    }
                }
            }

            return !this.Log.HasLoggedErrors && taskSuccess;
        }

        #endregion Overrides

        #region Private methods

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
