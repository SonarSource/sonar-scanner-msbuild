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
using System.IO;
using System.Linq;
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
        private const string DllExtension = ".dll";

        #region Input properties

        /// <summary>
        /// The directory containing the analysis config settings file
        /// </summary>
        [Required]
        public string AnalysisConfigDir { get; set; }

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

        /// <summary>
        /// Project-specific directory into which new output files can be written
        /// (e.g. a new project-specific ruleset file)
        /// </summary>
        [Required]
        public string ProjectSpecificOutputDirectory { get; set; }

        /// <summary>
        /// The language for which we are gettings the settings
        /// </summary>
        [Required]
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
        public string[] AdditionalFiles { get; private set; }

        /// <summary>
        /// List of additional files that are originally passed to the compiler,
        /// but <see cref="AdditionalFiles"/> contains an explicit override for them.
        /// </summary>
        [Output]
        public string[] AdditionalFilesToRemove { get; private set; }

        #endregion Input properties

        #region Overrides

        public override bool Execute()
        {
            var config = TaskUtilities.TryGetConfig(AnalysisConfigDir, new MSBuildLoggerAdapter(Log));

            if (ShouldMergeAnalysisSettings(config))
            {
                MergeAnalysisSettings(config);
            }
            else
            {
                OverrideAnalysisSettings(config);
            }

            return !Log.HasLoggedErrors;
        }

        #endregion Overrides

        #region Private methods

        internal /* for testing */ static bool ShouldMergeAnalysisSettings(AnalysisConfig config)
        {
            // See https://github.com/SonarSource/sonar-scanner-msbuild/issues/561
            // Legacy behaviour is to overwrite. The only time we don't is if the
            // we are using SQ 7.5 or greater and sonar.roslyn.importAllIssues is not 
            // set or is true.
            var serverVersion = config?.FindServerVersion();
            if (serverVersion != null && serverVersion >= new Version("7.5"))
            {
                var settingInFile = config.GetSettingOrDefault("sonar.roslyn.importAllIssues", true, "true");
                if (Boolean.TryParse(settingInFile, out var includeInFile))
                {
                    return includeInFile;
                }
            }
            return false;
        }

        private void OverrideAnalysisSettings(AnalysisConfig config)
        {
            Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_OverwritingSettings);

            if (config == null || Language == null)
            {
                return;
            }

            var settings = GetLanguageSpecificSettings(config);
            if (settings == null)
            {
                // Early-out: we don't have any settings for the current language, 
                // so we'll just wipe out the current settings without replacing
                // them with new ones
                return;
            }

            RuleSetFilePath = settings.RuleSetFilePath;

            if (settings.AnalyzerAssemblyPaths != null)
            {
                AnalyzerFilePaths = settings.AnalyzerAssemblyPaths.Where(f => IsAssemblyLibraryFileName(f)).ToArray();
            }

            if (settings.AdditionalFilePaths != null)
            {
                AdditionalFiles = settings.AdditionalFilePaths.ToArray();

                var additionalFileNames = new HashSet<string>(
                    AdditionalFiles
                        .Select(af => GetFileName(af))
                        .Where(n => !string.IsNullOrEmpty(n)));

                AdditionalFilesToRemove = (OriginalAdditionalFiles ?? Enumerable.Empty<string>())
                    .Where(original => additionalFileNames.Contains(GetFileName(original)))
                    .ToArray();
            }
        }

        private void MergeAnalysisSettings(AnalysisConfig config)
        {
            Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_MergingSettings);

            var settings = GetLanguageSpecificSettings(config);
            if (settings == null)
            {
                // Early-out: we don't have any settings for the current language
                // so don't change the supplied settings
                RuleSetFilePath = OriginalRulesetFilePath;
                AdditionalFiles = OriginalAdditionalFiles;
                return;
            }

            RuleSetFilePath = CreateMergedRuleset(settings);

            // TODO - merge analyzers
            // TODO - merge additional files
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

            var mergedRulesetFilePath = Path.Combine(ProjectSpecificOutputDirectory, "merged.ruleset");
            Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_CreatingMergedRuleset, mergedRulesetFilePath);

            var content = GetMergedRuleSetContent(OriginalRulesetFilePath, languageSpecificSettings.RuleSetFilePath);

            File.WriteAllText(mergedRulesetFilePath, content);
            return mergedRulesetFilePath;
        }

        private static string GetMergedRuleSetContent(string originalRuleset, string languageRuleset) =>
            // Template for the merged ruleset
            // * the first ruleset is the one specified by the user. We need to set the action to warning
            //   so the build won't fail if issues
            // * the second ruleset is the language-specific one generated by the Begin step from the 
            //   quality profile.
            $@"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""RulesetName"" ToolsVersion=""14.0"">
  <Include Path=""{originalRuleset}"" Action=""Warning"" />
  <Include Path=""{languageRuleset}"" Action = ""Default"" />
</RuleSet>";

        private AnalyzerSettings GetLanguageSpecificSettings(AnalysisConfig config)
        {
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

        /// <summary>
        /// Returns whether the supplied string is an assembly library (i.e. dll)
        /// </summary>
        private static bool IsAssemblyLibraryFileName(string filePath)
        {
            // Not expecting .winmd or .exe files to contain Roslyn analyzers
            // so we'll ignore them
            return filePath.EndsWith(DllExtension, StringComparison.OrdinalIgnoreCase);
        }

        #endregion Private methods
    }
}
