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
        public const string RoslynFormatNamePrefix = "roslyn-{0}";
        public const string RoslynRulesetFileName = "SonarQubeRoslyn-{0}.ruleset";

        public const string CSharpLanguage = "cs";
        public const string CSharpPluginKey = "csharp";
        public const string CSharpRepositoryKey = "csharp";

        public const string VBNetLanguage = "vbnet";
        public const string VBNetPluginKey = "vbnet";
        public const string VBNetRepositoryKey = "vbnet";

        private readonly IAnalyzerInstaller analyzerInstaller;
        private readonly ILogger logger;
        private ISonarQubeServer sqServer;
        private TeamBuildSettings sqSettings;
        private string sqProjectKey;
        private string sqProjectBranch;

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

        public IEnumerable<AnalyzerSettings> SetupAnalyzers(ISonarQubeServer server, TeamBuildSettings settings, string projectKey, string projectBranch)
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

            this.sqServer = server;
            this.sqSettings = settings;
            this.sqProjectKey = projectKey;
            this.sqProjectBranch = projectBranch;

            IList<AnalyzerSettings> analyzersSettings = new List<AnalyzerSettings>();
            IEnumerable<string> installedPlugins = server.GetInstalledPlugins();

            if (installedPlugins.Contains(CSharpPluginKey))
            {
                RoslynExportProfile profile = TryGetRoslynConfigForProject(CSharpLanguage);
                if (profile != null)
                {
                    analyzersSettings.Add(ProcessExportedProfile(profile, CSharpLanguage));
                }
            }
            if (installedPlugins.Contains(VBNetPluginKey))
            {
                RoslynExportProfile profile = TryGetRoslynConfigForProject(VBNetLanguage);
                if (profile != null)
                {
                    analyzersSettings.Add(ProcessExportedProfile(profile, VBNetLanguage));
                }
            }

            if (!analyzersSettings.Any())
            {
                logger.LogDebug(Resources.RAP_NoPluginInstalled);
            }

            return analyzersSettings;
        }

        public static string GetRoslynFormatName(string language)
        {
            return string.Format(RoslynFormatNamePrefix, language);
        }

        public static string GetRoslynRulesetFileName(string language)
        {
            return string.Format(RoslynRulesetFileName, language);
        }

        #endregion

        #region Private methods

        private RoslynExportProfile TryGetRoslynConfigForProject(string language)
        {
            string roslynFormatName = GetRoslynFormatName(language);
            string qualityProfile;

            if (!this.sqServer.TryGetQualityProfile(sqProjectKey, sqProjectBranch, language, out qualityProfile))
            {
                this.logger.LogDebug(Resources.RAP_NoProfileForProject, language, this.sqProjectKey);
                return null;
            }

            string profileContent = null;
            if (!sqServer.TryGetProfileExport(qualityProfile, language, roslynFormatName, out profileContent))
            {
                this.logger.LogDebug(Resources.RAP_ProfileExportNotFound, roslynFormatName, this.sqProjectKey);
                return null;
            }
            this.logger.LogDebug(Resources.RAP_ProfileExportFound, roslynFormatName, this.sqProjectKey);

            RoslynExportProfile profile = null;
            using (StringReader reader = new StringReader(profileContent))
            {
                profile = RoslynExportProfile.Load(reader);
            }

            return profile;
        }

        private AnalyzerSettings ProcessExportedProfile(RoslynExportProfile profile, string language)
        {
            Debug.Assert(profile != null, "Expecting a valid profile");

            string rulesetFilePath = this.UnpackRuleset(profile, language);
            if (rulesetFilePath == null)
            {
                return null;
            }

            IEnumerable<string> additionalFiles = this.UnpackAdditionalFiles(profile, language);
            IEnumerable<string> analyzersAssemblies = this.FetchAnalyzerAssemblies(profile, language);

            AnalyzerSettings compilerConfig = new AnalyzerSettings(language, rulesetFilePath,
                analyzersAssemblies ?? Enumerable.Empty<string>(),
                additionalFiles ?? Enumerable.Empty<string>());
            return compilerConfig;
        }

        private string UnpackRuleset(RoslynExportProfile profile, string language)
        {
            string rulesetFilePath = null;
            if (profile.Configuration.RuleSet == null)
            {
                this.logger.LogDebug(Resources.RAP_ProfileDoesNotContainRuleset);
            }
            else
            {
                rulesetFilePath = GetRulesetFilePath(this.sqSettings, language);
                this.logger.LogDebug(Resources.RAP_UnpackingRuleset, rulesetFilePath);

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(profile.Configuration.RuleSet.OuterXml);
                doc.Save(rulesetFilePath);
            }
            return rulesetFilePath;
        }

        private static string GetRulesetFilePath(TeamBuildSettings settings, string language)
        {
            return Path.Combine(settings.SonarConfigDirectory, GetRoslynRulesetFileName(language));
        }

        private IEnumerable<string> UnpackAdditionalFiles(RoslynExportProfile profile, string language)
        {
            Debug.Assert(profile.Configuration != null, "Supplied configuration should not be null");

            List<string> additionalFiles = new List<string>();
            foreach(AdditionalFile item in profile.Configuration.AdditionalFiles)
            {
                string filePath = ProcessAdditionalFile(item, language);
                if (filePath != null)
                {
                    Debug.Assert(File.Exists(filePath), "Expecting the additional file to exist: {0}", filePath);
                    additionalFiles.Add(filePath);
                }
            }

            return additionalFiles;
        }

        private string ProcessAdditionalFile(AdditionalFile file, string language)
        {
            if (string.IsNullOrWhiteSpace(file.FileName))
            {
                this.logger.LogDebug(Resources.RAP_AdditionalFileNameMustBeSpecified);
                return null;
            }

            string langDir = Path.Combine(this.sqSettings.SonarConfigDirectory, language);
            Directory.CreateDirectory(langDir);

            string fullPath = Path.Combine(langDir, file.FileName);
            if (File.Exists(fullPath))
            {
                this.logger.LogDebug(Resources.RAP_AdditionalFileAlreadyExists, file.FileName, fullPath);
                return null;
            }

            this.logger.LogDebug(Resources.RAP_WritingAdditionalFile, fullPath);
            File.WriteAllBytes(fullPath, file.Content ?? new byte[] { });
            return fullPath;
        }
        
        private IEnumerable<string> FetchAnalyzerAssemblies(RoslynExportProfile profile, string language)
        {
            IEnumerable<string> analyzerAssemblyPaths = null;
            if (profile.Deployment == null || profile.Deployment.Plugins == null || profile.Deployment.Plugins.Count == 0)
            {
                this.logger.LogInfo(Resources.RAP_NoAnalyzerPluginsSpecified, language);
            }
            else
            {
                this.logger.LogInfo(Resources.RAP_ProvisioningAnalyzerAssemblies, language);
                analyzerAssemblyPaths = this.analyzerInstaller.InstallAssemblies(profile.Deployment.Plugins);
            }
            return analyzerAssemblyPaths;
        }

        #endregion
    }
}
