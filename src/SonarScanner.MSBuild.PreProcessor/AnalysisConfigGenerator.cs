/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.TFS;

namespace SonarScanner.MSBuild.PreProcessor
{
    public static class AnalysisConfigGenerator
    {
        /// <summary>
        /// Combines the various configuration options into the AnalysisConfig file
        /// used by the build and post-processor. Saves the file and returns the config instance.
        /// </summary>
        /// <param name="localSettings">Processed local settings, including command line arguments supplied the user</param>
        /// <param name="buildSettings">Build environment settings</param>
        /// <param name="serverProperties">Analysis properties downloaded from the SonarQube server</param>
        /// <param name="analyzerSettings">Specifies the Roslyn analyzers to use. Can be empty</param>
        public static AnalysisConfig GenerateFile(ProcessedArgs localSettings,
            TeamBuildSettings buildSettings,
            IDictionary<string, string> serverProperties,
            List<AnalyzerSettings> analyzersSettings,
            ISonarQubeServer sonarQubeServer,
            ILogger logger)
        {
            if (localSettings == null)
            {
                throw new ArgumentNullException(nameof(localSettings));
            }
            if (buildSettings == null)
            {
                throw new ArgumentNullException(nameof(buildSettings));
            }
            if (serverProperties == null)
            {
                throw new ArgumentNullException(nameof(serverProperties));
            }
            if (sonarQubeServer == null)
            {
                throw new ArgumentNullException(nameof(sonarQubeServer));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            var config = new AnalysisConfig
            {
                SonarProjectKey = localSettings.ProjectKey,
                SonarProjectName = localSettings.ProjectName,
                SonarProjectVersion = localSettings.ProjectVersion,
                SonarQubeHostUrl = localSettings.SonarQubeUrl,
                HasBeginStepCommandLineCredentials = localSettings.CmdLineProperties.HasProperty(SonarProperties.SonarUserName),
                SonarQubeVersion = sonarQubeServer.GetServerVersion().Result.ToString()
            };

            config.SetBuildUri(buildSettings.BuildUri);
            config.SetTfsUri(buildSettings.TfsUri);

            config.SonarConfigDir = buildSettings.SonarConfigDirectory;
            config.SonarOutputDir = buildSettings.SonarOutputDirectory;
            config.SonarBinDir = buildSettings.SonarBinDirectory;
            config.SonarScannerWorkingDirectory = buildSettings.SonarScannerWorkingDirectory;
            config.SourcesDirectory = buildSettings.SourcesDirectory;

            // Add the server properties to the config
            config.ServerSettings = new AnalysisProperties();

            foreach (var property in serverProperties)
            {
                if (!Utilities.IsSecuredServerProperty(property.Key))
                {
                    AddSetting(config.ServerSettings, property.Key, property.Value);
                }
            }

            config.LocalSettings = new AnalysisProperties();
            // From the local settings, we only write the ones coming from the cmd line
            foreach (var property in localSettings.CmdLineProperties.GetAllProperties())
            {
                AddSetting(config.LocalSettings, property.Id, property.Value);
            }

            if (!string.IsNullOrEmpty(localSettings.Organization))
            {
                AddSetting(config.LocalSettings, SonarProperties.Organization, localSettings.Organization);
            }

            // Set the pointer to the properties file
            if (localSettings.PropertiesFileName != null)
            {
                config.SetSettingsFilePath(localSettings.PropertiesFileName);
            }

            config.AnalyzersSettings = analyzersSettings ?? throw new ArgumentNullException(nameof(analyzersSettings));
            config.Save(buildSettings.AnalysisConfigFilePath);

            return config;
        }

        private static void AddSetting(AnalysisProperties properties, string id, string value)
        {
            var property = new Property { Id = id, Value = value };

            // Ensure it isn't possible to write sensitive data to the config file
            if (!property.ContainsSensitiveData())
            {
                properties.Add(new Property { Id = id, Value = value });
            }
        }
    }
}
