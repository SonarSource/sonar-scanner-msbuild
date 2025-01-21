/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto: info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.Roslyn;

public class RoslynAnalyzerProvider : IAnalyzerProvider
{
    public const string RulesetFileNameNormal = "Sonar-{0}.ruleset";
    public const string RulesetFileNameNone = "Sonar-{0}-none.ruleset";

    private const string SONARANALYZER_PARTIAL_REPO_KEY_PREFIX = "sonaranalyzer-";
    private const string SONARANALYZER_PARTIAL_REPO_KEY = SONARANALYZER_PARTIAL_REPO_KEY_PREFIX + "{0}";
    private const string ROSLYN_REPOSITORY_PREFIX = "roslyn.";

    public const string CSharpLanguage = "cs";
    public const string VBNetLanguage = "vbnet";

    private readonly IAnalyzerInstaller analyzerInstaller;
    private readonly ILogger logger;
    private BuildSettings teamBuildSettings;
    private IAnalysisPropertyProvider sonarProperties;

    public RoslynAnalyzerProvider(IAnalyzerInstaller analyzerInstaller, ILogger logger)
    {
        this.analyzerInstaller = analyzerInstaller ?? throw new ArgumentNullException(nameof(analyzerInstaller));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates several files related to rulesets and roslyn analyzer assemblies.
    /// Active rules should never be empty, but depending on the server settings of repo keys, we might have no rules in the ruleset.
    /// In that case, this method returns null.
    /// </summary>
    public AnalyzerSettings SetupAnalyzer(BuildSettings teamBuildSettings, IAnalysisPropertyProvider sonarProperties, IEnumerable<SonarRule> rules, string language)
    {
        this.teamBuildSettings = teamBuildSettings ?? throw new ArgumentNullException(nameof(teamBuildSettings));
        this.sonarProperties = sonarProperties ?? throw new ArgumentNullException(nameof(sonarProperties));
        _ = language ?? throw new ArgumentNullException(nameof(language));
        _ = rules ?? throw new ArgumentNullException(nameof(rules));

        var rulesetPath = CreateRuleSet(language, rules, false);
        var deactivatedRulesetPath = CreateRuleSet(language, rules, true);
        var analyzerPlugins = FetchAnalyzerPlugins(language, rules.Where(x => x.IsActive));
        var additionalFiles = WriteAdditionalFiles(language, rules.Where(x => x.IsActive));

        return new AnalyzerSettings(language, rulesetPath, deactivatedRulesetPath, analyzerPlugins, additionalFiles);
    }

    private string CreateRuleSet(string language, IEnumerable<SonarRule> rules, bool deactivateAll)
    {
        var ruleSetGenerator = new RoslynRuleSetGenerator(this.sonarProperties, deactivateAll);
        var ruleSet = ruleSetGenerator.Generate(language, rules);
        Debug.Assert(ruleSet != null, "Expecting the RuleSet to be created.");
        Debug.Assert(ruleSet.Rules != null, "Expecting the RuleSet.Rules to be initialized.");

        var rulesetFilePath = Path.Combine(this.teamBuildSettings.SonarConfigDirectory, string.Format(deactivateAll ? RulesetFileNameNone : RulesetFileNameNormal, language));
        this.logger.LogDebug(Resources.RAP_UnpackingRuleset, rulesetFilePath);

        ruleSet.Save(rulesetFilePath);

        return rulesetFilePath;
    }

    private IEnumerable<string> WriteAdditionalFiles(string language, IEnumerable<SonarRule> activeRules)
    {
        Debug.Assert(activeRules != null, "Supplied active rules should not be null");

        if (TryWriteSonarLintXmlFile(language, activeRules, out var filePath))
        {
            Debug.Assert(File.Exists(filePath), "Expecting the additional file to exist: {0}", filePath);
            return new[] { filePath };
        }

        return Enumerable.Empty<string>();
    }

    private bool TryWriteSonarLintXmlFile(string language, IEnumerable<SonarRule> activeRules, out string sonarLintXmlPath)
    {
        var langDir = Path.Combine(this.teamBuildSettings.SonarConfigDirectory, language);
        Directory.CreateDirectory(langDir);

        sonarLintXmlPath = Path.Combine(langDir, "SonarLint.xml");
        if (File.Exists(sonarLintXmlPath))
        {
            this.logger.LogDebug(Resources.RAP_AdditionalFileAlreadyExists, language, sonarLintXmlPath);
            return false;
        }

        var content = RoslynSonarLint.GenerateXml(activeRules, this.sonarProperties, language);
        this.logger.LogDebug(Resources.RAP_WritingAdditionalFile, sonarLintXmlPath);
        File.WriteAllText(sonarLintXmlPath, content);
        return true;
    }

    private IEnumerable<AnalyzerPlugin> FetchAnalyzerPlugins(string language, IEnumerable<SonarRule> activeRules)
    {
        var partialRepoKeys = ActiveRulesPartialRepoKeys(activeRules);
        IList<Plugin> plugins = new List<Plugin>();

        foreach (var partialRepoKey in partialRepoKeys)
        {
            if (!this.sonarProperties.TryGetValue($"{partialRepoKey}.pluginKey", out var pluginKey) ||
                !this.sonarProperties.TryGetValue($"{partialRepoKey}.pluginVersion", out var pluginVersion) ||
                !this.sonarProperties.TryGetValue($"{partialRepoKey}.staticResourceName", out var staticResourceName))
            {
                if (!partialRepoKey.StartsWith(SONARANALYZER_PARTIAL_REPO_KEY_PREFIX))
                {
                    this.logger.LogInfo(Resources.RAP_NoAssembliesForRepo, partialRepoKey, language);
                }
                continue;
            }

            plugins.Add(new Plugin(pluginKey, pluginVersion, staticResourceName));
        }

        if (plugins.Count == 0)
        {
            this.logger.LogInfo(Resources.RAP_NoAnalyzerPluginsSpecified, language);
            return Enumerable.Empty<AnalyzerPlugin>();
        }
        else
        {
            this.logger.LogInfo(Resources.RAP_ProvisioningAnalyzerAssemblies, language);
            return this.analyzerInstaller.InstallAssemblies(plugins);
        }
    }

    private static ICollection<string> ActiveRulesPartialRepoKeys(IEnumerable<SonarRule> rules)
    {
        var partialRepoKeys = new HashSet<string>
        {
            // Always add SonarC# and SonarVB to have at least tokens...
            string.Format(SONARANALYZER_PARTIAL_REPO_KEY, "cs"),
            string.Format(SONARANALYZER_PARTIAL_REPO_KEY, "vbnet")
        };

        // Add the Roslyn SDK rules' partial repo keys, if any
        partialRepoKeys.UnionWith(
            rules
                .Where(rule => rule.RepoKey.StartsWith(ROSLYN_REPOSITORY_PREFIX))
                .Select(rule => rule.RepoKey.Substring(ROSLYN_REPOSITORY_PREFIX.Length)));

        return partialRepoKeys;
    }
}
