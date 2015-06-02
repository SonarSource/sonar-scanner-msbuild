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
        
        * values set in a project info file override those set in the analysis config file

        * if any non-core property is set multiple times, the outcome depends on whether the
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

            IEnumerable<SettingWithSource> validProjectSettings = GetValidProjectSettings(validProjectInfos, logger);

            IEnumerable<SettingWithSource> validConfigSettings = GetValidConfigSettings(config, logger);

            IEnumerable<AnalysisSetting> combined = CombineSettings(validConfigSettings, validProjectSettings, logger);

            return combined;
        }

        #endregion

        #region Private methods

        private static IEnumerable<SettingWithSource> GetValidProjectSettings(IEnumerable<ProjectInfo> projectInfos, ILogger logger)
        {
            // Gather all additional settings from the project info files
            // We're preserving the location the settings came from to improve error reporting.
            IList<SettingWithSource> projectSettingsWithSource = new List<SettingWithSource>();
            foreach (ProjectInfo projectInfo in projectInfos)
            {
                AddSettingsWithSource(projectSettingsWithSource, projectInfo.GlobalAnalysisSettings, projectInfo.FullPath);
            }

            IList<SettingWithSource> validSettings = GetValidSettings(projectSettingsWithSource, logger);
            return validSettings;
        }

        private static IEnumerable<SettingWithSource> GetValidConfigSettings(AnalysisConfig config, ILogger logger)
        {
            string source = System.IO.Path.Combine(config.SonarConfigDir ?? string.Empty, FileConstants.ConfigFileName);
            IList<SettingWithSource> configSettingsWithSource = new List<SettingWithSource>();
            AddSettingsWithSource(configSettingsWithSource, config.AdditionalSettings, source);

            IList<SettingWithSource> validSettings = GetValidSettings(configSettingsWithSource, logger);
            return validSettings;
        }

        private static void AddSettingsWithSource(IList<SettingWithSource> targetList, IEnumerable<AnalysisSetting> settingsToAdd, string source)
        {
            Debug.Assert(targetList != null, "Supplied target list should not be null");
            Debug.Assert(source != null, "Supplied source should not be null");

            if (settingsToAdd != null)
            {
                foreach (AnalysisSetting setting in settingsToAdd)
                {
                    SettingWithSource info = new SettingWithSource()
                    {
                        Setting = setting,
                        Source = source
                    };

                    targetList.Add(info);
                }
            }
        }

        /// <summary>
        /// Returns the set of valid settings from the supplied list.
        /// A valid setting has a valid key, and either
        /// 1) is not defined multiple times, or 
        /// 2) is defined multiple times but all definitions have the same value
        /// Messages and warnings will be logged as appropriate.
        /// </summary>
        private static IList<SettingWithSource> GetValidSettings(IEnumerable<SettingWithSource> candidates, ILogger logger)
        {
            IList<SettingWithSource> validSettings = new List<SettingWithSource>();
            foreach (string key in candidates.Select(s => s.Setting.Id).Distinct(AnalysisSetting.SettingKeyComparer))
            {
                ProcessKey(key, candidates, validSettings, logger);
            }

            return validSettings;
        }

        /// <summary>
        /// Process all settings for the supplied key, logging info and warnings as appropriate.
        /// If the setting for the key is valid it will be added to the list of valid settings.
        /// </summary>
        private static void ProcessKey(string key, IEnumerable<SettingWithSource> projectSettings, IList<SettingWithSource> validSettings, ILogger logger)
        {
            IEnumerable<SettingWithSource> matchingSettings = projectSettings.Where(s => AnalysisSetting.SettingKeyComparer.Equals(s.Setting.Id, key));
            Debug.Assert(matchingSettings.Count() > 0, "At least one setting should exist for the key. Key: " + key);

            SettingWithSource settingToAdd = matchingSettings.First();

            if (matchingSettings.Count() != 1)
            {
                // Duplicate settings are ok as long as all of the values are the same
                bool allSameValue = matchingSettings.All(s => AnalysisSetting.SettingValueComparer.Equals(s.Setting.Value, settingToAdd.Setting.Value));
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

            AddSettingIfValid(settingToAdd, validSettings, logger);
        }

        private static void AddSettingIfValid(SettingWithSource candidate, IList<SettingWithSource> validSettings, ILogger logger)
        {
            if (candidate != null)
            {
                if (IsCoreSetting(candidate.Setting))
                {
                    logger.LogWarning(Resources.WARN_CoreSettingCannotBeSet, candidate.Setting.Id, candidate.Source);
                }
                else if (AnalysisSetting.IsValidKey(candidate.Setting.Id))
                {
                    validSettings.Add(candidate);
                }
                else
                {
                    logger.LogWarning(Resources.WARN_InvalidSettingKey, candidate.Setting.Id, candidate.Source);
                }
            }
        }

        private static string GetLocations(IEnumerable<SettingWithSource> settings)
        {
            return string.Join(", ", settings.Select(s => s.Source));
        }
        
        private static bool IsCoreSetting(AnalysisSetting setting)
        {
            return CorePropertyKeys.Any(p => AnalysisSetting.SettingKeyComparer.Equals(p, setting.Id));
        }

        private static IEnumerable<AnalysisSetting> CombineSettings(IEnumerable<SettingWithSource> configSettings, IEnumerable<SettingWithSource> projectSettings, ILogger logger)
        {
            List<AnalysisSetting> combinedSettings = new List<AnalysisSetting>(projectSettings.Select(s => s.Setting));

            foreach (SettingWithSource configSetting in configSettings)
            {
                SettingWithSource projectSetting = TryGetMatchingSetting(configSetting, projectSettings);

                if (projectSetting != null)
                {
                    logger.LogMessage(Resources.INFO_ConfigSettingOverridden, projectSetting.Setting.Id, configSetting.Source, projectSetting.Source);
                }
                else
                {
                    combinedSettings.Add(configSetting.Setting);
                }
            }

            return combinedSettings;
        }

        private static SettingWithSource TryGetMatchingSetting(SettingWithSource candidate, IEnumerable<SettingWithSource> allSettings)
        {
            return allSettings.SingleOrDefault(s => AnalysisSetting.SettingKeyComparer.Equals(candidate.Setting.Id, s.Setting.Id));
        }

        #endregion
    }
}
