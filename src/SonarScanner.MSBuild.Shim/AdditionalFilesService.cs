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

using SonarScanner.MSBuild.Common.RegularExpressions;
using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.Shim;

// Scanner engine code for language detection:
// https://github.com/SonarSource/sonar-scanner-engine/blob/0d222f01c0b3a15e95c5c7d335d29c40ddf5d628/sonarcloud/sonar-scanner-engine/src/main/java/org/sonar/scanner/scan/filesystem/ProjectFilePreprocessor.java#L96
// and
// https://github.com/SonarSource/sonar-scanner-engine/blob/0d222f01c0b3a15e95c5c7d335d29c40ddf5d628/sonarcloud/sonar-scanner-engine/src/main/java/org/sonar/scanner/scan/filesystem/LanguageDetection.java#L70
public class AdditionalFilesService(IDirectoryWrapper directoryWrapper, ILogger logger) : IAdditionalFilesService
{
    private const string SearchPatternAll = "*";
    private static readonly char[] Comma = [','];

    private static readonly IReadOnlyList<string> ExcludedDirectories =
    [
        ".sonarqube",
        ".sonar"
    ];

    // See https://github.com/SonarSource/sonar-iac/pull/1249/files#diff-a10a88bfebc0f61ea4e5c34a130cd3c79b7bae47f716b1a8e405282724cb9141R28
    // and https://sonarsource.atlassian.net/browse/SONARIAC-1419
    // sonar-iac already excludes these files, but the plugin is updated only on later versions of SQ, at least after 10.4.
    // To support excluding them for previous versions, as long as we support them, we exclude them here.
    private static readonly IReadOnlyList<string> ExcludedFiles =
    [
        "build-wrapper-dump.json",
        "compile_commands.json",
    ];

    private static readonly IReadOnlyList<string> SupportedLanguages =
    [
        "sonar.tsql.file.suffixes",
        "sonar.plsql.file.suffixes",
        "sonar.yaml.file.suffixes",
        "sonar.json.file.suffixes",
        "sonar.css.file.suffixes",
        "sonar.html.file.suffixes",
        "sonar.javascript.file.suffixes",
        "sonar.typescript.file.suffixes",
        "sonar.python.file.suffixes",
        "sonar.ipynb.file.suffixes",
        "sonar.php.file.suffixes",
        "sonar.azureresourcemanager.file.suffixes",
    ];

    private static readonly IReadOnlyList<string> GlobingExpressions =
    [
        "sonar.docker.file.patterns"
    ];

    private static readonly IReadOnlyList<string> HardcodedPattern =
    [
        // https://github.com/SonarSource/sonar-iac/blob/801cb490bd13b0c8d721766556f381a68945aa54/iac-extensions/docker/src/main/java/org/sonar/iac/docker/plugin/DockerSensor.java#L93-L102
        // Hardcoded patterns in the IaC plugin.
        // The following patterns are hardcoded until SLCORE-526 is fixed
        "**/Dockerfile",
        "**/**.Dockerfile",
        "**/**.dockerfile",
        // This pattern is hardcoded in the IaC plugin due the limitation of the language recognition preventing multiple language assigned to a single file.
        // It will most likely stay hardcoded.
        "**/Dockerfile.*"
    ];

    private static readonly IReadOnlyList<string> SupportedTestLanguages =
    [
        "sonar.javascript.file.suffixes",
        "sonar.typescript.file.suffixes"
    ];

    private static readonly IReadOnlyList<string> SupportedTestInfixes =
    [
        "test",
        "spec"
    ];

    public AdditionalFiles AdditionalFiles(AnalysisConfig analysisConfig, DirectoryInfo projectBaseDir)
    {
        if (!analysisConfig.ScanAllAnalysis)
        {
            return new([], []);
        }
        var extensions = GetExtensions(analysisConfig);
        var wildcardExpressions = WildcardExpressions(analysisConfig);
        return PartitionAdditionalFiles(GetAllFiles(extensions, wildcardExpressions, projectBaseDir), analysisConfig);
    }

    private FileInfo[] GetAllFiles(IReadOnlyList<string> extensions, IReadOnlyList<string> wildcardExpressions, DirectoryInfo projectBaseDir) =>
        CallDirectoryQuerySafe(projectBaseDir, "directories", () => directoryWrapper.EnumerateDirectories(projectBaseDir, SearchPatternAll, SearchOption.AllDirectories))
            .Concat([projectBaseDir]) // also include the root directory
            .Where(x => !IsExcludedDirectory(x))
            .SelectMany(x => CallDirectoryQuerySafe(x, "files", () => directoryWrapper.EnumerateFiles(x, SearchPatternAll, SearchOption.TopDirectoryOnly)))
            .Where(x => !IsExcludedFile(x) && AdditionalFiles(x, extensions, wildcardExpressions))
            .ToArray();

    private static bool AdditionalFiles(FileInfo file, IReadOnlyList<string> extensions, IReadOnlyList<string> wildcardExpressions) =>
        extensions.Any(x => file.Name.EndsWith(x, StringComparison.OrdinalIgnoreCase) && !file.Name.Equals(x, StringComparison.OrdinalIgnoreCase))
        || wildcardExpressions.Any(x => WildcardPatternMatcher.IsMatch(x, file.FullName, false))
        || HardcodedPattern.Any(x => WildcardPatternMatcher.IsMatch(x, file.FullName, false));

    private IReadOnlyList<T> CallDirectoryQuerySafe<T>(DirectoryInfo path, string entryType, Func<IEnumerable<T>> query)
    {
        try
        {
            logger.LogDebug("Reading {0} from: '{1}'.", entryType, path.FullName);
            var result = query().ToArray();
            logger.LogDebug("Found {0} {1} in: '{2}'.", result.Length, entryType, path.FullName);
            return result;
        }
        catch (Exception exception)
        {
            logger.LogWarning(Resources.WARN_DirectoryGetContentFailure, entryType, path.FullName);
            logger.LogDebug("HResult: {0}, Exception: {1}", exception.HResult, exception);
        }
        return Array.Empty<T>();
    }

    private static bool IsExcludedDirectory(DirectoryInfo directory) =>
        ExcludedDirectories.Any(x => Array.Exists(
            directory.FullName.Split(Path.DirectorySeparatorChar), // split it so that we also exclude subdirectories like .sonarqube/conf.
            part => part.Equals(x, StringComparison.OrdinalIgnoreCase)));

    private static bool IsExcludedFile(FileInfo file) =>
        ExcludedFiles.Any(x => x.Equals(file.Name, StringComparison.OrdinalIgnoreCase));

    private static AdditionalFiles PartitionAdditionalFiles(FileInfo[] allFiles, AnalysisConfig analysisConfig)
    {
        var testExtensions = GetTestExtensions(analysisConfig);
        if (testExtensions.Length == 0)
        {
            return new(allFiles, []);
        }
        var sources = new List<FileInfo>();
        var tests = new List<FileInfo>();
        foreach (var file in allFiles)
        {
            if (Array.Exists(testExtensions, x => file.Name.EndsWith(x, StringComparison.OrdinalIgnoreCase) && !file.Name.Equals(x, StringComparison.OrdinalIgnoreCase)))
            {
                tests.Add(file);
            }
            else
            {
                sources.Add(file);
            }
        }
        return new(sources, tests);
    }

    private static string[] GetExtensions(AnalysisConfig config) =>
        GetProperties(config, SupportedLanguages)
            .Select(EnsureDot)
            .ToArray();

    private static string[] WildcardExpressions(AnalysisConfig config) =>
        GetProperties(config, GlobingExpressions)
            .Select(x => x.StartsWith("**/") ? x : $"**/{x}")
            .ToArray();

    private static string[] GetTestExtensions(AnalysisConfig config) =>
        GetProperties(config, SupportedTestLanguages)
            .Select(EnsureDot)
            .SelectMany(x => SupportedTestInfixes.Select(infix => $".{infix}{x}"))
            .Distinct()
            .ToArray();

    private static IEnumerable<string> GetProperties(AnalysisConfig config, IReadOnlyList<string> ids) =>
        ids
            .Select(x => GetProperty(config, x))
            .Where(x => x is { Value: { } })
            .SelectMany(x => x.Value.Split(Comma, StringSplitOptions.RemoveEmptyEntries));

    // Local settings take priority over Server settings.
    private static Property GetProperty(AnalysisConfig config, string id) =>
        config.LocalSettings?.Find(x => x.Id == id)
        ?? config.ServerSettings?.Find(x => x.Id == id);

    private static string EnsureDot(string x)
    {
        x = x.Trim();
        return x.StartsWith(".") ? x : $".{x}";
    }
}

public sealed class AdditionalFiles(ICollection<FileInfo> sources, ICollection<FileInfo> tests)
{
    public ICollection<FileInfo> Sources { get; } = sources;
    public ICollection<FileInfo> Tests { get; } = tests;
}
