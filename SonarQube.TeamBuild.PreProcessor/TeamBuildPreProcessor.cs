//-----------------------------------------------------------------------
// <copyright file="TeamBuildPreProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System;
using System.IO;
using System.Net;

namespace SonarQube.TeamBuild.PreProcessor
{
    // was internal
    public class TeamBuildPreProcessor
    {
        public const string FxCopRulesetFileName = "SonarQubeAnalysis.ruleset";
        private const string DefaultSonarServerUrl = "http://localhost:9000";

        private readonly IPropertiesFetcher propertiesFetcher;
        private readonly IRulesetGenerator rulesetGenerator;

        #region Constructor(s)

        public TeamBuildPreProcessor()
            : this(new PropertiesFetcher(), new RulesetGenerator())
        {
        }

        /// <summary>
        /// Internal constructor for testing
        /// </summary>
        public TeamBuildPreProcessor(IPropertiesFetcher propertiesFetcher, IRulesetGenerator rulesetGenerator) // was internal
        {
            if (propertiesFetcher == null)
            {
                throw new ArgumentNullException("propertiesFetcher");
            }
            if (rulesetGenerator == null)
            {
                throw new ArgumentNullException("rulesetGenerator");
            }

            this.propertiesFetcher = propertiesFetcher;
            this.rulesetGenerator = rulesetGenerator;
        }

        #endregion
        
        #region Public methods

        public bool Execute(ILogger logger, string projectKey, string projectName, string projectVersion, string propertiesPath)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            if (string.IsNullOrWhiteSpace(projectKey))
            {
                throw new ArgumentNullException("projectKey");
            }
            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new ArgumentNullException("projectName");
            }
            if (string.IsNullOrWhiteSpace(projectVersion))
            {
                throw new ArgumentNullException("projectVersion");
            }
            if (string.IsNullOrWhiteSpace(propertiesPath))
            {
                throw new ArgumentNullException("propertiesPath");
            }
            
            AnalysisConfig config = new AnalysisConfig();
            config.SonarProjectKey = projectKey;
            config.SonarProjectName = projectName;
            config.SonarProjectVersion = projectVersion;
            config.SonarRunnerPropertiesPath = propertiesPath;

            TeamBuildSettings teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(logger);

            // We're checking the args and environment variables so we can report all
            // config errors to the user at once
            if (teamBuildSettings == null)
            {
                logger.LogError(Resources.ERROR_CannotPerformProcessing);
                return false;
            }

            config.SetBuildUri(teamBuildSettings.BuildUri);
            config.SetTfsUri(teamBuildSettings.TfsUri);
            config.SonarConfigDir = teamBuildSettings.SonarConfigDirectory;
            config.SonarOutputDir = teamBuildSettings.SonarOutputDirectory;

            // Create the directories
            logger.LogMessage(Resources.DIAG_CreatingFolders);
            EnsureEmptyDirectory(logger, config.SonarConfigDir);
            EnsureEmptyDirectory(logger, config.SonarOutputDir);

            using (SonarWebService ws = GetSonarWebService(config))
            {
                // Fetch the SonarQube project properties
                FetchSonarQubeProperties(config, ws);

                // Generate the FxCop ruleset
                GenerateFxCopRuleset(config, ws, logger);
            }

            // Save the config file
            logger.LogMessage(Resources.DIAG_SavingConfigFile, teamBuildSettings.AnalysisConfigFilePath);
            config.Save(teamBuildSettings.AnalysisConfigFilePath);

            return true;
        }

        #endregion

        #region Private methods

        private static void EnsureEmptyDirectory(ILogger logger, string directory)
        {
            if (Directory.Exists(directory))
            {
                logger.LogMessage(Resources.DIAG_DeletingDirectory, directory);
                Directory.Delete(directory, true);
            }
            logger.LogMessage(Resources.DIAG_CreatingDirectory, directory);
            Directory.CreateDirectory(directory);
        }

        private static SonarWebService GetSonarWebService(AnalysisConfig config)
        {
            FilePropertiesProvider sonarRunnerProperties = new FilePropertiesProvider(config.SonarRunnerPropertiesPath);

            string server = sonarRunnerProperties.GetProperty(SonarProperties.HostUrl, DefaultSonarServerUrl);
            string username = sonarRunnerProperties.GetProperty(SonarProperties.SonarUserName, null);
            string password = sonarRunnerProperties.GetProperty(SonarProperties.SonarPassword, null);

            return new SonarWebService(new WebClientDownloader(new WebClient(), username, password), server, "cs", "fxcop");
        }

        private void FetchSonarQubeProperties(AnalysisConfig config, SonarWebService ws)
        {
            var properties = this.propertiesFetcher.FetchProperties(ws, config.SonarProjectKey);
            foreach (var property in properties)
            {
                config.SetValue(property.Key, property.Value);
            }
        }

        private void GenerateFxCopRuleset(AnalysisConfig config, SonarWebService ws, ILogger logger)
        {
            logger.LogMessage(Resources.DIAG_GeneratingRuleset);
            this.rulesetGenerator.Generate(ws, config.SonarProjectKey, Path.Combine(config.SonarConfigDir, FxCopRulesetFileName));
        }

        #endregion
    }
}

