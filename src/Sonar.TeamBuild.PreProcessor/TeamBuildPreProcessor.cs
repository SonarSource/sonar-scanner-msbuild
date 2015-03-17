//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using Sonar.FxCopRuleset;
using Sonar.TeamBuild.Integration;
using System;
using System.IO;

namespace Sonar.TeamBuild.PreProcessor
{
    internal class TeamBuildPreProcessor
    {
        public const string FxCopRulesetFileName = "SonarAnalysis.ruleset";

        private ILogger logger;
        private IRulesetGenerator rulesetGenerator;

        #region Constructor(s)

        public TeamBuildPreProcessor()
            : this(new ConsoleLogger(), new RulesetGenerator())
        {
        }

        /// <summary>
        /// Internal constructor for testing
        /// </summary>
        internal TeamBuildPreProcessor(ILogger logger, IRulesetGenerator rulesetGenerator)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            if (rulesetGenerator == null)
            {
                throw new ArgumentNullException("rulesetGenerator");
            }

            this.logger = logger;
            this.rulesetGenerator = rulesetGenerator;
        }

        #endregion
        
        #region Public methods

        public bool Execute(ILogger logger, string projectKey, string projectName, string projectVersion, string propertiesPath)
        {
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
            config.SonarConfigDir = teamBuildSettings.SonarConfigDir;
            config.SonarOutputDir = teamBuildSettings.SonarOutputDir;

            // Create the directories
            logger.LogMessage(Resources.DIAG_CreatingFolders);
            EnsureEmptyDirectory(logger, config.SonarConfigDir);
            EnsureEmptyDirectory(logger, config.SonarOutputDir);

            // Save the config file
            logger.LogMessage(Resources.DIAG_SavingConfigFile, teamBuildSettings.AnalysisConfigFilePath);
            config.Save(teamBuildSettings.AnalysisConfigFilePath);

            GenerateFxCopRuleset(logger, config);

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

        private void GenerateFxCopRuleset(ILogger logger, AnalysisConfig config)
        {
            FilePropertiesProvider sonarRunnerProperties = new FilePropertiesProvider(config.SonarRunnerPropertiesPath);

            string server = sonarRunnerProperties.GetProperty(SonarProperties.HostUrl, "http://localhost:9000");
            string username = sonarRunnerProperties.GetProperty(SonarProperties.SonarUserName, null);
            string password = sonarRunnerProperties.GetProperty(SonarProperties.SonarPassword, null);

            string rulesetPath = Path.Combine(config.SonarConfigDir, FxCopRulesetFileName);

            this.rulesetGenerator.Generate(config.SonarProjectKey, rulesetPath, server, username, password);
        }

        #endregion
    }
}
