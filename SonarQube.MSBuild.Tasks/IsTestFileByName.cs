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
    public sealed class IsTestFileByName : Task, SonarQube.Common.ILogger
    {
        /// <summary>
        /// Id of the SonarQube test setting that specifies the RegEx to use when determining
        /// if a project is a test project or not
        /// </summary>
        public const string TestRegExSettingId = "sonar.cs.msbuild.testProjectPattern";

        // Workaround for the file locking issue: retry after a short period.
        public const int MaxConfigRetryPeriodInMilliseconds = 2500; // Maximum time to spend trying to access the config file

        public const int DelayBetweenRetriesInMilliseconds = 499; // Period to wait between retries

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

            AnalysisConfig config = this.TryGetConfig();

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

        private AnalysisConfig TryGetConfig()
        {
            AnalysisConfig config = null;
            if (string.IsNullOrEmpty(this.AnalysisConfigDir)) // not specified
            {
                return null;
            }

            string fullAnalysisPath = Path.Combine(this.AnalysisConfigDir, FileConstants.ConfigFileName);
            this.Log.LogMessage(MessageImportance.Low, Resources.IsTest_ReadingConfigFile, fullAnalysisPath);
            if (!File.Exists(fullAnalysisPath))
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.IsTest_ConfigFileNotFound);
                return null;
            }

            bool succeeded = Utilities.Retry(MaxConfigRetryPeriodInMilliseconds, DelayBetweenRetriesInMilliseconds, (SonarQube.Common.ILogger)this, () => DoLoadConfig(fullAnalysisPath, out config));
            if (succeeded)
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.IsTest_ReadingConfigSucceeded, fullAnalysisPath);
            }
            else
            {
                this.Log.LogError(Resources.IsTest_ReadingConfigFailed, fullAnalysisPath);
            }
            return config;
        }

        /// <summary>
        /// Attempts to load the config file, suppressing any IO errors that occur.
        /// This method is expected to be called inside a "retry"
        /// </summary>
        private bool DoLoadConfig(string filePath, out AnalysisConfig config)
        {
            Debug.Assert(File.Exists(filePath), "Expecting the config file to exist: " + filePath);
            config = null;

            try
            {
                config = AnalysisConfig.Load(filePath);
            }
            catch (IOException e)
            {
                // Log this as a message for info. We'll log an error if all of the re-tries failed
                this.Log.LogMessage(MessageImportance.Low, Resources.IsTest_ErrorReadingConfigFile, e.Message);
                return false;
            }
            return true;
        }

        private void LogMessage(Common.LoggerVerbosity verbosity, string message, params object[] args)
        {
            // We need to adapt between the ILogger verbosity and the MsBuild logger verbosity
            if (verbosity == Common.LoggerVerbosity.Info)
            {
                this.Log.LogMessage(MessageImportance.Normal, message, args);
            }
            else
            {
                this.Log.LogMessage(MessageImportance.Low, message, args);
            }
        }

        #endregion Private methods

        #region ILogger interface

        void Common.ILogger.LogWarning(string message, params object[] args)
        {
            this.Log.LogWarning(message, args);
        }

        void Common.ILogger.LogError(string message, params object[] args)
        {
            this.Log.LogError(message, args);
        }

        public void LogDebug(string message, params object[] args)
        {
            LogMessage(Common.LoggerVerbosity.Debug, message, args);
        }

        void Common.ILogger.LogInfo(string message, params object[] args)
        {
            LogMessage(Common.LoggerVerbosity.Info, message, args);
        }

        Common.LoggerVerbosity Common.ILogger.Verbosity
        {
            get; set;
        }

        bool Common.ILogger.IncludeTimestamp
        {
            get; set;
        }

        #endregion ILogger interface
    }
}