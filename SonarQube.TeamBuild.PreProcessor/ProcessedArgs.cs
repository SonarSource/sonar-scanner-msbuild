/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
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
 
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Data class to hold validated command line arguments required by the pre-processor
    /// </summary>
    public class ProcessedArgs
    {
        private readonly string sonarQubeUrl;
        private readonly string projectKey;
        private readonly string projectName;
        private readonly string projectVersion;
        private readonly string organization;

        private readonly IAnalysisPropertyProvider cmdLineProperties;
        private readonly IAnalysisPropertyProvider globalFileProperties;
        private readonly IAnalysisPropertyProvider scannerEnvProperties;
        private readonly IAnalysisPropertyProvider aggProperties;

        public ProcessedArgs(string key, string name, string version, string organization, bool installLoaderTargets, IAnalysisPropertyProvider cmdLineProperties,
            IAnalysisPropertyProvider globalFileProperties, IAnalysisPropertyProvider scannerEnvProperties)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException("key");
            }

            this.projectKey = key;
            this.projectName = name;
            this.projectVersion = version;
            this.organization = organization;

            this.cmdLineProperties = cmdLineProperties ?? throw new ArgumentNullException("cmdLineProperties");
            this.globalFileProperties = globalFileProperties ?? throw new ArgumentNullException("globalFileProperties");
            this.scannerEnvProperties = scannerEnvProperties ?? throw new ArgumentNullException("scannerEnvProperties");
            this.InstallLoaderTargets = installLoaderTargets;

            this.aggProperties = new AggregatePropertiesProvider(cmdLineProperties, globalFileProperties, scannerEnvProperties);
            if (!aggProperties.TryGetValue(SonarProperties.HostUrl, out this.sonarQubeUrl))
            {
                this.sonarQubeUrl = "http://localhost:9000";
            }
        }

        public string ProjectKey { get { return this.projectKey; } }

        public string ProjectName { get { return this.projectName; } }

        public string ProjectVersion { get { return this.projectVersion; } }

        public string Organization { get { return this.organization; } }

        public string SonarQubeUrl { get { return this.sonarQubeUrl; } }

        /// <summary>
        /// If true the preprocessor should copy the loader targets to a user location where MSBuild will pick them up
        /// </summary>
        public bool InstallLoaderTargets { get; private set; }

        /// <summary>
        /// Returns the combined command line and file analysis settings
        /// </summary>
        public IAnalysisPropertyProvider AggregateProperties { get { return this.aggProperties; } }

        public IAnalysisPropertyProvider CmdLineProperties { get { return this.cmdLineProperties; } }

        public IAnalysisPropertyProvider ScannerEnvProperties { get { return this.scannerEnvProperties; } }

        /// <summary>
        /// Returns the name of property settings file or null if there is not one
        /// </summary>
        public string PropertiesFileName
        {
            get
            {
                FilePropertyProvider fileProvider = this.globalFileProperties as FilePropertyProvider;
                if (fileProvider != null)
                {
                    Debug.Assert(fileProvider.PropertiesFile != null, "File properties should not be null");
                    Debug.Assert(!string.IsNullOrWhiteSpace(fileProvider.PropertiesFile.FilePath), "Settings file name should not be null");
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
            if (!this.aggProperties.TryGetValue(key, out string value))
            {
                string message = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.ERROR_MissingSetting, key);
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
            if (!this.aggProperties.TryGetValue(key, out string value))
            {
                value = defaultValue;
            }
            return value;
        }

        public bool TryGetSetting(string key, out string value)
        {
            return this.aggProperties.TryGetValue(key, out value);
        }

        public IEnumerable<Property> GetAllProperties()
        {
            return this.aggProperties.GetAllProperties();
        }
    }
}
