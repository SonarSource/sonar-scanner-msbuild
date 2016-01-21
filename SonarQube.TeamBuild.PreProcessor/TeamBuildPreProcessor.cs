//-----------------------------------------------------------------------
// <copyright file="TeamBuildPreProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.PreProcessor.Roslyn;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace SonarQube.TeamBuild.PreProcessor
{
    public class TeamBuildPreProcessor
    {
        public const string FxCopCSharpRuleset = "SonarQubeFxCop-cs.ruleset";
        public const string FxCopVBNetRuleset = "SonarQubeFxCop-vbnet.ruleset";

        private readonly ILogger logger;
        private readonly ISonarQubeServerFactory serverFactory;
        private readonly ITargetsInstaller targetInstaller;
        private readonly IAnalyzerProvider analyzerProvider;

        #region Constructor(s)

        public TeamBuildPreProcessor(ILogger logger)
            : this(logger, new SonarQubeServerFactory(), new TargetsInstaller(), new RoslynAnalyzerProvider(new AnalyzerInstaller(), logger))
        {
        }

        /// <summary>
        /// Internal constructor for testing
        /// </summary>
        public TeamBuildPreProcessor(ILogger logger, ISonarQubeServerFactory serverFactory, ITargetsInstaller targetInstaller, IAnalyzerProvider analyzerInstaller)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            if (serverFactory == null)
            {
                throw new ArgumentNullException("serverFactory");
            }
            if (targetInstaller == null)
            {
                throw new ArgumentNullException("targetInstaller");
            }
            if (analyzerInstaller == null)
            {
                throw new ArgumentNullException("analyzerProvider");
            }

            this.logger = logger;
            this.serverFactory = serverFactory;
            this.targetInstaller = targetInstaller;
            this.analyzerProvider = analyzerInstaller;
        }

        #endregion Constructor(s)

        #region Public methods

        public bool Execute(string[] args)
        {
            bool success;

            ProcessedArgs processedArgs = ArgumentProcessor.TryProcessArgs(args, this.logger);

            if (processedArgs == null)
            {
                success = false;
                this.logger.LogError(Resources.ERROR_InvalidCommandLineArgs);
            }
            else
            {
                success = DoExecute(processedArgs);
            }

            return success;
        }

        private bool DoExecute(ProcessedArgs args)
        {
            Debug.Assert(args != null, "Not expecting the process arguments to be null");

            this.logger.Verbosity = VerbosityCalculator.ComputeVerbosity(args.AggregateProperties, this.logger);

            InstallLoaderTargets(args);

            TeamBuildSettings teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(this.logger);

            // We're checking the args and environment variables so we can report all
            // config errors to the user at once
            if (teamBuildSettings == null)
            {
                this.logger.LogError(Resources.ERROR_CannotPerformProcessing);
                return false;
            }

            // Create the directories
            this.logger.LogDebug(Resources.MSG_CreatingFolders);
            if (!Utilities.TryEnsureEmptyDirectories(this.logger,
                teamBuildSettings.SonarConfigDirectory,
                teamBuildSettings.SonarOutputDirectory))
            {
                return false;
            }

            IDictionary<string, string> serverSettings;
            if (!FetchArgumentsAndRulesets(args, teamBuildSettings, out serverSettings))
            {
                return false;
            }

            AnalysisConfigGenerator.GenerateFile(args, teamBuildSettings, serverSettings, this.logger);

            return true;
        }

        #endregion Public methods

        #region Private methods

        private void InstallLoaderTargets(ProcessedArgs args)
        {
            if (args.InstallLoaderTargets)
            {
                this.targetInstaller.InstallLoaderTargets(this.logger);
            }
            else
            {
                this.logger.LogDebug(Resources.MSG_NotCopyingTargets);
            }
        }

        private bool FetchArgumentsAndRulesets(ProcessedArgs args, TeamBuildSettings settings, out IDictionary<string, string> serverSettings)
        {
            string hostUrl = args.GetSetting(SonarProperties.HostUrl);
            serverSettings = null;

            ISonarQubeServer server = this.serverFactory.Create(args, this.logger);
            try
            {
                // Fetch the SonarQube project properties
                this.logger.LogInfo(Resources.MSG_FetchingAnalysisConfiguration);
                serverSettings = server.GetProperties(args.ProjectKey);

                // Generate the FxCop rulesets
                this.logger.LogInfo(Resources.MSG_GeneratingRulesets);
                GenerateFxCopRuleset(server, args.ProjectKey, "csharp", "cs", "fxcop", Path.Combine(settings.SonarConfigDirectory, FxCopCSharpRuleset));
                GenerateFxCopRuleset(server, args.ProjectKey, "vbnet", "vbnet", "fxcop-vbnet", Path.Combine(settings.SonarConfigDirectory, FxCopVBNetRuleset));

                SonarLintAnalyzerProvider.SetupAnalyzers(server, settings, args.ProjectKey, this.logger);

                CompilerAnalyzerConfig analyzerConfig = this.analyzerProvider.SetupAnalyzers(server, settings, args.ProjectKey);
                //TODO: save the config so it can be picked up by the targets files

            }
            catch (WebException ex)
            {
                if (Utilities.HandleHostUrlWebException(ex, hostUrl, this.logger))
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

        private void GenerateFxCopRuleset(ISonarQubeServer server, string projectKey, string requiredPluginKey, string language, string repository, string path)
        {
            this.logger.LogDebug(Resources.MSG_GeneratingRuleset, path);
            RulesetGenerator.Generate(server, requiredPluginKey, language, repository, projectKey, path);
        }

        #endregion Private methods
    }
}