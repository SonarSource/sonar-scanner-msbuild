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
    public class TeamBuildPreProcessor
    {
        public const string FxCopCSharpRuleset = "SonarQubeFxCop-cs.ruleset";
        public const string FxCopVBNetRuleset = "SonarQubeFxCop-vbnet.ruleset";

        private readonly ISonarQubeServerFactory serverFactory;
        private readonly ITargetsInstaller targetInstaller;

        #region Constructor(s)

        public TeamBuildPreProcessor()
            : this(new SonarQubeServerFactory(), new TargetsInstaller())
        {
        }

        /// <summary>
        /// Internal constructor for testing
        /// </summary>
        public TeamBuildPreProcessor(ISonarQubeServerFactory serverFactory, ITargetsInstaller targetInstaller)
        {
            if (serverFactory == null)
            {
                throw new ArgumentNullException("serverFactory");
            }
            if (targetInstaller == null)
            {
                throw new ArgumentNullException("targetInstaller");
            }

            this.serverFactory = serverFactory;
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
            if (!FetchArgumentsAndRulesets(args, teamBuildSettings, logger, out serverSettings))
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

        private bool FetchArgumentsAndRulesets(ProcessedArgs args, TeamBuildSettings settings, ILogger logger, out IDictionary<string, string> serverSettings)
        {
            string hostUrl = args.GetSetting(SonarProperties.HostUrl);
            serverSettings = null;

            ISonarQubeServer server = this.serverFactory.Create(args);
            try
            {
                // Fetch the SonarQube project properties
                logger.LogInfo(Resources.MSG_FetchingAnalysisConfiguration);
                serverSettings = server.GetProperties(args.ProjectKey, logger);

                // Generate the FxCop rulesets
                logger.LogInfo(Resources.MSG_GeneratingRulesets);
                GenerateFxCopRuleset(server, args.ProjectKey, "csharp", "cs", "fxcop", Path.Combine(settings.SonarConfigDirectory, FxCopCSharpRuleset), logger);
                GenerateFxCopRuleset(server, args.ProjectKey, "vbnet", "vbnet", "fxcop-vbnet", Path.Combine(settings.SonarConfigDirectory, FxCopVBNetRuleset), logger);

                SonarLintAnalyzerProvider.SetupAnalyzers(server, settings, args.ProjectKey, logger);
            }
            catch (WebException ex)
            {
                if (Utilities.HandleHostUrlWebException(ex, hostUrl, logger))
                {
                    return false;
                }

                throw;
            }
            finally
            {
                Utilities.SafeDispose(server);
            }

            return true;
        }

        private static void GenerateFxCopRuleset(ISonarQubeServer server, string projectKey, string requiredPluginKey, string language, string repository, string path, ILogger logger)
        {
            logger.LogDebug(Resources.MSG_GeneratingRuleset, path);
            RulesetGenerator.Generate(server, requiredPluginKey, language, repository, projectKey, path);
        }

        #endregion Private methods
    }
}