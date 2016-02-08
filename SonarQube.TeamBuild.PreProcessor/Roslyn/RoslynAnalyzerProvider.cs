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
    public class RoslynAnalyzerProvider : IAnalyzerProvider
    {
        public const string RoslynCSharpFormatName = "roslyn-cs";
        public const string RoslynCSharpRulesetFileName = "SonarQubeRoslyn-cs.ruleset";

        public const string CSharpLanguage = "cs";
        public const string CSharpPluginKey = "csharp";
        public const string CSharpRepositoryKey = "csharp";

        private readonly IAnalyzerInstaller analyzerInstaller;
        private readonly ILogger logger;
        private ISonarQubeServer server;
        private TeamBuildSettings settings;
        private string projectKey;
        private string projectBranch;

        #region Public methods

        public RoslynAnalyzerProvider(IAnalyzerInstaller analyzerInstaller, ILogger logger)
        {
            if (analyzerInstaller == null)
            {
                throw new ArgumentNullException("analyzerInstaller");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            this.analyzerInstaller = analyzerInstaller;
            this.logger = logger;
        }

        public AnalyzerSettings SetupAnalyzers(ISonarQubeServer sqServer, TeamBuildSettings teamBuildSettings, string sqProjectKey, string sqProjectBranch)
        {
            if (sqServer == null)
            {
                throw new ArgumentNullException("server");
            }
            if (teamBuildSettings == null)
            {
                throw new ArgumentNullException("settings");
            }
            if (string.IsNullOrWhiteSpace(sqProjectKey))
            {
                throw new ArgumentNullException("projectKey");
            }

            AnalyzerSettings analyzerSettings = null;
            if (IsCSharpPluginInstalled(sqServer))
            {
                this.server = sqServer;
                this.settings = teamBuildSettings;
                this.projectKey = sqProjectKey;
                this.projectBranch = sqProjectBranch;

                RoslynExportProfile profile = TryGetRoslynConfigForProject();
                if (profile != null)
                {
                    analyzerSettings = ProcessProfile(profile);
                }
            }
            else
            {
                logger.LogDebug(Resources.RAP_CSharpPluginNotInstalled);
            }

            return analyzerSettings ?? new AnalyzerSettings(); // return emtpy settings rather than null
        }

        #endregion

        #region Private methods

        private static bool IsCSharpPluginInstalled(ISonarQubeServer server)
        {
            return server.GetInstalledPlugins().Contains(CSharpPluginKey);
        }
        
        private RoslynExportProfile TryGetRoslynConfigForProject()
        {
            string qualityProfile;
            if (!this.server.TryGetQualityProfile(projectKey, projectBranch, CSharpLanguage, out qualityProfile))
            {
                this.logger.LogDebug(Resources.RAP_NoProfileForProject, this.projectKey);
                return null;
            }

            string profileContent = null;
            if (!server.TryGetProfileExport(qualityProfile, CSharpLanguage, RoslynCSharpFormatName, out profileContent))
            {
                this.logger.LogDebug(Resources.RAP_ProfileExportNotFound, RoslynCSharpFormatName, this.projectKey);
                return null;
            }
            this.logger.LogDebug(Resources.RAP_ProfileExportFound, RoslynCSharpFormatName, this.projectKey);

            RoslynExportProfile profile = null;
            using (StringReader reader = new StringReader(profileContent))
            {
                profile = RoslynExportProfile.Load(reader);
            }

            return profile;
        }

        private AnalyzerSettings ProcessProfile(RoslynExportProfile profile)
        {
            Debug.Assert(profile != null, "Expecting a valid profile");

            string rulesetFilePath = this.UnpackRuleset(profile);
            if (rulesetFilePath == null)
            {
                return null;
            }

            IEnumerable<string> additionalFiles = this.UnpackAdditionalFiles(profile);

            IEnumerable<string> analyzersAssemblies = this.FetchAnalyzerAssemblies(profile);

            AnalyzerSettings compilerConfig = new AnalyzerSettings(rulesetFilePath,
                analyzersAssemblies ?? Enumerable.Empty<string>(),
                additionalFiles ?? Enumerable.Empty<string>());
            return compilerConfig;
        }

        private string UnpackRuleset(RoslynExportProfile profile)
        {
            string rulesetFilePath = null;
            if (profile.Configuration.RuleSet == null)
            {
                this.logger.LogDebug(Resources.RAP_ProfileDoesNotContainRuleset);
            }
            else
            {
                rulesetFilePath = GetRulesetFilePath(this.settings);
                this.logger.LogDebug(Resources.RAP_UnpackingRuleset, rulesetFilePath);

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
            Debug.Assert(profile.Configuration != null, "Supplied configuration should not be null");

            List<string> additionalFiles = new List<string>();
            foreach(AdditionalFile item in profile.Configuration.AdditionalFiles)
            {
                string filePath = ProcessAdditionalFile(item);
                if (filePath != null)
                {
                    Debug.Assert(File.Exists(filePath), "Expecting the additional file to exist: {0}", filePath);
                    additionalFiles.Add(filePath);
                }
            }

            return additionalFiles;
        }

        private string ProcessAdditionalFile(AdditionalFile file)
        {
            if (string.IsNullOrWhiteSpace(file.FileName))
            {
                this.logger.LogDebug(Resources.RAP_AdditionalFileNameMustBeSpecified);
                return null;
            }

            string fullPath = Path.Combine(this.settings.SonarConfigDirectory, file.FileName);
            if (File.Exists(fullPath))
            {
                this.logger.LogDebug(Resources.RAP_AdditionalFileAlreadyExists, file.FileName, fullPath);
                return null;
            }

            this.logger.LogDebug(Resources.RAP_WritingAdditionalFile, fullPath);
            File.WriteAllBytes(fullPath, file.Content ?? new byte[] { });
            return fullPath;
        }
        
        private IEnumerable<string> FetchAnalyzerAssemblies(RoslynExportProfile profile)
        {
            IEnumerable<string> analyzerAssemblyPaths = null;
            if (profile.Deployment == null || profile.Deployment.Plugins == null || profile.Deployment.Plugins.Count == 0)
            {
                this.logger.LogInfo(Resources.RAP_NoAnalyzerPluginsSpecified);
            }
            else
            {
                this.logger.LogInfo(Resources.RAP_ProvisioningAnalyzerAssemblies);
                analyzerAssemblyPaths = this.analyzerInstaller.InstallAssemblies(profile.Deployment.Plugins);
            }
            return analyzerAssemblyPaths;
        }

        #endregion
    }
}
