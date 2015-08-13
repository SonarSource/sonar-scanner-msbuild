//-----------------------------------------------------------------------
// <copyright file="AnalysisConfigGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor
{
    public static class AnalysisConfigGenerator
    {
        public static AnalysisConfig GenerateFile(ProcessedArgs args, TeamBuildSettings settings, IDictionary<string, string> serverProperties, ILogger logger)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }
            if (serverProperties == null)
            {
                throw new ArgumentNullException("serverProperties");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            AnalysisConfig config = new AnalysisConfig();
            config.SonarProjectKey = args.ProjectKey;
            config.SonarProjectName = args.ProjectName;
            config.SonarProjectVersion = args.ProjectVersion;
            config.SonarQubeHostUrl = args.GetSetting(SonarProperties.HostUrl);

            config.SetBuildUri(settings.BuildUri);
            config.SetTfsUri(settings.TfsUri);
            config.SonarConfigDir = settings.SonarConfigDirectory;
            config.SonarOutputDir = settings.SonarOutputDirectory;
            config.SonarBinDir = settings.SonarBinDirectory;


            // Add the server properties to the config
            foreach (var property in serverProperties)
            {
                config.SetInheritedValue(property.Key, property.Value);
            }

            // Merge in command line arguments
            MergeSettingsFromCommandLine(config, args);

            config.Save(settings.AnalysisConfigFilePath);

            return config;
        }

        private static void MergeSettingsFromCommandLine(AnalysisConfig config, ProcessedArgs args)
        {
            if (args == null)
            {
                return;
            }

            foreach (Property item in args.GetAllProperties())
            {
                if (!ProcessRunnerArguments.ContainsSensitiveData(item.Id) && !ProcessRunnerArguments.ContainsSensitiveData(item.Value))
                {
                    config.SetExplicitValue(item.Id, item.Value); // this will overwrite the setting if it already exists
                }
            }
        }

    }
}
