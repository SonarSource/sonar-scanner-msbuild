//-----------------------------------------------------------------------
// <copyright file="ConfigSettingsExtensions.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SonarQube.Common
{
    /// <summary>
    /// Extension methods for <see cref="AnalysisConfig"/>
    /// </summary>
    public static class ConfigSettingsExtensions
    {

        /// <summary>
        /// The key for the setting that holds the path to a settings file
        /// </summary>
        private const string SettingsFileKey = "settings.file.path";

        #region Public methods

        /// <summary>
        /// Returns the value of the specified config setting, or the supplied default value if the setting could not be found
        /// </summary>
        public static string GetConfigValue(this AnalysisConfig config, string settingId, string defaultValue)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (string.IsNullOrWhiteSpace(settingId))
            {
                throw new ArgumentNullException("settingId");
            }

            string result = defaultValue;

            ConfigSetting setting;
            if (config.TryGetConfigSetting(settingId, out setting))
            {
                result = setting.Value;
            }

            return result;
        }

        /// <summary>
        /// Sets the value of the additional setting. The setting will be added if it does not already exist.
        /// </summary>
        public static void SetConfigValue(this AnalysisConfig config, string settingId, string value)
        {
            SetValue(config, settingId, value);
        }

        /// <summary>
        /// Returns a provider containing all of the analysis settings from the config.
        /// Optionally includes settings downloaded from the SonarQube server.
        /// </summary>
        /// <remarks>This could include settings imported from a settings file</remarks>
        public static IAnalysisPropertyProvider GetAnalysisSettings(this AnalysisConfig config, bool includeServerSettings)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            List<IAnalysisPropertyProvider> providers = new List<IAnalysisPropertyProvider>();

            // Note: the order in which the providers are added determines the precedence
            // Add local settings
            if (config.LocalSettings != null)
            {
                providers.Add(new ListPropertiesProvider(config.LocalSettings));
            }
            
            // Add file settings
            string settingsFilePath = config.GetSettingsFilePath();
            if (settingsFilePath != null)
            {
                ListPropertiesProvider fileProvider = new ListPropertiesProvider(AnalysisProperties.Load(settingsFilePath));
                providers.Add(fileProvider);
            }

            // Add server settings
            if (includeServerSettings && config.ServerSettings != null)
            {
                providers.Add(new ListPropertiesProvider(config.ServerSettings));
            }

            IAnalysisPropertyProvider provider = null;
            switch(providers.Count)
            {
                case 0:
                    provider = EmptyPropertyProvider.Instance;
                    break;
                case 1:
                    provider = providers[0];
                    break;
                default:
                    provider = new AggregatePropertiesProvider(providers.ToArray());
                    break;
            }

            return provider;
        }

        public static void SetSettingsFilePath(this AnalysisConfig config, string fileName)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }
            config.SetValue(SettingsFileKey, fileName);
        }

        public static string GetSettingsFilePath(this AnalysisConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            ConfigSetting setting;
            if (config.TryGetConfigSetting(SettingsFileKey, out setting))
            {
                return setting.Value;
            }
            return null;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Attempts to find and return the config setting with the specified id
        /// </summary>
        /// <returns>True if the setting was found, otherwise false</returns>
        private static bool TryGetConfigSetting(this AnalysisConfig config, string settingId, out ConfigSetting result)
        {
            Debug.Assert(config != null, "Supplied config should not be null");
            Debug.Assert(!string.IsNullOrWhiteSpace(settingId), "Setting id should not be null/empty");

            result = null;

            if (config.AdditionalConfig != null)
            {
                result = config.AdditionalConfig.FirstOrDefault(ar => ConfigSetting.SettingKeyComparer.Equals(settingId, ar.Id));
            }
            return result != null;
        }

        /// <summary>
        /// Sets the value of the additional setting. The setting will be added if it does not already exist.
        /// </summary>
        private static void SetValue(this AnalysisConfig config, string settingId, string value)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (string.IsNullOrWhiteSpace(settingId))
            {
                throw new ArgumentNullException("settingId");
            }

            ConfigSetting setting;
            if (config.TryGetConfigSetting(settingId, out setting))
            {
                setting.Value = value;
            }
            else
            {
                setting = new ConfigSetting()
                {
                    Id = settingId,
                    Value = value
                };
            }

            if (config.AdditionalConfig == null)
            {
                config.AdditionalConfig = new System.Collections.Generic.List<ConfigSetting>();
            }
            config.AdditionalConfig.Add(setting);
        }

        #endregion
    }
}
