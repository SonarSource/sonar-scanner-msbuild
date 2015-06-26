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

        public bool Execute(ProcessedArgs args, ILogger logger)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            
            AnalysisConfig config = new AnalysisConfig();
            config.SonarProjectKey = args.ProjectKey;
            config.SonarProjectName = args.ProjectName;
            config.SonarProjectVersion = args.ProjectVersion;

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
            config.SonarBinDir = teamBuildSettings.SonarBinDirectory;
            config.SonarQubeHostUrl = args.GetSetting(SonarProperties.HostUrl);

            // Create the directories
            logger.LogMessage(Resources.DIAG_CreatingFolders);
            Utilities.EnsureEmptyDirectory(config.SonarConfigDir, logger);
            Utilities.EnsureEmptyDirectory(config.SonarOutputDir, logger);

            using (SonarWebService ws = GetSonarWebService(args))
            {
                // Fetch the SonarQube project properties
                FetchSonarQubeProperties(config, ws);

                // Merge in command line arguments
                MergeSettingsFromCommandLine(config, args);

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

        private static SonarWebService GetSonarWebService(ProcessedArgs args)
        {
            string server = args.GetSetting(SonarProperties.HostUrl, DefaultSonarServerUrl);
            string username = args.GetSetting(SonarProperties.SonarUserName, null);
            string password = args.GetSetting(SonarProperties.SonarPassword, null);

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

        private static void MergeSettingsFromCommandLine(AnalysisConfig config, ProcessedArgs args)
        {
            if (args == null)
            {
                return;
            }

            foreach (Property item in args.GetAllProperties())
            {
                config.SetValue(item.Id, item.Value); // this will overwrite the setting if it already exists
            }
        }

        #endregion
    }
}

