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
using System.Text.RegularExpressions;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor
{
    /// <summary>
    /// Data class to hold validated command line arguments required by the pre-processor.
    /// </summary>
    public class ProcessedArgs
    {
        /// <summary>
        /// Regular expression to validate a project key.
        /// See http://docs.sonarqube.org/display/SONAR/Project+Administration#ProjectAdministration-AddingaProject
        /// </summary>
        /// <remarks>Should match the java regex here: https://github.com/SonarSource/sonarqube/blob/5.1.1/sonar-core/src/main/java/org/sonar/core/component/ComponentKeys.java#L36
        /// "Allowed characters are alphanumeric, '-', '_', '.' and ':', with at least one non-digit".
        /// </remarks>
        private static readonly Regex ProjectKeyRegEx = new(@"^[a-zA-Z0-9:\-_\.]*[a-zA-Z:\-_\.]+[a-zA-Z0-9:\-_\.]*$", RegexOptions.Compiled | RegexOptions.Singleline, RegexConstants.DefaultTimeout);

        private readonly IAnalysisPropertyProvider globalFileProperties;
        private readonly IOperatingSystemProvider operatingSystemProvider;

        public /* for testing */ virtual string ProjectKey { get; }

        public string ProjectName { get; }

        public string ProjectVersion { get; }

        public TimeSpan HttpTimeout { get; }

        public /* for testing */ virtual string Organization { get; }

        public SonarServer SonarServer { get; }

        /// <summary>
        /// Returns the operating system used to run the scanner.
        /// Supported values are windows|linux|macos|alpine but more can be added later
        /// https://xtranet-sonarsource.atlassian.net/wiki/spaces/LANG/pages/3155001395/Scanner+Bootstrappers+implementation+guidelines
        public string OperatingSystem { get; }

        /// <summary>
        /// If true the preprocessor should copy the loader targets to a user location where MSBuild will pick them up.
        /// </summary>
        public bool InstallLoaderTargets { get; private set; }

        /// <summary>
        /// Path to the Java executable.
        /// </summary>
        public string JavaExePath { get; }

        /// <summary>
        /// Skip JRE provisioning (default false).
        /// </summary>
        public bool SkipJreProvisioning { get; }

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
                    Debug.Assert(fileProvider.PropertiesFile is not null, "File properties should not be null");
                    Debug.Assert(!string.IsNullOrWhiteSpace(fileProvider.PropertiesFile.FilePath),
                        "Settings file name should not be null");
                    return fileProvider.PropertiesFile.FilePath;
                }
                return null;
            }
        }

        internal bool IsValid { get; }

        public ProcessedArgs(
            string key,
            string name,
            string version,
            string organization,
            bool installLoaderTargets,
            IAnalysisPropertyProvider cmdLineProperties,
            IAnalysisPropertyProvider globalFileProperties,
            IAnalysisPropertyProvider scannerEnvProperties,
            IFileWrapper fileWrapper,
            IOperatingSystemProvider operatingSystemProvider,
            ILogger logger)
        {
            IsValid = true;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            ProjectKey = key;
            IsValid &= CheckProjectKeyValidity(key, logger);

            ProjectName = name;
            ProjectVersion = version;
            Organization = organization;

            CmdLineProperties = cmdLineProperties ?? throw new ArgumentNullException(nameof(cmdLineProperties));
            this.globalFileProperties = globalFileProperties ?? throw new ArgumentNullException(nameof(globalFileProperties));
            this.operatingSystemProvider = operatingSystemProvider;
            ScannerEnvProperties = scannerEnvProperties ?? throw new ArgumentNullException(nameof(scannerEnvProperties));
            InstallLoaderTargets = installLoaderTargets;

            IsValid &= CheckOrganizationValidity(logger);
            AggregateProperties = new AggregatePropertiesProvider(cmdLineProperties, globalFileProperties, ScannerEnvProperties);
            var isHostSet = AggregateProperties.TryGetValue(SonarProperties.HostUrl, out var sonarHostUrl); // Used for SQ and may also be set to https://SonarCloud.io
            var isSonarcloudSet = AggregateProperties.TryGetValue(SonarProperties.SonarcloudUrl, out var sonarcloudUrl);
            SonarServer = GetAndCheckSonarServer(logger, isHostSet, sonarHostUrl, isSonarcloudSet, sonarcloudUrl);
            IsValid &= SonarServer is not null;

            OperatingSystem = GetOperatingSystem(AggregateProperties);

            if (AggregateProperties.TryGetProperty(SonarProperties.JavaExePath, out var javaExePath))
            {
                if (!fileWrapper.Exists(javaExePath.Value))
                {
                    IsValid = false;
                    logger.LogError(Resources.ERROR_InvalidJavaExePath);
                }
                JavaExePath = javaExePath.Value;
            }
            if (AggregateProperties.TryGetProperty(SonarProperties.SkipJreProvisioning, out var skipJreProvisioningString))
            {
                if (!bool.TryParse(skipJreProvisioningString.Value, out var result))
                {
                    IsValid = false;
                    logger.LogError(Resources.ERROR_InvalidSkipJreProvisioning);
                }
                SkipJreProvisioning = result;
            }
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

        private string GetOperatingSystem(IAnalysisPropertyProvider properties) =>
            properties.TryGetProperty(SonarProperties.OperatingSystem, out var operatingSystem)
                ? operatingSystem.Value
                : operatingSystemProvider.OperatingSystem() switch
                  {
                      PlatformOS.Windows => "windows",
                      PlatformOS.MacOSX => "macos",
                      PlatformOS.Alpine => "alpine",
                      PlatformOS.Linux => "linux",
                      _ => "unknown"
                  };

        private bool CheckOrganizationValidity(ILogger logger)
        {
            if (Organization is null && this.globalFileProperties.TryGetValue(SonarProperties.Organization, out var filePropertiesOrganization))
            {
                logger.LogError(Resources.ERROR_Organization_Provided_In_SonarQubeAnalysis_file);
                return false;
            }
            return true;
        }

        private static bool CheckProjectKeyValidity(string key, ILogger logger)
        {
            if (!ProjectKeyRegEx.SafeIsMatch(key, timeoutFallback: true))
            {
                logger.LogError(Resources.ERROR_InvalidProjectKeyArg);
                return false;
            }
            return true;
        }

        // see spec in https://xtranet-sonarsource.atlassian.net/wiki/spaces/LANG/pages/3155001395/Scanner+Bootstrappers+implementation+guidelines
        private SonarServer GetAndCheckSonarServer(ILogger logger, bool isHostSet, string sonarHostUrl, bool isSonarcloudSet, string sonarcloudUrl)
        {
            const string defaultSonarCloud = "https://sonarcloud.io";
            const string defaultSonarCloudApi = "https://api.sonarcloud.io";
            var info = new { isHostSet, isSonarcloudSet } switch
            {
                { isHostSet: true, isSonarcloudSet: true } when sonarHostUrl != sonarcloudUrl => Error(Resources.ERR_HostUrlDiffersFromSonarcloudUrl),
                { isHostSet: true, isSonarcloudSet: true } when string.IsNullOrWhiteSpace(sonarcloudUrl) => Error(Resources.ERR_HostUrlAndSonarcloudUrlAreEmpty),
                { isHostSet: true, isSonarcloudSet: true } => Warn(new(sonarcloudUrl, defaultSonarCloudApi, true), Resources.WARN_HostUrlAndSonarcloudUrlSet),
                { isHostSet: false, isSonarcloudSet: false } => new(defaultSonarCloud, defaultSonarCloudApi, true),
                { isHostSet: false, isSonarcloudSet: true } => new(sonarcloudUrl, defaultSonarCloudApi, true),
                { isHostSet: true, isSonarcloudSet: false } => sonarHostUrl == defaultSonarCloud
                    ? new(defaultSonarCloud, defaultSonarCloudApi, true)
                    : new(sonarHostUrl, $"{sonarHostUrl.TrimEnd('/')}/api/v2", false),
            };

            if (info is not null)
            {
                // Override by the user
                var apiBaseUrl = AggregateProperties.TryGetProperty(SonarProperties.ApiBaseUrl, out var property)
                    ? property.Value
                    : info.ApiBaseUrl;

                return new(info.ServerUrl, apiBaseUrl, info.IsSonarCloud);
            }

            return null;

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

    public sealed record SonarServer(string ServerUrl, string ApiBaseUrl, bool IsSonarCloud)
    {
        public string ServerUrl { get; } = ServerUrl;
        public string ApiBaseUrl { get; } = ApiBaseUrl;
        public bool IsSonarCloud { get; } = IsSonarCloud;
    }
}
