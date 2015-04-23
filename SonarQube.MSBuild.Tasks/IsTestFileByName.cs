//-----------------------------------------------------------------------
// <copyright file="IsTestFileByName.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

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
    public class IsTestFileByName : Task
    {
        /// <summary>
        /// Id of the SonarQube test setting that specifies the RegEx to use when determining
        /// if a project is a test project or not
        /// </summary>
        public const string TestRegExSettingId = "sonar.cs.msbuild.testProjectPattern";

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
        
        #endregion

        #region Overrides

        public override bool Execute()
        {
            bool taskSuccess = true;
            string regEx = GetRegularExpression();

            try
            {
                this.IsTest = !string.IsNullOrEmpty(regEx) && Regex.IsMatch(this.FullFilePath, regEx, RegexOptions.IgnoreCase);
            }
            catch(ArgumentException ex) // thrown for invalid regular expressions
            {
                taskSuccess = false;
                this.Log.LogError(Resources.IsTest_InvalidRegularExpression, regEx, ex.Message, TestRegExSettingId);
            }
            
            return taskSuccess;
        }

        #endregion


        #region Private methods

        private string GetRegularExpression()
        {
            string regEx = TryGetRegExFromConfig();
            if (!string.IsNullOrWhiteSpace(regEx))
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.IsTest_UsingRegExFromConfig, regEx);
            }

            return regEx;
        }
        
        /// <summary>
        /// Attempts to locate and return the regular expression to use from the analysis config file.
        /// Returns null if the config file could not be found or if it does not contain the setting.
        /// </summary>
        private string TryGetRegExFromConfig()
        {
            if (string.IsNullOrEmpty(this.AnalysisConfigDir)) // not specified
            {
                return null;
            }

            string fullAnalysisPath = Path.Combine(this.AnalysisConfigDir, FileConstants.ConfigFileName);
            if (!File.Exists(fullAnalysisPath))
            {
                return null;
            }

            AnalysisConfig config = AnalysisConfig.Load(fullAnalysisPath);
            AnalysisSetting setting;
            string regEx = null;
            if (config.TryGetSetting(TestRegExSettingId, out setting))
            {
                regEx = setting.Value;
            }

            return regEx;
        }

        #endregion

    }
}
