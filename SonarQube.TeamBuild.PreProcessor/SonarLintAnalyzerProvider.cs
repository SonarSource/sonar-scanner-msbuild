//-----------------------------------------------------------------------
// <copyright file="SonarLintAnalyzerProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System;
using System.IO;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Handles fetching the SonarLint ruleset and provisioning the SonarLint assemblies on the client
    /// </summary>
    /// <remarks>This code is a short-term solution introduced in MSBuild Scanner version 1.1.
    /// Going forwards, we want to provide a more general solution that will work for all Roslyn
    /// analyzers that the SonarQube server knows about. It is likely that the general-purpose
    /// solution will be quite different.</remarks>
    public static class SonarLintAnalyzerProvider
    {
        public const string RoslynCSharpRulesetFileName = "SonarQubeRoslyn-cs.ruleset";

        public const string SonarLintProfileFormatName = "sonarlint-vs-cs";
        public const string CSharpLanguage = "cs";
        public const string CSharpPluginKey = "csharp";
        public const string CSharpRepositoryKey = "csharp";

        /// <summary>
        /// Sets up the client to run the SonarLint analyzer as part of the build
        /// i.e. creates the Rolsyn ruleset and provisions the analyzer assemblies
        /// </summary>
        public static void SetupAnalyzers(ISonarQubeServer server, TeamBuildSettings settings, string projectKey, ILogger logger)
        {
            if (server == null)
            {
                throw new ArgumentNullException("server");
            }
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }
            if (string.IsNullOrWhiteSpace(projectKey))
            {
                throw new ArgumentNullException("projectKey");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            if (IsCSharpPluginInstalled(server))
            {
                if (TryCreateRuleset(server, settings, projectKey, logger))
                {
                    FetchBinaries(server, settings, logger);
                }
            }
            else
            {
                logger.LogDebug(Resources.SLAP_CSharpPluginNotInstalled);
            }
        }

        private static bool IsCSharpPluginInstalled(ISonarQubeServer server)
        {
            return server.GetInstalledPlugins().Contains(CSharpPluginKey);
        }

        private static bool TryCreateRuleset(ISonarQubeServer server, TeamBuildSettings settings, string projectKey, ILogger logger)
        {
            logger.LogDebug(Resources.SLAP_FetchingSonarLintRuleset);
            string content = TryGetProfileExportForProject(server, projectKey, logger);

            if (content != null)
            {
                if (IsValidRuleset(content))
                {
                    string ruleSetFilePath = GetRulesetFilePath(settings);
                    logger.LogDebug(Resources.SLAP_SonarLintRulesetCreated, ruleSetFilePath);

                    File.WriteAllText(ruleSetFilePath, content);
                    return true;
                }
                else
                {
                    // TODO: decide on an appropriate error message once we know whether could
                    // happen in the 1.1 release.
                    logger.LogError(Resources.SLAP_InvalidRulesetReturned);
                }
            }
            return false;
        }

        private static string TryGetProfileExportForProject(ISonarQubeServer server, string projectKey, ILogger logger)
        {
            string profileContent = null;

            string qualityProfile;
            if (server.TryGetQualityProfile(projectKey, CSharpLanguage, out qualityProfile))
            {
                if (server.TryGetProfileExport(qualityProfile, CSharpLanguage, SonarLintProfileFormatName, out profileContent))
                {
                    logger.LogDebug(Resources.SLAP_ProfileExportFound, projectKey);
                }
                else
                {
                    logger.LogDebug(Resources.SLAP_ProfileExportNotFound, projectKey);
                }
            }
            else
            {
                logger.LogDebug(Resources.SLAP_NoProfileForProject, projectKey);
            }
            return profileContent;
        }

        private static bool IsValidRuleset(string rulesetContent)
        {
            // TODO: check the file contains a ruleset. There is a platform bug that means the wrong
            // profile could be exported, depending on which plugins are installed on the server.
            // Depending on the workaround put into place in the next version of the C# plugin, this
            // might not occur in practice. If it could still occur then consider making this check more robust.
            return rulesetContent.Contains("<RuleSet Name=\"Rules for SonarLint");
        }

        private static string GetRulesetFilePath(TeamBuildSettings settings)
        {
            return Path.Combine(settings.SonarConfigDirectory, RoslynCSharpRulesetFileName);
        }

        private static void FetchBinaries(ISonarQubeServer server, TeamBuildSettings settings, ILogger logger)
        {
            // TODO: https://jira.sonarsource.com/browse/SONARCS-556
        }

    }
}