/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
 * mailto: info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SonarScanner.MSBuild.Common;

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
            throw new ArgumentNullException(nameof(config));
        }
        if (string.IsNullOrWhiteSpace(settingId))
        {
            throw new ArgumentNullException(nameof(settingId));
        }

        var result = defaultValue;

        if (config.TryGetConfigSetting(settingId, out var setting))
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
    /// Returns a provider containing the analysis settings coming from all providers (analysis config file, environment, settings file).
    /// Optionally includes settings downloaded from the SonarQube server.
    /// </summary>
    /// <remarks>This could include settings imported from a settings file</remarks>
    public static IAnalysisPropertyProvider GetAnalysisSettings(this AnalysisConfig config, bool includeServerSettings, ILogger logger)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        var providers = new List<IAnalysisPropertyProvider>();

        // Note: the order in which the providers are added determines the precedence
        // Add local settings
        if (config.LocalSettings != null)
        {
            providers.Add(new ListPropertiesProvider(config.LocalSettings));
        }

        // Add file settings
        var settingsFilePath = config.GetSettingsFilePath();
        if (settingsFilePath != null)
        {
            var fileProvider = new ListPropertiesProvider(AnalysisProperties.Load(settingsFilePath));
            providers.Add(fileProvider);
        }

        // Add scanner environment settings
        if (EnvScannerPropertiesProvider.TryCreateProvider(logger, out var envProvider))
        {
            providers.Add(envProvider);
        }

        // Add server settings
        if (includeServerSettings && config.ServerSettings != null)
        {
            providers.Add(new ListPropertiesProvider(config.ServerSettings));
        }

        IAnalysisPropertyProvider provider;
        switch (providers.Count)
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
            throw new ArgumentNullException(nameof(config));
        }
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }
        config.SetValue(SettingsFileKey, fileName);
    }

    public static string GetSettingsFilePath(this AnalysisConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (config.TryGetConfigSetting(SettingsFileKey, out var setting))
        {
            return setting.Value;
        }
        return null;
    }

    public static Version FindServerVersion(this AnalysisConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        Version.TryParse(config.SonarQubeVersion, out var version);
        return version;
    }

    public static string GetSettingOrDefault(this AnalysisConfig config, string settingName, bool includeServerSettings, string defaultValue, ILogger logger)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }
        if (settingName == null)
        {
            throw new ArgumentNullException(nameof(settingName));
        }
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        if (config.GetAnalysisSettings(includeServerSettings, logger).TryGetValue(settingName, out var value))
        {
            return value;
        }
        return defaultValue;
    }

    #endregion Public methods

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
            throw new ArgumentNullException(nameof(config));
        }
        if (string.IsNullOrWhiteSpace(settingId))
        {
            throw new ArgumentNullException(nameof(settingId));
        }

        if (config.TryGetConfigSetting(settingId, out var setting))
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

    #endregion Private methods
}
