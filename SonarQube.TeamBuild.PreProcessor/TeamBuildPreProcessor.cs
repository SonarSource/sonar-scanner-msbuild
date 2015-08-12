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
        public const string FxCopCSharpRuleset = "SonarQubeFxCop-cs.ruleset";
        public const string FxCopVBNetRuleset = "SonarQubeFxCop-vbnet.ruleset";

        private readonly IPropertiesFetcher propertiesFetcher;
        private readonly IRulesetGenerator rulesetGenerator;
        private readonly ITargetsInstaller targetInstaller;

        #region Constructor(s)

        public TeamBuildPreProcessor()
            : this(new PropertiesFetcher(), new RulesetGenerator(), new TargetsInstaller())
        {
        }

        /// <summary>
        /// Internal constructor for testing
        /// </summary>
        public TeamBuildPreProcessor(IPropertiesFetcher propertiesFetcher, IRulesetGenerator rulesetGenerator, ITargetsInstaller targetInstaller) // was internal
        {
            if (propertiesFetcher == null)
            {
                throw new ArgumentNullException("propertiesFetcher");
            }
            if (rulesetGenerator == null)
            {
                throw new ArgumentNullException("rulesetGenerator");
            }
            if (targetInstaller == null)
            {
                throw new ArgumentNullException("rulesetGenerator");
            }

            this.propertiesFetcher = propertiesFetcher;
            this.rulesetGenerator = rulesetGenerator;
            this.targetInstaller = targetInstaller;
        }

        #endregion Constructor(s)

        #region Public methods

        public bool Execute(string[] args, ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            bool success;

            ProcessedArgs processedArgs = ArgumentProcessor.TryProcessArgs(args, logger);

            if (processedArgs == null)
            {
                success = false;
                logger.LogError(Resources.ERROR_InvalidCommandLineArgs);
            }
            else
            {
                success = DoExecute(processedArgs, logger);
            }

            return success;
        }

        private bool DoExecute(ProcessedArgs args, ILogger logger)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            InstallLoaderTargets(args, logger);

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
            logger.LogDebug(Resources.DIAG_CreatingFolders);
            if (!Utilities.TryEnsureEmptyDirectories(logger,
                config.SonarConfigDir,
                config.SonarOutputDir))
            {
                return false;
            }

            if (!FetchArgumentsAndRulesets(args, config, logger))
            {
                return false;
            }

            // Save the config file
            logger.LogDebug(Resources.DIAG_SavingConfigFile, teamBuildSettings.AnalysisConfigFilePath);
            config.Save(teamBuildSettings.AnalysisConfigFilePath);

            return true;
        }

        private void InstallLoaderTargets(ProcessedArgs args, ILogger logger)
        {
            if (args.InstallLoaderTargets)
            {
                this.targetInstaller.InstallLoaderTargets(logger);
            }
            else
            {
                logger.LogDebug(Resources.INFO_NotCopyingTargets);
            }
        }

        #endregion Public methods

        #region Private methods

        private bool FetchArgumentsAndRulesets(ProcessedArgs args, AnalysisConfig config, ILogger logger)
        {
            string hostUrl = args.GetSetting(SonarProperties.HostUrl);

            try
            {
                using (IDownloader downloader = GetDownloader(args))
                {
                    SonarWebService ws = new SonarWebService(downloader, hostUrl);

                    // Fetch the SonarQube project properties
                    this.FetchSonarQubeProperties(config, ws);

                    // Merge in command line arguments
                    MergeSettingsFromCommandLine(config, args);

                    // Generate the FxCop rulesets
                    GenerateFxCopRuleset(config, ws, "csharp", "cs", "fxcop", Path.Combine(config.SonarConfigDir, FxCopCSharpRuleset), logger);
                    GenerateFxCopRuleset(config, ws, "vbnet", "vbnet", "fxcop-vbnet", Path.Combine(config.SonarConfigDir, FxCopVBNetRuleset), logger);
                }
            }
            catch (WebException ex)
            {
                if (Utilities.HandleHostUrlWebException(ex, hostUrl, logger))
                {
                    return false;
                }

                throw;
            }

            return true;
        }

        private static IDownloader GetDownloader(ProcessedArgs args)
        {
            string username = args.GetSetting(SonarProperties.SonarUserName, null);
            string password = args.GetSetting(SonarProperties.SonarPassword, null);

            return new WebClientDownloader(new WebClient(), username, password);
        }

        private void FetchSonarQubeProperties(AnalysisConfig config, SonarWebService ws)
        {
            var properties = this.propertiesFetcher.FetchProperties(ws, config.SonarProjectKey);
            foreach (var property in properties)
            {
                config.SetInheritedValue(property.Key, property.Value);
            }
        }

        private void GenerateFxCopRuleset(AnalysisConfig config, SonarWebService ws, string requiredPluginKey, string language, string repository, string path, ILogger logger)
        {
            logger.LogDebug(Resources.DIAG_GeneratingRuleset, path);
            this.rulesetGenerator.Generate(ws, requiredPluginKey, language, repository, config.SonarProjectKey, path);
        }

        private static void MergeSettingsFromCommandLine(AnalysisConfig config, ProcessedArgs args)
        {
            if (args == null)
            {
                return;
            }

            foreach (Property item in args.GetAllProperties())
            {
                config.SetExplicitValue(item.Id, item.Value); // this will overwrite the setting if it already exists
            }
        }

        #endregion Private methods
    }
}