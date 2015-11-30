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
using System.IO.Compression;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Handles fetching the SonarLint ruleset and provisioning the SonarLint assemblies on the client
    /// </summary>
    /// <remarks>This code is a short-term solution introduced in MSBuild Scanner version 1.1.
    /// Going forwards, we want to provide a more general solution that will work for all Roslyn
    /// analyzers that the SonarQube server knows about. It is likely that the general-purpose
    /// solution will be quite different.
    /// <para>
    /// We won't be able to run the analyzers unless the user is using MSBuild 14.0 or later.
    /// However, this code is called during the pre-process stage i.e. we don't know which
    /// version of MSBuild will be used so we have to download the analyzers even if we
    /// can't then use them.
    /// </para></remarks>
    public static class SonarLintAnalyzerProvider
    {
        public const string RoslynCSharpRulesetFileName = "SonarQubeRoslyn-cs.ruleset";
        public const string SonarLintCSharpParametersFileName = "SonarLint.xml";

        public const string SonarLintProfileFormatName = "sonarlint-vs-cs";
        public const string SonarLintParametersFormatName = "sonarlint-vs-param-cs";
        public const string CSharpLanguage = "cs";
        public const string CSharpPluginKey = "csharp";
        public const string CSharpRepositoryKey = "csharp";

        public const string EmbeddedSonarLintZipFileName = "SonarLint.zip";

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

            string rulesetContent;
            string parametersContent;
            if (TryGetProfileExportForProject(server, projectKey, logger, out rulesetContent, out parametersContent))
            {
                if (IsValidRuleset(rulesetContent))
                {
                    string ruleSetFilePath = GetRulesetFilePath(settings);
                    logger.LogDebug(Resources.SLAP_SonarLintRulesetCreated, ruleSetFilePath);
                    File.WriteAllText(ruleSetFilePath, rulesetContent);

                    string parametersFilePath = GetParametersFilePath(settings);
                    logger.LogDebug(Resources.SLAP_SonarLintParametersCreated, parametersFilePath);
                    File.WriteAllText(parametersFilePath, parametersContent);

                    return true;
                }
                else
                {
                    logger.LogError(Resources.SLAP_InvalidRulesetReturned);
                }
            }
            return false;
        }

        private static bool TryGetProfileExportForProject(ISonarQubeServer server, string projectKey, ILogger logger, out string rulesetContent, out string parametersContent)
        {
            string qualityProfile;
            if (!server.TryGetQualityProfile(projectKey, CSharpLanguage, out qualityProfile))
            {
                logger.LogDebug(Resources.SLAP_NoProfileForProject, projectKey);
                rulesetContent = null;
                parametersContent = null;
                return false;
            }

            if (!server.TryGetProfileExport(qualityProfile, CSharpLanguage, SonarLintProfileFormatName, out rulesetContent))
            {
                logger.LogDebug(Resources.SLAP_ProfileExportNotFound, SonarLintProfileFormatName, projectKey);
                parametersContent = null;
                return false;
            }
            logger.LogDebug(Resources.SLAP_ProfileExportFound, SonarLintProfileFormatName, projectKey);

            if (!server.TryGetProfileExport(qualityProfile, CSharpLanguage, SonarLintParametersFormatName, out parametersContent))
            {
                logger.LogDebug(Resources.SLAP_ProfileExportNotFound, SonarLintParametersFormatName, projectKey);
                return false;
            }
            logger.LogDebug(Resources.SLAP_ProfileExportFound, SonarLintParametersFormatName, projectKey);

            return true;
        }

        private static bool IsValidRuleset(string rulesetContent)
        {
            // There is a platform bug that means the wrong profile can be exported if unless both a
            // profile exporter and importer exist. This should not be a problem with version 4.4
            // of the C# plugin, so we shouldn't hit this situation.
            return rulesetContent.Contains("<RuleSet Name=\"Rules for SonarLint");
        }

        private static string GetRulesetFilePath(TeamBuildSettings settings)
        {
            return Path.Combine(settings.SonarConfigDirectory, RoslynCSharpRulesetFileName);
        }

        private static string GetParametersFilePath(TeamBuildSettings settings)
        {
            return Path.Combine(settings.SonarConfigDirectory, SonarLintCSharpParametersFileName);
        }

        private static void FetchBinaries(ISonarQubeServer server, TeamBuildSettings settings, ILogger logger)
        {
            // For the 1.1 release of the runner/scanner, we are hard-coding support for a single known version
            // of SonarLint. The required assemblies will be packaged in the "static" folder of the jar (in the
            // same way that the SonarQube.MSBuild.Runner.Implementation.zip file is).
            logger.LogDebug(Resources.SLAP_FetchingSonarLintAnalyzer);
            if (server.TryDownloadEmbeddedFile(CSharpPluginKey, EmbeddedSonarLintZipFileName, settings.SonarBinDirectory))
            {
                string filePath = Path.Combine(settings.SonarBinDirectory, EmbeddedSonarLintZipFileName);

                logger.LogDebug(Resources.MSG_ExtractingFiles, settings.SonarBinDirectory);
                ZipFile.ExtractToDirectory(filePath, settings.SonarBinDirectory);
            }
            else
            {
                logger.LogDebug(Resources.SLAP_AnalyzerNotFound);
            }
        }

    }
}