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
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor
{
    /// <summary>
    /// Data class to hold validated command line arguments required by the pre-processor.
    /// </summary>
    public class ProcessedArgs
    {
        private readonly SonarServer sonarServer;

        private readonly IAnalysisPropertyProvider globalFileProperties;

        public bool IsOrganizationValid { get; set; }

        public /* for testing */ virtual string ProjectKey { get; }

        public string ProjectName { get; }

        public string ProjectVersion { get; }

        public TimeSpan HttpTimeout { get; }

        public /* for testing */ virtual string Organization { get; }

        /// <summary>
        /// Returns either a <see cref="SonarQubeServer"/>, <see cref="SonarCloudServer"/>, or <see langword="null"/> depending on
        /// the sonar.host and sonar.scanner.sonarcloudUrl settings.
        /// </summary>
        public SonarServer SonarServer => this.sonarServer;

        /// <summary>
        /// Api v2 endpoint. Either https://api.sonarcloud.io for SonarCloud or http://host/api/v2 for SonarQube.
        /// </summary>
        public string ApiBaseUrl { get; }

        /// <summary>
        /// If true the preprocessor should copy the loader targets to a user location where MSBuild will pick them up.
        /// </summary>
        public bool InstallLoaderTargets { get; private set; }

        /// <summary>
        /// Returns the combined command line and file analysis settings.
        /// </summary>
        public IAnalysisPropertyProvider AggregateProperties { get; }

        public IAnalysisPropertyProvider CmdLineProperties { get; }

        public IAnalysisPropertyProvider ScannerEnvProperties { get; }

        /// <summary>
        /// Returns the name of property settings file or null if there is not one.
        /// </summary>
        public string PropertiesFileName
        {
            get
            {
                if (globalFileProperties is FilePropertyProvider fileProvider)
                {
                    Debug.Assert(fileProvider.PropertiesFile != null, "File properties should not be null");
                    Debug.Assert(!string.IsNullOrWhiteSpace(fileProvider.PropertiesFile.FilePath),
                        "Settings file name should not be null");
                    return fileProvider.PropertiesFile.FilePath;
                }
                return null;
            }
        }

        public ProcessedArgs(
            string key,
            string name,
            string version,
            string organization,
            bool installLoaderTargets,
            IAnalysisPropertyProvider cmdLineProperties,
            IAnalysisPropertyProvider globalFileProperties,
            IAnalysisPropertyProvider scannerEnvProperties,
            ILogger logger)
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
            var isHostSet = AggregateProperties.TryGetValue(SonarProperties.HostUrl, out var sonarHostUrl); // Used for SQ and may also be set to https://SonarCloud.io
            var isSonarcloudSet = AggregateProperties.TryGetValue(SonarProperties.SonarcloudUrl, out var sonarcloudUrl);
            this.sonarServer = GetSonarServer(logger, isHostSet, sonarHostUrl, isSonarcloudSet, sonarcloudUrl);
            ApiBaseUrl = AggregateProperties.TryGetProperty(SonarProperties.ApiBaseUrl, out var apiBaseUrl)
                ? apiBaseUrl.Value
                : SonarServer switch
                {
                    SonarCloudServer => "https://api.sonarcloud.io", // SQ default
                    SonarQubeServer { ServerUrl: { } baseUrl } => $"{baseUrl.TrimEnd('/')}/api/v2", // SQ default
                    _ => null,
                };
            HttpTimeout = TimeoutProvider.HttpTimeout(AggregateProperties, logger);
        }

        protected /* for testing */ ProcessedArgs() { }

        /// <summary>
        /// Returns the value for the specified setting.
        /// Throws if the setting does not exist.
        /// </summary>
        public string GetSetting(string key)
        {
            if (AggregateProperties.TryGetValue(key, out var value))
            {
                return value;
            }

            var message = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.ERROR_MissingSetting, key);
            throw new InvalidOperationException(message);
        }

        /// <summary>
        /// Returns the value for the specified setting, or the supplied
        /// default if the setting does not exist.
        /// </summary>
        public string GetSetting(string key, string defaultValue)
        {
            if (!AggregateProperties.TryGetValue(key, out var value))
            {
                value = defaultValue;
            }
            return value;
        }

        public /* for testing */ virtual bool TryGetSetting(string key, out string value) =>
            AggregateProperties.TryGetValue(key, out value);

        public IEnumerable<Property> AllProperties() =>
            AggregateProperties.GetAllProperties();

        // see spec in https://xtranet-sonarsource.atlassian.net/wiki/spaces/LANG/pages/3155001395/Scanner+Bootstrappers+implementation+guidelines
        private static SonarServer GetSonarServer(ILogger logger, bool isHostSet, string sonarHostUrl, bool isSonarcloudSet, string sonarcloudUrl)
        {
            const string defaultSonarCloud = "https://sonarcloud.io";
            return new { isHostSet, isSonarcloudSet } switch
            {
                { isHostSet: true, isSonarcloudSet: true } when sonarHostUrl != sonarcloudUrl => Error(Resources.ERR_HostUrlDiffersFromSonarcloudUrl),
                { isHostSet: true, isSonarcloudSet: true } when string.IsNullOrWhiteSpace(sonarcloudUrl) => Error(Resources.ERR_HostUrlAndSonarcloudUrlAreEmpty),
                { isHostSet: true, isSonarcloudSet: true } => Warn(new SonarCloudServer(sonarcloudUrl), Resources.WARN_HostUrlAndSonarcloudUrlSet),
                { isHostSet: false, isSonarcloudSet: false } => new SonarCloudServer(defaultSonarCloud),
                { isHostSet: true, isSonarcloudSet: false } => sonarHostUrl == defaultSonarCloud
                    ? new SonarCloudServer(defaultSonarCloud)
                    : new SonarQubeServer(sonarHostUrl),
                { isHostSet: false, isSonarcloudSet: true } => new SonarCloudServer(sonarcloudUrl),
            };

            SonarServer Error(string message)
            {
                logger.LogError(message);
                return null;
            }

            SonarServer Warn(SonarServer server, string message)
            {
                logger.LogWarning(message);
                return server;
            }
        }
    }
}
