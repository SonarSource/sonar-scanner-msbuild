//-----------------------------------------------------------------------
// <copyright file="TeamBuildPreProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System;
using System.Collections.Generic;
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

            logger.Verbosity = VerbosityCalculator.ComputeVerbosity(args.AggregateProperties, logger);

            InstallLoaderTargets(args, logger);

            TeamBuildSettings teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(logger);

            // We're checking the args and environment variables so we can report all
            // config errors to the user at once
            if (teamBuildSettings == null)
            {
                logger.LogError(Resources.ERROR_CannotPerformProcessing);
                return false;
            }

            // Create the directories
            logger.LogDebug(Resources.MSG_CreatingFolders);
            if (!Utilities.TryEnsureEmptyDirectories(logger,
                teamBuildSettings.SonarConfigDirectory,
                teamBuildSettings.SonarOutputDirectory))
            {
                return false;
            }

            IDictionary<string, string> serverSettings;
            if (!FetchArgumentsAndRulesets(args, teamBuildSettings.SonarConfigDirectory, logger, out serverSettings))
            {
                return false;
            }

            AnalysisConfigGenerator.GenerateFile(args, teamBuildSettings, serverSettings, logger);

            return true;
        }

        #endregion Public methods

        #region Private methods

        private void InstallLoaderTargets(ProcessedArgs args, ILogger logger)
        {
            if (args.InstallLoaderTargets)
            {
                this.targetInstaller.InstallLoaderTargets(logger);
            }
            else
            {
                logger.LogDebug(Resources.MSG_NotCopyingTargets);
            }
        }

        private bool FetchArgumentsAndRulesets(ProcessedArgs args, string configDir, ILogger logger, out IDictionary<string, string> serverSettings)
        {
            string hostUrl = args.GetSetting(SonarProperties.HostUrl);
            serverSettings = null;

            try
            {
                using (IDownloader downloader = GetDownloader(args))
                {
                    SonarWebService ws = new SonarWebService(downloader, hostUrl);

                    // Fetch the SonarQube project properties
                    logger.LogInfo(Resources.MSG_FetchingAnalysisConfiguration);
                    serverSettings = this.propertiesFetcher.FetchProperties(ws, args.ProjectKey, logger);

                    // Generate the FxCop rulesets
                    logger.LogInfo(Resources.MSG_GeneratingRulesets);
                    GenerateFxCopRuleset(ws, args.ProjectKey, "csharp", "cs", "fxcop", Path.Combine(configDir, FxCopCSharpRuleset), logger);
                    GenerateFxCopRuleset(ws, args.ProjectKey, "vbnet", "vbnet", "fxcop-vbnet", Path.Combine(configDir, FxCopVBNetRuleset), logger);
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

        private void GenerateFxCopRuleset(SonarWebService ws, string projectKey, string requiredPluginKey, string language, string repository, string path, ILogger logger)
        {
            logger.LogDebug(Resources.MSG_GeneratingRuleset, path);
            this.rulesetGenerator.Generate(ws, requiredPluginKey, language, repository, projectKey, path);
        }

        #endregion Private methods
    }
}