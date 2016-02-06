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

        private readonly IPreprocessorObjectFactory factory;
        private readonly ILogger logger;

        #region Constructor(s)

        public TeamBuildPreProcessor(IPreprocessorObjectFactory factory, ILogger logger)
        {
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            this.factory = factory;
            this.logger = logger;
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
            AnalyzerSettings analyzerSettings;
            if (!FetchArgumentsAndRulesets(args, teamBuildSettings, out serverSettings, out analyzerSettings))
            {
                return false;
            }

            AnalysisConfigGenerator.GenerateFile(args, teamBuildSettings, serverSettings, analyzerSettings, this.logger);

            return true;
        }

        #endregion Public methods

        #region Private methods

        private void InstallLoaderTargets(ProcessedArgs args)
        {
            if (args.InstallLoaderTargets)
            {
                ITargetsInstaller installer = this.factory.CreateTargetInstaller();
                Debug.Assert(installer != null, "Factory should not return null");
                installer.InstallLoaderTargets(this.logger);
            }
            else
            {
                this.logger.LogDebug(Resources.MSG_NotCopyingTargets);
            }
        }

        private bool FetchArgumentsAndRulesets(ProcessedArgs args, TeamBuildSettings settings, out IDictionary<string, string> serverSettings, out AnalyzerSettings analyzerSettings)
        {
            string hostUrl = args.GetSetting(SonarProperties.HostUrl);
            serverSettings = null;
            analyzerSettings = null;

            ISonarQubeServer server = this.factory.CreateSonarQubeServer(args, this.logger);
            try
            {
                this.logger.LogInfo(Resources.MSG_FetchingAnalysisConfiguration);

                // Respect sonar.branch setting if set
                string projectBranch = null;
                args.TryGetSetting(SonarProperties.ProjectBranch, out projectBranch);

                // Fetch the SonarQube project properties
                serverSettings = server.GetProperties(args.ProjectKey, projectBranch);

                // Generate the FxCop rulesets
                this.logger.LogInfo(Resources.MSG_GeneratingRulesets);
                GenerateFxCopRuleset(server, args.ProjectKey, projectBranch,
                    "csharp", "cs", "fxcop", Path.Combine(settings.SonarConfigDirectory, FxCopCSharpRuleset));
                GenerateFxCopRuleset(server, args.ProjectKey, projectBranch, 
                    "vbnet", "vbnet", "fxcop-vbnet", Path.Combine(settings.SonarConfigDirectory, FxCopVBNetRuleset));

                IAnalyzerProvider analyzerProvider = this.factory.CreateAnalyzerProvider(this.logger);
                Debug.Assert(analyzerProvider != null, "Factory should not return null");

                analyzerSettings = analyzerProvider.SetupAnalyzers(server, settings, args.ProjectKey, projectBranch);
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

        private void GenerateFxCopRuleset(ISonarQubeServer server, string projectKey, string projectBranch, string requiredPluginKey, string language, string repository, string path)
        {
            this.logger.LogDebug(Resources.MSG_GeneratingRuleset, path);
            RulesetGenerator.Generate(server, requiredPluginKey, language, repository, projectKey, projectBranch, path);
        }

        #endregion Private methods
    }
}