//-----------------------------------------------------------------------
// <copyright file="ConfigSettingsExtensions.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;

namespace SonarQube.Common
{
    /// <summary>
    /// Extension methods for <see cref="AnalysisConfig"/>
    /// </summary>
    public static class ConfigSettingsExtensions
    {
        #region Public methods

        /// <summary>
        /// Returns the value of the specified setting, or the supplied default value if the setting could not be found
        /// </summary>
        public static string GetSetting(this AnalysisConfig config, string settingId, string defaultValue)
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

            AnalysisSetting setting;
            if (config.TryGetSetting(settingId, out setting))
            {
                result = setting.Value;
            }

            return result;
        }

        /// <summary>
        /// Attempts to find and return the analysis setting with the specified id
        /// </summary>
        /// <returns>True if the setting was found, otherwise false</returns>
        public static bool TryGetSetting(this AnalysisConfig config, string settingId, out AnalysisSetting result)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (string.IsNullOrWhiteSpace(settingId))
            {
                throw new ArgumentNullException("settingId");
            } 

            result = null;

            if (config.AdditionalSettings != null)
            {
                result = config.AdditionalSettings.FirstOrDefault(ar => settingId.Equals(ar.Id, StringComparison.Ordinal));
            }
            return result != null;
        }

        /// <summary>
        /// Sets the value of the additional setting. The setting will be added if it does not already exist.
        /// </summary>
        public static void SetValue(this AnalysisConfig config, string settingId, string value)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }            
            if (string.IsNullOrWhiteSpace(settingId))
            {
                throw new ArgumentNullException("settingId");
            }

            AnalysisSetting setting;
            if (config.TryGetSetting(settingId, out setting))
            {
                setting.Value = value;
            }
            else
            {
                setting = new AnalysisSetting()
                {
                    Id = settingId,
                    Value = value
                };
            }

            if (config.AdditionalSettings == null)
            {
                config.AdditionalSettings = new System.Collections.Generic.List<AnalysisSetting>();
            }
            config.AdditionalSettings.Add(setting);
        }

        #endregion
    }
}
