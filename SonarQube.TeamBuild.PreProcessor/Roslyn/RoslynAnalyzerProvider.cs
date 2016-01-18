//-----------------------------------------------------------------------
// <copyright file="RoslynAnalyzerProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    public class RoslynAnalyzerProvider
    {
        public const string RoslynCSharpFormatName = "roslyn-config-cs";
        public const string RoslynCSharpRulesetFileName = "SonarQubeRoslyn-cs.ruleset";

        public const string CSharpLanguage = "cs";
        public const string CSharpPluginKey = "csharp";
        public const string CSharpRepositoryKey = "csharp";

        private readonly ISonarQubeServer server;
        private readonly TeamBuildSettings settings;
        private readonly string projectKey;
        private readonly ILogger logger;

        #region Public methods

        /// Sets up the client to run the Roslyn analyzers as part of the build
        /// i.e. creates the Roslyn ruleset and provisions the analyzer assemblies
        /// and rule parameter files
        /// </summary>
        public static CompilerAnalyzerConfig SetupAnalyzers(ISonarQubeServer server, TeamBuildSettings settings, string projectKey, ILogger logger)
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
                RoslynAnalyzerProvider provider = new RoslynAnalyzerProvider(server, settings, projectKey, logger);
                return provider.GetCompilerConfig();
            }
            else
            {
                logger.LogDebug(Resources.SLAP_CSharpPluginNotInstalled);
            }

            return null;
        }

        #endregion

        #region Private methods

        private static bool IsCSharpPluginInstalled(ISonarQubeServer server)
        {
            return server.GetInstalledPlugins().Contains(CSharpPluginKey);
        }

        private RoslynAnalyzerProvider(ISonarQubeServer server, TeamBuildSettings settings, string projectKey, ILogger logger)
        {
            this.server = server;
            this.settings = settings;
            this.projectKey = projectKey;
            this.logger = logger;
        }

        private CompilerAnalyzerConfig GetCompilerConfig()
        {
            CompilerAnalyzerConfig compilerConfig = null;

            RoslynExportProfile profile = TryGetRoslynConfigForProject();
            if (profile != null)
            {
                compilerConfig = ProcessProfile(profile);
            }
            return compilerConfig;
        }

        private RoslynExportProfile TryGetRoslynConfigForProject()
        {
            string qualityProfile;
            if (!this.server.TryGetQualityProfile(projectKey, CSharpLanguage, out qualityProfile))
            {
                this.logger.LogDebug(Resources.SLAP_NoProfileForProject, this.projectKey);
                return null;
            }

            string profileContent = null;
            if (!server.TryGetProfileExport(qualityProfile, CSharpLanguage, RoslynCSharpFormatName, out profileContent))
            {
                this.logger.LogDebug(Resources.SLAP_ProfileExportNotFound, RoslynCSharpFormatName, this.projectKey);
                return null;
            }
            this.logger.LogDebug(Resources.SLAP_ProfileExportFound, RoslynCSharpFormatName, this.projectKey);

            RoslynExportProfile profile = null;
            using (StringReader reader = new StringReader(profileContent))
            {
                profile = RoslynExportProfile.Load(reader);
            }

            return profile;
        }

        private CompilerAnalyzerConfig ProcessProfile(RoslynExportProfile profile)
        {
            Debug.Assert(profile != null, "Expecting a valid profile");

            string rulesetFilePath = this.UnpackRuleset(profile);
            if (rulesetFilePath == null)
            {
                return null;
            }

            IEnumerable<string> additionalFiles = this.UnpackAdditionalFiles(profile);

            IEnumerable<string> analyzersAssemblies = this.FetchAnalyzerAssemblies(profile);

            CompilerAnalyzerConfig compilerConfig = new CompilerAnalyzerConfig(rulesetFilePath,
                analyzersAssemblies ?? Enumerable.Empty<string>(),
                additionalFiles ?? Enumerable.Empty<string>());
            return compilerConfig;
        }

        private string UnpackRuleset(RoslynExportProfile profile)
        {
            string rulesetFilePath = null;
            if (profile.Configuration.RuleSet == null)
            {
                this.logger.LogDebug(Resources.SLAP_ProfileDoesNotContainRuleset);
            }
            else
            {
                rulesetFilePath = GetRulesetFilePath(this.settings);
                this.logger.LogDebug(Resources.SLAP_UnpackingRuleset, rulesetFilePath);

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(profile.Configuration.RuleSet.OuterXml);
                doc.Save(rulesetFilePath);
            }
            return rulesetFilePath;
        }

        private static string GetRulesetFilePath(TeamBuildSettings settings)
        {
            return Path.Combine(settings.SonarConfigDirectory, RoslynCSharpRulesetFileName);
        }

        private IEnumerable<string> UnpackAdditionalFiles(RoslynExportProfile profile)
        {
            // TODO

            return null;
        }

        private IEnumerable<string> FetchAnalyzerAssemblies(RoslynExportProfile profile)
        {
            // TODO
            if (profile.Deployment != null && profile.Deployment.NuGetPackages != null)
            {
                foreach (NuGetPackageInfo p in profile.Deployment.NuGetPackages)
                {
                    // TODO: Fetch NuGet package
                }
            }
            return null;
        }


        #endregion
    }
}
