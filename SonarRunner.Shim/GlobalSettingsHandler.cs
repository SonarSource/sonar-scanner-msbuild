//-----------------------------------------------------------------------
// <copyright file="GlobalSettingsHandler.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace SonarRunner.Shim
{
    /*  Global analysis settings
        ------------------------
        The analysis config file can contain additional settings downloaded from the SonarQube server.
        Also, each ProjectInfo project file can contain global settings that were set in the MSBuild project.

        It's possible that the same property could be set in multiple places so we need to handle
        possible duplicates. The following simple rules are used:
        
        * attempting to set one of the core analysis settings other than via the appropriate
          AnalysisConfig property will generate a warning and additional value will be ignored.
        
        * if any non-core property is set multiple times, the outcome depeneds on whether the
          value is the same or not:
          - if the value is the same in all cases, we'll use the value and log an info message
            saying it is defined in multiple places
          - if the value is not the same in all cases, we'll issue a warning and ignore the setting
            (the order in which we process the ProcessInfo files is arbitrary, so we don't have
             any information about the build order that we could use to determine which is the first/
             last value set).

        The info/warning messages will be like the following:
            "Analysis setting 'abc' is assigned the same value in multiple places. Locations: c:\project1.proj, c:\project2.proj"
            "Analysis setting 'xyz" is assigned different values and will be ignored. Locations:  c:\project1.proj, c:\project2.proj"
    */

    public static class GlobalSettingsHandler
    {
        /// <summary>
        /// Regular expression to validate setting ids.
        /// </summary>
        /// <remarks>
        /// Validation rules:
        /// Must start with an alpanumeric character.
        /// Can be followed by any number of alphanumeric characters or .
        /// Whitespace is not allowed
        /// </remarks>
        private static readonly Regex ValidSettingKeyRegEx = new Regex(@"^\w[\w\d\.-]*$", RegexOptions.Compiled);

        /// <summary>
        /// Core properties that can only be specified by setting the appropriate property in the AnalysisConfig file
        /// </summary>
        private static readonly string[] CorePropertyKeys = new string[] {
            SonarProperties.ProjectKey,
            SonarProperties.ProjectName,
            SonarProperties.ProjectVersion,
            SonarProperties.ProjectBaseDir };

        private class SettingWithSource
        {
            public AnalysisSetting Setting { get; set; }
            public string Source { get; set; }
        }

        #region Public methods

        /// <summary>
        /// Collects and return the valid global settings specified in the config and project info instances.
        /// Warnings will be logged about invalid settings.
        /// </summary>
        /// <remarks>Some core settings cannot be set as additional properties i.e. those settings which have
        /// explicit properties on <see cref="AnalysisConfig"/> such as project key and project name.
        /// The core settings will not be included in the list of returned settings.</remarks>
        public static IEnumerable<AnalysisSetting> GetGlobalSettings(AnalysisConfig config, IEnumerable<ProjectInfo> validProjectInfos, ILogger logger)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (validProjectInfos == null)
            {
                throw new ArgumentNullException("validProjectInfos");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            IList<AnalysisSetting> validSettings = new List<AnalysisSetting>();

            // Gather any additional settings from the config and project info files
            // We're preserving the location the settings came from to improve error reporting.
            IList<SettingWithSource> allSettingsWithSource = new List<SettingWithSource>();
            string configSource = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.AnalysisConfigFileDescription, config.SonarConfigDir);
            AddSettingsWithSource(allSettingsWithSource, config.AdditionalSettings, configSource);
            foreach (ProjectInfo projectInfo in validProjectInfos)
            {
                AddSettingsWithSource(allSettingsWithSource, projectInfo.GlobalAnalysisSettings, projectInfo.FullPath);
            }

            // De-duplicate and validate
            foreach (string key in allSettingsWithSource.Select(s => s.Setting.Id).Distinct(AnalysisSetting.SettingKeyComparer))
            {
                ProcessKey(key, allSettingsWithSource, validSettings, logger);
            }
            
            return validSettings;
        }

        #endregion

        #region Private methods

        private static void AddSettingsWithSource(IList<SettingWithSource> allSettings, IEnumerable<AnalysisSetting> settings, string source)
        {
            if (settings != null)
            {
                foreach (AnalysisSetting setting in settings)
                {
                    SettingWithSource info = new SettingWithSource()
                    {
                        Setting = setting,
                        Source = source
                    };

                    allSettings.Add(info);
                }
            }
        }

        /// <summary>
        /// Process all settings for the supplied key, logging info and warnings as appropriate.
        /// If the setting for the key is valid it will be added to the list of valid settings.
        /// </summary>
        private static void ProcessKey(string key, IEnumerable<SettingWithSource> allSettings, IList<AnalysisSetting> validSettings, ILogger logger)
        {
            IEnumerable<SettingWithSource> matchingSettings = allSettings.Where(s => AnalysisSetting.SettingKeyComparer.Equals(s.Setting.Id, key));
            Debug.Assert(matchingSettings.Count() > 0, "At least one setting should exist for the key. Key: " + key);

            AnalysisSetting settingToAdd = matchingSettings.First().Setting;

            if (matchingSettings.Count() != 1)
            {
                // Duplicate settings are ok as long as all of the values are the same
                bool allSameValue = matchingSettings.All(s => AnalysisSetting.SettingValueComparer.Equals(s.Setting.Value, settingToAdd.Value));
                if (allSameValue)
                {
                    logger.LogMessage(Resources.INFO_DuplicateSettingWithSameValue, key, GetLocations(matchingSettings));
                }
                else
                {
                    logger.LogWarning(Resources.WARN_DuplicateSettingWithDifferentValue, key, GetLocations(matchingSettings));
                    settingToAdd = null;
                }
            }

            if (settingToAdd != null)
            {
                if (IsCoreSetting(settingToAdd))
                {
                    logger.LogWarning(Resources.WARN_CoreSettingCannotBeSet, key, GetLocations(matchingSettings));
                }
                else if (AnalysisSetting.IsValidKey(key))
                {
                    validSettings.Add(settingToAdd);
                }
                else
                {
                    logger.LogWarning(Resources.WARN_InvalidSettingKey, settingToAdd.Id, matchingSettings.Single().Source);
                }
            }
        }
        
        private static string GetLocations(IEnumerable<SettingWithSource> settings)
        {
            return string.Join(", ", settings.Select(s => s.Source));
        }

        private static void AddSetting(IList<AnalysisSetting> settings, string key, string value)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(key), "Settings key should not be null/empty");
            Debug.Assert(!settings.Any(s => s.Id.Equals(key, StringComparison.OrdinalIgnoreCase)), "Setting with this key already exists. Key: " + key);
            AnalysisSetting setting = new AnalysisSetting() { Id = key, Value = value };
            settings.Add(setting);
        }

        private static bool IsCoreSetting(AnalysisSetting setting)
        {
            return CorePropertyKeys.Any(p => AnalysisSetting.SettingKeyComparer.Equals(p, setting.Id));
        }
        
        #endregion
    }
}
