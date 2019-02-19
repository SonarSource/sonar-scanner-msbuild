/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.Tasks
{
    /// <summary>
    /// Build task to return the Roslyn analyzer settings from the analysis config file
    /// </summary>
    public class GetAnalyzerSettings : Task
    {

        private readonly string[] SonarDotNetPluginKeys = new string[] { "csharp", "vbnet" };

        private const string DllExtension = ".dll";

        #region Input properties

        /// <summary>
        /// The directory containing the analysis config settings file
        /// </summary>
        [Required]
        public string AnalysisConfigDir { get; set; }

        /// <summary>
        /// List of analyzers that would be passed to the compiler if
        /// no SonarQube analysis was happening.
        /// </summary>
        [Required]
        public string[] OriginalAnalyzers { get; set; }

        /// <summary>
        /// List of additional files that would be passed to the compiler if
        /// no SonarQube analysis was happening.
        /// </summary>
        [Required]
        public string[] OriginalAdditionalFiles { get; set; }

        /// <summary>
        /// Original ruleset specified in the project, if any
        /// </summary>
        public string OriginalRulesetFilePath { get; set; }

        [Required]
        /// <summary>
        /// Path to the directory containing the project being built
        /// </summary>
        public string CurrentProjectDirectoryPath { get; set; }

        /// <summary>
        /// Project-specific directory into which new output files can be written
        /// (e.g. a new project-specific ruleset file)
        /// </summary>
        [Required]
        public string ProjectSpecificConfigDirectory { get; set; }

        /// <summary>
        /// Indicates whether the current project is a test project or product project
        /// </summary>
        [Required]
        public bool IsTestProject { get; set; }

        /// <summary>
        /// The language for which we are gettings the settings
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Path to the generated ruleset file to use
        /// </summary>
        [Output]
        public string RuleSetFilePath { get; private set; }

        /// <summary>
        /// List of analyzer assemblies and dependencies to pass to the compiler as analyzers
        /// </summary>
        [Output]
        public string[] AnalyzerFilePaths { get; private set; }

        /// <summary>
        /// List of additional files to pass to the compiler
        /// </summary>
        [Output]
        public string[] AdditionalFilePaths { get; private set; }

        #endregion Input properties

        #region Overrides

        public override bool Execute()
        {
            var logger = new MSBuildLoggerAdapter(Log);
            var config = TaskUtilities.TryGetConfig(AnalysisConfigDir, logger);

            var languageSettings = GetLanguageSpecificSettings(config);
            if (languageSettings == null)
            {
                // Early-out: we don't have any settings for the current language.
                // Preserve the default existing behaviour of only preserving the original list of additional files
                // but clearing the analyzers.
                RuleSetFilePath = null;
                AdditionalFilePaths = OriginalAdditionalFiles;
                return !Log.HasLoggedErrors;
            }

            TaskOutputs outputs = null;
            if (IsTestProject)
            {
                // Special case: to provide colorization etc for code in test projects, we need
                // to run only the SonarC#/VB analyzers, with all of the non-utility rules turned off
                // See [MMF-486]: https://jira.sonarsource.com/browse/MMF-486
                Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_ConfiguringTestProjectAnalysis);
                outputs = CreateTestProjectSettings(languageSettings);
            }
            else
            {
                if (ShouldMergeAnalysisSettings(Language, config, logger))
                {
                    Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_MergingSettings);
                    outputs = CreateMergedAnalyzerSettings(languageSettings);
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_OverwritingSettings);
                    outputs = CreateLegacyProductProjectSettings(languageSettings);
                }
            }

            ApplyTaskOutput(outputs);

            return !Log.HasLoggedErrors;
        }

        #endregion Overrides

        #region Private methods

        internal /* for testing */ static bool ShouldMergeAnalysisSettings(string language, AnalysisConfig config,
            SonarScanner.MSBuild.Common.ILogger logger)
        {
            Debug.Assert(!string.IsNullOrEmpty(language));
            Debug.Assert(config != null);

            // See https://github.com/SonarSource/sonar-scanner-msbuild/issues/561
            // Legacy behaviour is to overwrite.
            // The new (SQ 7.4+) behaviour is to merge only if sonar.[LANGUAGE].roslyn.ignoreIssues is true.
            var serverVersion = config?.FindServerVersion();
            if (serverVersion == null || serverVersion < new Version("7.4"))
            {
                logger.LogInfo(Resources.AnalyzerSettings_ExternalIssueNotSupported);
                return false;
            }

            var settingName = $"sonar.{language}.roslyn.ignoreIssues";
            var settingInFile = config.GetSettingOrDefault(settingName,
                includeServerSettings: true, defaultValue: "true");

            if (bool.TryParse(settingInFile, out var ignoreExternalRoslynIssues))
            {
                logger.LogDebug(Resources.AnalyzerSettings_ImportAllSettingValue, settingName,
                    ignoreExternalRoslynIssues.ToString().ToLowerInvariant());
                return !ignoreExternalRoslynIssues;
            }
            else
            {
                logger.LogWarning(Resources.AnalyzerSettings_InvalidValueForImportAll, settingName, settingInFile);
                return false;
            }
        }

        private TaskOutputs CreateTestProjectSettings(AnalyzerSettings settings)
        {
            var sonarDotNetAnalyzers = settings.AnalyzerPlugins
                    .Where(p => SonarDotNetPluginKeys.Contains(p.Key, StringComparer.OrdinalIgnoreCase))
                    .SelectMany(p => p.AssemblyPaths);

            return new TaskOutputs(settings.TestProjectRuleSetFilePath, sonarDotNetAnalyzers, settings.AdditionalFilePaths);
        }

        private TaskOutputs CreateLegacyProductProjectSettings(AnalyzerSettings settings)
        {
            var configOnlyAnalyzers = settings.AnalyzerPlugins.SelectMany(p => p.AssemblyPaths);
            var additionalFilePaths = MergeFileLists(settings.AdditionalFilePaths, OriginalAdditionalFiles);

            return new TaskOutputs(settings.RuleSetFilePath, configOnlyAnalyzers, additionalFilePaths);
        }

        private TaskOutputs CreateMergedAnalyzerSettings(AnalyzerSettings settings)
        {
            var mergedRuleset = CreateMergedRuleset(settings);
            var allAnalyzers = MergeFileLists(settings.AnalyzerPlugins.SelectMany(ap => ap.AssemblyPaths), OriginalAnalyzers);
            var additionalFilePaths = MergeFileLists(settings.AdditionalFilePaths, OriginalAdditionalFiles);

            return new TaskOutputs(mergedRuleset, allAnalyzers, additionalFilePaths);
        }
   
        private string CreateMergedRuleset(AnalyzerSettings languageSpecificSettings)
        {
            // The original ruleset should have been provided to the task.
            // This should never be null when using the default targets
            // (if the user hasn't specified anything then it will be the
            // Microsoft minimum recommended tooleset).
            // However, we'll be defensive and handle nulls in case the
            // user has customised their build.
            if (OriginalRulesetFilePath == null)
            {
                // If the project doesn't already have a ruleset can just
                // return the generated one directly
                Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_OriginalRulesetNotSpecified, languageSpecificSettings.RuleSetFilePath);
                return languageSpecificSettings.RuleSetFilePath;
            }

            var resolvedRulesetPath = GetAbsoluteRulesetPath();

            var mergedRulesetFilePath = Path.Combine(ProjectSpecificConfigDirectory, "merged.ruleset");
            Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_CreatingMergedRuleset, mergedRulesetFilePath);

            WriteMergedRuleSet(resolvedRulesetPath, languageSpecificSettings.RuleSetFilePath, mergedRulesetFilePath);
            return mergedRulesetFilePath;
        }

        private string GetAbsoluteRulesetPath()
        {
            // If the supplied ruleset path is relative then it is relative to the project folder.
            // This relative path will be wrong if use it directly in the generated merged ruleset
            // file so we need to make it absolute.
            string resolvedRulesetFilePath;
            if (Path.IsPathRooted(OriginalRulesetFilePath))
            {
                Log.LogMessage(MessageImportance.Low, $"Supplied ruleset path is rooted: {OriginalRulesetFilePath}");
                resolvedRulesetFilePath = OriginalRulesetFilePath;
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, $"Supplied ruleset path is not rooted: {OriginalRulesetFilePath}");
                resolvedRulesetFilePath = Path.GetFullPath(Path.Combine(CurrentProjectDirectoryPath, OriginalRulesetFilePath));
            }

            Log.LogMessage(MessageImportance.Low,
                File.Exists(resolvedRulesetFilePath) ? Resources.AnalyzerSettings_ResolvedRulesetFound : Resources.AnalyzerSettings_ResolvedRulesetNotFound,
                resolvedRulesetFilePath);
            return resolvedRulesetFilePath;
        }

        private static void WriteMergedRuleSet(string originalRuleset, string languageRuleset, string mergedRulesetFilePath)
        {
            // We want the QP ruleset settings to take precedence over any conflicting settings
            // in the local ruleset. The only way to do this is to make a copy of the QP ruleset
            // and "Include" the local ruleset in it.
            // See bug https://github.com/SonarSource/sonar-scanner-msbuild/issues/581
            using (var reader = new StreamReader(languageRuleset))
            {
                var xdoc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);

                // This will fail if the ruleset is invalid. However, we generated the
                // ruleset so something else is already wrong in that case.
                var rulesetNode = xdoc.Descendants().First(e => e.Name == "RuleSet");

                var importElement = new XElement("Include");
                importElement.Add(new XAttribute("Path", originalRuleset));
                importElement.Add(new XAttribute("Action", "Default"));

                rulesetNode.AddFirst(importElement);
                xdoc.Save(mergedRulesetFilePath);
            }
        }

        private AnalyzerSettings GetLanguageSpecificSettings(AnalysisConfig config)
        {
            if (config == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(Language))
            {
                Log.LogMessage(Resources.AnalyzerSettings_LanguageNotSpecified);
                return null;
            }
            
            IList<AnalyzerSettings> analyzers = config.AnalyzersSettings;
            if (analyzers == null)
            {
                Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_NotSpecifiedInConfig, Language);
                return null;
            }

            var settings = analyzers.SingleOrDefault(s => Language.Equals(s.Language));
            if (settings == null)
            {
                Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_NotSpecifiedInConfig, Language);
                return null;
            }
            return settings;
        }

        /// <summary>
        /// Merges and returns the supplied list of file paths. In case of duplicate
        /// // file *names* (not full paths), the path from the primary list is used.
        /// </summary>
        private string[] MergeFileLists(IEnumerable<string> primaryList, IEnumerable<string> secondaryList)
        {
            var nonNullPrimary = primaryList ?? Enumerable.Empty<string>();
            var nonNullSecondary = secondaryList ?? Enumerable.Empty<string>();

            var duplicates = GetEntriesWithMatchingFileNames(nonNullPrimary, nonNullSecondary);
            var finalList = nonNullPrimary
                .Union(nonNullSecondary)
                .Except(duplicates)
                .ToArray();

            Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_RemovingDuplicateFiles, string.Join(", ", duplicates) ?? "{none}");
            return finalList;
        }

        /// <summary>
        /// Returns the entries from <paramref name="candidateFilePaths"/> where the file name
        /// part of the candidate matches the file name of an entry in <paramref name="sourceFilePaths"/>
        /// </summary>
        private static string[] GetEntriesWithMatchingFileNames(IEnumerable<string> sourceFilePaths, IEnumerable<string> candidateFilePaths)
        {
            Debug.Assert(sourceFilePaths != null);
            Debug.Assert(candidateFilePaths != null);

            var sourceFileNames = new HashSet<string>(
                sourceFilePaths
                    .Select(sfp => GetFileName(sfp))
                    .Where(n => !string.IsNullOrEmpty(n)));

            var matches = candidateFilePaths
                .Where(candidate => sourceFileNames.Contains(GetFileName(candidate), StringComparer.OrdinalIgnoreCase))
                .ToArray();

            return matches;
        }

        private static string GetFileName(string path)
        {
            try
            {
                return Path.GetFileName(path)?.ToUpperInvariant();
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static string[] RemoveNonAnalyzerFiles(IEnumerable<string> files) =>
            files.Where(f => IsAssemblyLibraryFileName(f)).ToArray();

        /// <summary>
        /// Returns whether the supplied string is an assembly library (i.e. dll)
        /// </summary>
        private static bool IsAssemblyLibraryFileName(string filePath)
        {
            // Not expecting .winmd or .exe files to contain Roslyn analyzers
            // so we'll ignore them
            return filePath.EndsWith(DllExtension, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyTaskOutput(TaskOutputs outputs)
        {
            RuleSetFilePath = outputs.Ruleset;
            AnalyzerFilePaths = RemoveNonAnalyzerFiles(outputs.AssemblyPaths);
            AdditionalFilePaths = outputs.AdditionalFilePaths;
        }

        #endregion Private methods

        /// <summary>
        /// Internal data class to hold the set of output values for this task
        /// </summary>
        private class TaskOutputs
        {
            public TaskOutputs(string ruleset, IEnumerable<string> assemblyPaths, IEnumerable<string> additionalFilePaths)
            {
                Ruleset = ruleset;
                AssemblyPaths = assemblyPaths?.ToArray() ?? new string[] { };
                AdditionalFilePaths = additionalFilePaths?.ToArray() ?? new string[] { };
            }

            public string Ruleset { get; }
            public string[] AssemblyPaths { get; }
            public string[] AdditionalFilePaths { get; }
        }
    }
}
