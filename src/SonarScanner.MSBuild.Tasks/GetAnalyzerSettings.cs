/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SonarScanner.MSBuild.Tasks;

/// <summary>
/// Build task to return the Roslyn analyzer settings from the analysis config file.
/// </summary>
public class GetAnalyzerSettings : Task
{
    private const string ExcludeTestProjectsSettingId = "sonar.dotnet.excludeTestProjects";
    private const string DllExtension = ".dll";

    private readonly string[] sonarDotNetPluginKeys = ["csharp", "vbnet"];

    /// <summary>
    /// The directory containing the analysis config settings file.
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
    /// Original ruleset specified in the project, if any.
    /// </summary>
    public string OriginalRulesetFilePath { get; set; }

    [Required]
    /// <summary>
    /// Path to the directory containing the project being built.
    /// </summary>
    public string CurrentProjectDirectoryPath { get; set; }

    /// <summary>
    /// Project-specific directory into which new output files can be written
    /// (e.g. a new project-specific ruleset file).
    /// </summary>
    [Required]
    public string ProjectSpecificConfigDirectory { get; set; }

    /// <summary>
    /// Indicates whether the current project is a test project or product project.
    /// </summary>
    [Required]
    public bool IsTestProject { get; set; }

    /// <summary>
    /// The language for which we are gettings the settings.
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Path to the generated ruleset file to use.
    /// </summary>
    [Output]
    public string RuleSetFilePath { get; private set; }

    /// <summary>
    /// List of analyzer assemblies and dependencies to pass to the compiler as analyzers.
    /// </summary>
    [Output]
    public string[] AnalyzerFilePaths { get; private set; }

    /// <summary>
    /// List of additional files to pass to the compiler.
    /// </summary>
    [Output]
    public string[] AdditionalFilePaths { get; private set; }

    public override bool Execute()
    {
        var logger = new MSBuildLoggerAdapter(Log);
        var config = TaskUtilities.TryGetConfig(AnalysisConfigDir, logger);

        var languageSettings = LanguageSpecificSettings(config);
        if (languageSettings is null)
        {
            // Early-out: we don't have any settings for the current language.
            // Preserve the default existing behaviour of only preserving the original list of additional files but clearing the analyzers.
            RuleSetFilePath = null;
            AdditionalFilePaths = OriginalAdditionalFiles;
            return !Log.HasLoggedErrors;
        }

        TaskOutputs outputs;
        if (IsTestProject && ExcludeTestProjects())
        {
            // Special case: to provide colorization etc for code in test projects, we need to run only the SonarC#/VB analyzers, with all of the non-utility rules turned off
            // See [MMF-486]: https://jira.sonarsource.com/browse/MMF-486
            Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_ConfiguringTestProjectAnalysis);
            outputs = CreateDeactivatedProjectSettings(languageSettings);
        }
        else
        {
            Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_MergingSettings);
            outputs = CreateMergedAnalyzerSettings(languageSettings);
        }

        ApplyTaskOutput(outputs);

        return !Log.HasLoggedErrors;

        bool ExcludeTestProjects() =>
            config.AnalysisSettings(false, logger).TryGetValue(ExcludeTestProjectsSettingId, out var excludeTestProjects)
            && excludeTestProjects.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private TaskOutputs CreateDeactivatedProjectSettings(AnalyzerSettings settings)
    {
        var sonarDotNetAnalyzers = settings.AnalyzerPlugins
            .Where(x => sonarDotNetPluginKeys.Contains(x.Key, StringComparer.OrdinalIgnoreCase))
            .SelectMany(x => x.AssemblyPaths);
        return new TaskOutputs(settings.DeactivatedRulesetPath, sonarDotNetAnalyzers, settings.AdditionalFilePaths);
    }

    private TaskOutputs CreateMergedAnalyzerSettings(AnalyzerSettings settings)
    {
        var mergedRuleset = CreateMergedRuleset(settings);
        var allAnalyzers = MergeAnalyzersLists(settings.AnalyzerPlugins.SelectMany(x => x.AssemblyPaths), OriginalAnalyzers);
        var additionalFilePaths = MergeAdditionalFilesLists(settings.AdditionalFilePaths, OriginalAdditionalFiles);
        return new TaskOutputs(mergedRuleset, allAnalyzers, additionalFilePaths);
    }

    private string CreateMergedRuleset(AnalyzerSettings languageSpecificSettings)
    {
        if (OriginalRulesetFilePath is null)
        {
            // If the project doesn't already have a ruleset can just
            // return the generated one directly
            Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_OriginalRulesetNotSpecified, languageSpecificSettings.RulesetPath);
            return languageSpecificSettings.RulesetPath;
        }

        var resolvedRulesetPath = AbsoluteRulesetPath();
        var mergedRulesetFilePath = Path.Combine(ProjectSpecificConfigDirectory, "merged.ruleset");
        Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_CreatingMergedRuleset, mergedRulesetFilePath);
        WriteMergedRuleSet(resolvedRulesetPath, languageSpecificSettings.RulesetPath, mergedRulesetFilePath);
        return mergedRulesetFilePath;
    }

    private string AbsoluteRulesetPath()
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

        Log.LogMessage(
            MessageImportance.Low,
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
            // This will fail if the ruleset is invalid. However, we generated the ruleset so something else is already wrong in that case.
            var rulesetNode = xdoc.Descendants().First(e => e.Name == "RuleSet");
            var importElement = new XElement("Include");
            importElement.Add(new XAttribute("Path", originalRuleset));
            importElement.Add(new XAttribute("Action", "Default"));
            rulesetNode.AddFirst(importElement);
            xdoc.Save(mergedRulesetFilePath);
        }
    }

    private AnalyzerSettings LanguageSpecificSettings(AnalysisConfig config)
    {
        if (config is null)
        {
            return null;
        }
        else if (string.IsNullOrEmpty(Language))
        {
            Log.LogMessage(Resources.AnalyzerSettings_LanguageNotSpecified);
            return null;
        }
        else if (config.AnalyzersSettings is { } analyzers)
        {
            if (analyzers.SingleOrDefault(x => Language.Equals(x.Language)) is { } settings)
            {
                return settings;
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_NotSpecifiedInConfig, Language);
                return null;
            }
        }
        else
        {
            Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_NotSpecifiedInConfig, Language);
            return null;
        }
    }

    /// <summary>
    /// Merges and returns the supplied list of analyzer paths. In case of duplicate
    /// SonarAnalyzers, the path from the sonarAnalyzerPaths list is used.
    /// </summary>
    private string[] MergeAnalyzersLists(IEnumerable<string> sonarAnalyzerPaths, IEnumerable<string> userProvidedAnalyzerPaths)
    {
        Debug.Assert(sonarAnalyzerPaths is not null, $"{nameof(sonarAnalyzerPaths)} should not be null at this point.");
        var nonNullUserProvidedAnalyzerPaths = userProvidedAnalyzerPaths ?? Enumerable.Empty<string>();
        var sonarAnalyzerDuplicates = nonNullUserProvidedAnalyzerPaths
            .Where(x => FileNameOrDefault(x).StartsWith("SonarAnalyzer", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var finalList = sonarAnalyzerPaths
            .Union(nonNullUserProvidedAnalyzerPaths)
            .Except(sonarAnalyzerDuplicates)
            .ToArray();
        LogRemovedFiles(sonarAnalyzerDuplicates);
        return finalList;
    }

    /// <summary>
    /// Merges and returns the supplied list of file paths. In case of duplicate
    /// file *names* (not full paths), the path from the sonarAdditionalFiles list is used.
    /// </summary>
    private string[] MergeAdditionalFilesLists(IEnumerable<string> sonarAdditionalFiles, IEnumerable<string> userProvidedAdditionalFiles)
    {
        var nonNullSonarAdditionalFiles = sonarAdditionalFiles ?? Enumerable.Empty<string>();
        var nonNullUserProvidedAdditionalFiles = userProvidedAdditionalFiles ?? Enumerable.Empty<string>();
        var duplicateAdditionalFiles = EntriesWithMatchingFileNames(nonNullSonarAdditionalFiles, nonNullUserProvidedAdditionalFiles);
        var finalList = nonNullSonarAdditionalFiles
            .Union(nonNullUserProvidedAdditionalFiles)
            .Except(duplicateAdditionalFiles)
            .ToArray();
        LogRemovedFiles(duplicateAdditionalFiles);
        return finalList;
    }

    private void LogRemovedFiles(string[] removedDuplicateFiles)
    {
        var removedDuplicates = string.Join(", ", removedDuplicateFiles);
        Log.LogMessage(MessageImportance.Low, Resources.AnalyzerSettings_RemovingDuplicateFiles, string.IsNullOrEmpty(removedDuplicates) ? removedDuplicates : "{none}");
    }

    /// <summary>
    /// Returns the entries from <paramref name="candidateFilePaths"/> where the file name
    /// part of the candidate matches the file name of an entry in <paramref name="sourceFilePaths"/>.
    /// </summary>
    private static string[] EntriesWithMatchingFileNames(IEnumerable<string> sourceFilePaths, IEnumerable<string> candidateFilePaths)
    {
        Debug.Assert(sourceFilePaths is not null, $"{nameof(sourceFilePaths)} should not be null at this point.");
        Debug.Assert(candidateFilePaths is not null, $"{nameof(candidateFilePaths)} should not be null at this point.");
        var sourceFileNames = new HashSet<string>(sourceFilePaths.Select(FileNameOrDefault).Where(x => !string.IsNullOrEmpty(x)));
        return candidateFilePaths.Where(x => sourceFileNames.Contains(FileNameOrDefault(x), StringComparer.OrdinalIgnoreCase)).ToArray();
    }

    private static string FileNameOrDefault(string path)
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
        files.Where(IsAssemblyLibraryFileName).ToArray();

    /// <summary>
    /// Returns whether the supplied string is an assembly library (i.e. dll).
    /// </summary>
    private static bool IsAssemblyLibraryFileName(string filePath) =>
        // Not expecting .winmd or .exe files to contain Roslyn analyzers so we'll ignore them
        filePath.EndsWith(DllExtension, StringComparison.OrdinalIgnoreCase);

    private void ApplyTaskOutput(TaskOutputs outputs)
    {
        RuleSetFilePath = outputs.RuleSet;
        AnalyzerFilePaths = RemoveNonAnalyzerFiles(outputs.AssemblyPaths);
        AdditionalFilePaths = outputs.AdditionalFilePaths;
    }

    private sealed class TaskOutputs
    {
        public string RuleSet { get; }
        public string[] AssemblyPaths { get; }
        public string[] AdditionalFilePaths { get; }

        public TaskOutputs(string ruleSet, IEnumerable<string> assemblyPaths, IEnumerable<string> additionalFilePaths)
        {
            RuleSet = ruleSet;
            AssemblyPaths = assemblyPaths?.ToArray() ?? new string[] { };
            AdditionalFilePaths = additionalFilePaths?.ToArray() ?? new string[] { };
        }
    }
}
