/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2020 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor
{
    /// <summary>
    /// Data class to hold validated command line arguments required by the pre-processor
    /// </summary>
    public class ProcessedArgs
    {
        private readonly string sonarQubeUrl;
        private readonly IAnalysisPropertyProvider globalFileProperties;

        public ProcessedArgs(string key, string name, string version, string organization, bool installLoaderTargets,
            IAnalysisPropertyProvider cmdLineProperties, IAnalysisPropertyProvider globalFileProperties,
            IAnalysisPropertyProvider scannerEnvProperties, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            ProjectKey = key;
            ProjectName = name;
            ProjectVersion = version;
            Organization = organization;
            
            CmdLineProperties = cmdLineProperties ?? throw new ArgumentNullException(nameof(cmdLineProperties));
            this.globalFileProperties = globalFileProperties ?? throw new ArgumentNullException(nameof(globalFileProperties));
            ScannerEnvProperties = scannerEnvProperties ?? throw new ArgumentNullException(nameof(scannerEnvProperties));
            InstallLoaderTargets = installLoaderTargets;

            if (Organization == null && this.globalFileProperties.TryGetValue(SonarProperties.Organization, out var filePropertiesOrganization))
            {
                logger.LogError(Resources.ERROR_Organization_Provided_In_SonarQubeAnalysis_file);
                IsOrganizationValid = false;
            }
            else
            {
                IsOrganizationValid = true;
            }

            AggregateProperties = new AggregatePropertiesProvider(cmdLineProperties, globalFileProperties, ScannerEnvProperties);
            if (!AggregateProperties.TryGetValue(SonarProperties.HostUrl, out this.sonarQubeUrl))
            {
                this.sonarQubeUrl = "http://localhost:9000";
            }
        }

        public bool IsOrganizationValid { get; set; }

        public string ProjectKey { get; }

        public string ProjectName { get; }

        public string ProjectVersion { get; }

        public string Organization { get; }

        public string SonarQubeUrl => this.sonarQubeUrl;

        /// <summary>
        /// If true the preprocessor should copy the loader targets to a user location where MSBuild will pick them up
        /// </summary>
        public bool InstallLoaderTargets { get; private set; }

        /// <summary>
        /// Returns the combined command line and file analysis settings
        /// </summary>
        public IAnalysisPropertyProvider AggregateProperties { get; }

        public IAnalysisPropertyProvider CmdLineProperties { get; }

        public IAnalysisPropertyProvider ScannerEnvProperties { get; }

        /// <summary>
        /// Returns the name of property settings file or null if there is not one
        /// </summary>
        public string PropertiesFileName
        {
            get
            {
                if (this.globalFileProperties is FilePropertyProvider fileProvider)
                {
                    Debug.Assert(fileProvider.PropertiesFile != null, "File properties should not be null");
                    Debug.Assert(!string.IsNullOrWhiteSpace(fileProvider.PropertiesFile.FilePath),
                        "Settings file name should not be null");
                    return fileProvider.PropertiesFile.FilePath;
                }
                return null;
            }
        }

        /// <summary>
        /// Returns the value for the specified setting.
        /// Throws if the setting does not exist.
        /// </summary>
        public string GetSetting(string key)
        {
            if (!AggregateProperties.TryGetValue(key, out var value))
            {
                var message = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.ERROR_MissingSetting, key);
                throw new InvalidOperationException(message);
            }
            return value;
        }

        /// <summary>
        /// Returns the value for the specified setting, or the supplied
        /// default if the setting does not exist
        /// </summary>
        public string GetSetting(string key, string defaultValue)
        {
            if (!AggregateProperties.TryGetValue(key, out var value))
            {
                value = defaultValue;
            }
            return value;
        }

        public bool TryGetSetting(string key, out string value)
        {
            return AggregateProperties.TryGetValue(key, out value);
        }

        public IEnumerable<Property> GetAllProperties()
        {
            return AggregateProperties.GetAllProperties();
        }
    }
}
