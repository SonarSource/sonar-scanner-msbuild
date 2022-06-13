/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Shim;

namespace SonarScanner.MSBuild
{
    public static class IgnoredIssuesRemover
    {
        private const string CSSettingName = "sonar.cs.roslyn.ignoreIssues";
        private const string VBSettingName = "sonar.vbnet.roslyn.ignoreIssues";
        private const string CSReportFilePaths = "sonar.cs.roslyn.reportFilePaths";
        private const string VBReportFilePaths = "sonar.vbnet.roslyn.reportFilePaths";
        private const string CSLanguageName = "cs";
        private const string VBnetLanguageName = "vbnet";
        internal const char RoslynReportPathsDelimiter = '|';

        public static void RemoveIgnoredIssues(AnalysisConfig config, ILogger logger)
        {
            var csIgnoreIssues = RetrieveSetting(config, CSSettingName, logger);
            var vbnetIgnoreIssues = RetrieveSetting(config, VBSettingName, logger);
            var csRuleIDs = RetrieveSonarRuleIDs(config, CSLanguageName);
            var vbnetRuleIDs = RetrieveSonarRuleIDs(config, VBnetLanguageName);

            if ((!csIgnoreIssues && !vbnetIgnoreIssues)
                || csRuleIDs == null
                || vbnetRuleIDs == null)
            {
                return;
            }

            var projects = ProjectLoader.LoadFrom(config.SonarOutputDir);

            foreach (var project in projects.Where(x => ShouldIgnoreIssues(x)))
            {
                RemoveIgnoredIssuess(project, csRuleIDs, vbnetRuleIDs);
            }

            bool ShouldIgnoreIssues(ProjectInfo projectInfo) =>
                (projectInfo.ProjectLanguage == ProjectLanguages.CSharp && csIgnoreIssues)
                || (projectInfo.ProjectLanguage == ProjectLanguages.VisualBasic && vbnetIgnoreIssues);
        }

        private static void RemoveIgnoredIssuess(ProjectInfo project, ISet<string> sonarCsRuleIds, ISet<string> sonarVbnetRuleIds)
        {
            string reportFilesPropertyKey;
            ISet<string> sonarRuleIds;
            if (project.ProjectLanguage == ProjectLanguages.CSharp)
            {
                reportFilesPropertyKey = CSReportFilePaths;
                sonarRuleIds = sonarCsRuleIds;
            }
            else
            {
                reportFilesPropertyKey = VBReportFilePaths;
                sonarRuleIds = sonarVbnetRuleIds;
            }

            if (project.TryGetAnalysisSetting(reportFilesPropertyKey, out var reportPathsProperty))
            {
                foreach (var reportPath in reportPathsProperty.Value.Split(RoslynReportPathsDelimiter))
                {
                    JsonIssueRemover.RemoveIgnoredIssuesFromJson(reportPath, sonarRuleIds);
                }
            }
        }

        private static bool RetrieveSetting(AnalysisConfig config, string settingName, ILogger logger)
        {
            var settingInFile = config.GetSettingOrDefault(settingName, includeServerSettings: true, defaultValue: "false");

            if (bool.TryParse(settingInFile, out var ignoreExternalRoslynIssues))
            {
                logger.LogDebug(Resources.AnalyzerSettings_ImportAllSettingValue, settingName, ignoreExternalRoslynIssues.ToString().ToLowerInvariant());
                return ignoreExternalRoslynIssues;
            }
            else
            {
                logger.LogWarning(Resources.AnalyzerSettings_InvalidValueForImportAll, settingName, settingInFile);
                return false;
            }
        }

        private static ISet<string> RetrieveSonarRuleIDs(AnalysisConfig config, string language)
        {
            var ruleIDs = new HashSet<string>();
            var languageSettings = config.AnalyzersSettings.FirstOrDefault(x => x.Language.Equals(language));
            if (languageSettings == null)
            {
                return null;
            }

            var ruleSet = RuleSet.Load(languageSettings.RulesetPath);
            foreach (var rules in ruleSet.Rules)
            {
                ruleIDs.UnionWith(rules.RuleList.Select(x => x.Id));
            }
            return ruleIDs;
        }

        private static class JsonIssueRemover
        {
            public static void RemoveIgnoredIssuesFromJson(string jsonPath, ISet<string> sonarRuleIds)
            {
                var issuesJson = JObject.Parse(File.ReadAllText(jsonPath));
                if (!issuesJson.ContainsKey("version"))
                {
                    return;
                }

                JObject fixedFile;
                bool isItChanged;
                switch (issuesJson.GetValue("version").Value<string>())
                {
                    case "0.1":
                        isItChanged = Filter0_1(issuesJson, sonarRuleIds, out fixedFile);
                        break;
                    case "0.4":
                        isItChanged = Filter0_4(issuesJson, sonarRuleIds, out fixedFile);
                        break;
                    case "1.0.0":
                        isItChanged = Filter1_0(issuesJson, sonarRuleIds, out fixedFile);
                        break;
                    default:
                        return;
                }

                if (isItChanged)
                {
                    using (var file = File.CreateText(jsonPath))
                    {
                        using (var writer = new JsonTextWriter(file))
                        {
                            writer.Formatting = Formatting.Indented;
                            fixedFile.WriteTo(writer);
                        }
                    }
                }
            }

            private static bool Filter1_0(JObject issuesJson, ISet<string> sonarRuleIds, out JObject filteredJson)
            {
                var tokensToRemove = new List<JToken>();
                if (issuesJson.ContainsKey("runs")
                    && issuesJson.GetValue("runs").First != null)
                {
                    var run = issuesJson.GetValue("runs").First;
                    if (run["results"] != null)
                    {
                        foreach (var result in run["results"])
                        {
                            if (result["ruleId"].Value<string>() != null)
                            {
                                if (!sonarRuleIds.Contains(result["ruleId"].Value<string>()))
                                {
                                    tokensToRemove.Add(result);
                                }
                            }
                        }
                    }

                    if (run["rules"] != null)
                    {
                        var rules = run["rules"];
                        foreach (var rule in rules)
                        {
                            if (rule.First != null
                                && rule.First["id"].Value<string>() != null
                                && !sonarRuleIds.Contains(rule.First["id"].Value<string>()))
                            {
                                tokensToRemove.Add(rule);
                            }
                        }
                    }
                    tokensToRemove.ForEach(x => x.Remove());
                }
                filteredJson = issuesJson;
                return tokensToRemove.Any();
            }

            private static bool Filter0_1(JObject issuesJson, ISet<string> sonarRuleIds, out JObject filteredJson)
            {
                var tokensToRemove = new List<JToken>();
                if (issuesJson.ContainsKey("issues"))
                {
                    foreach (var issue in issuesJson.GetValue("issues"))
                    {
                        if (issue["ruleId"].Value<string>() != null
                             && !sonarRuleIds.Contains(issue["ruleId"].Value<string>()))
                        {
                            tokensToRemove.Add(issue);
                        }
                    }
                    tokensToRemove.ForEach(x => x.Remove());
                }
                filteredJson = issuesJson;
                return tokensToRemove.Any();
            }

            private static bool Filter0_4(JObject issuesJson, ISet<string> sonarRuleIds, out JObject filteredJson)
            {
                var tokensToRemove = new List<JToken>();
                if (issuesJson.ContainsKey("runLogs")
                    && issuesJson.GetValue("runLogs")?.First["results"] != null)
                {
                    foreach (var result in issuesJson.GetValue("runLogs").First["results"])
                    {
                        if (result["ruleId"].Value<string>() != null
                            && !sonarRuleIds.Contains(result["ruleId"].Value<string>()))
                        {
                            tokensToRemove.Add(result);
                        }
                    }
                    tokensToRemove.ForEach(x => x.Remove());
                }
                filteredJson = issuesJson;
                return tokensToRemove.Any();
            }
        }
    }
}
