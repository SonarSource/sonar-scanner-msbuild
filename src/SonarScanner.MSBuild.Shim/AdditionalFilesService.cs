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

namespace SonarScanner.MSBuild.Shim;

// Scanner engine code for language detection:
// https://github.com/SonarSource/sonar-scanner-engine/blob/0d222f01c0b3a15e95c5c7d335d29c40ddf5d628/sonarcloud/sonar-scanner-engine/src/main/java/org/sonar/scanner/scan/filesystem/ProjectFilePreprocessor.java#L96
// and
// https://github.com/SonarSource/sonar-scanner-engine/blob/0d222f01c0b3a15e95c5c7d335d29c40ddf5d628/sonarcloud/sonar-scanner-engine/src/main/java/org/sonar/scanner/scan/filesystem/LanguageDetection.java#L70
public class AdditionalFilesService
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
        "sonar.terraform.file.suffixes",
        "sonar.go.file.suffixes",
    ];

    private static readonly IReadOnlyList<string> GlobingExpressions =
    [
        "sonar.docker.file.patterns",
        "sonar.java.jvmframeworkconfig.file.patterns",
        "sonar.text.inclusions"
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
        "**/Dockerfile.*",
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

    private readonly IRuntime runtime;

    public AdditionalFilesService(IRuntime runtime) =>
        this.runtime = runtime;

    /// <summary>
    /// Searches projectBaseDir for files with extensions that match the languages specified in AnalysisConfig.
    /// </summary>
    public AdditionalFiles AdditionalFiles(AnalysisConfig config, DirectoryInfo projectBaseDir)
    {
        if (config.ScanAllAnalysis)
        {
            var extensions = SupportedLanguagesExtensions(config);
            var wildcardExpressions = WildcardExpressions(config);
            return PartitionAdditionalFiles(FindAllFiles(extensions, wildcardExpressions, projectBaseDir), config);
        }
        else
        {
            return new([], []);
        }
    }

    private FileInfo[] FindAllFiles(IReadOnlyList<string> extensions, IReadOnlyList<string> wildcardExpressions, DirectoryInfo projectBaseDir) =>
        CallDirectoryQuerySafe(projectBaseDir, "directories", () => runtime.Directory.EnumerateDirectories(projectBaseDir, SearchPatternAll, SearchOption.AllDirectories))
            .Concat([projectBaseDir]) // also include the root directory
            .Where(x => !IsExcludedDirectory(x))
            .SelectMany(x => CallDirectoryQuerySafe(x, "files", () => runtime.Directory.EnumerateFiles(x, SearchPatternAll, SearchOption.TopDirectoryOnly)))
            .Where(x => !IsExcludedFile(x) && AdditionalFiles(x, projectBaseDir, extensions, wildcardExpressions))
            .ToArray();

    private static bool AdditionalFiles(FileInfo file, DirectoryInfo projectBaseDir, IReadOnlyList<string> extensions, IReadOnlyList<string> wildcardExpressions) =>
        extensions.Any(x => file.Name.EndsWith(x, StringComparison.OrdinalIgnoreCase) && !file.Name.Equals(x, StringComparison.OrdinalIgnoreCase))
        || wildcardExpressions.Any(x => WildcardPatternMatcher.IsMatch(x, RelativePath(projectBaseDir, file), false))
        || HardcodedPattern.Any(x => WildcardPatternMatcher.IsMatch(x, RelativePath(projectBaseDir, file), false));

    private static string RelativePath(DirectoryInfo baseDir, FileInfo file) =>
        // Path.GetRelativePath is not available in .NET Standard 2.0
        // file has been found by EnumerateFiles within baseDir, so it is guaranteed to be a child of baseDir.
        file.FullName.StartsWith(baseDir.FullName)
            ? file.FullName.Substring(baseDir.FullName.Length + 1)
            : file.FullName;

    private IReadOnlyList<T> CallDirectoryQuerySafe<T>(DirectoryInfo path, string entryType, Func<IEnumerable<T>> query)
    {
        try
        {
            runtime.Logger.LogDebug("Reading {0} from: '{1}'.", entryType, path.FullName);
            var result = query().ToArray();
            runtime.Logger.LogDebug("Found {0} {1} in: '{2}'.", result.Length, entryType, path.FullName);
            return result;
        }
        catch (Exception exception)
        {
            runtime.Logger.LogWarning(Resources.WARN_DirectoryGetContentFailure, entryType, path.FullName);
            runtime.Logger.LogDebug("HResult: {0}, Exception: {1}", exception.HResult, exception);
            return [];
        }
    }

    private static bool IsExcludedDirectory(DirectoryInfo directory) =>
        ExcludedDirectories.Any(x => Array.Exists(
                                        directory.FullName.Split(Path.DirectorySeparatorChar), // split it so that we also exclude subdirectories like .sonarqube/conf.
                                        part => part.Equals(x, StringComparison.OrdinalIgnoreCase)));

    private static bool IsExcludedFile(FileInfo file) =>
        ExcludedFiles.Any(x => x.Equals(file.Name, StringComparison.OrdinalIgnoreCase));

    private AdditionalFiles PartitionAdditionalFiles(FileInfo[] allFiles, AnalysisConfig config)
    {
        var testExtensions = SupportedLanguagesTestExtensions(config);
        if (testExtensions.Length == 0)
        {
            return new(allFiles, []);
        }
        var result = new AdditionalFiles([], []);
        foreach (var file in allFiles)
        {
            if (Array.Exists(testExtensions, x => file.Name.EndsWith(x, StringComparison.OrdinalIgnoreCase) && !file.Name.Equals(x, StringComparison.OrdinalIgnoreCase)))
            {
                result.Tests.Add(file);
            }
            else
            {
                result.Sources.Add(file);
            }
        }
        return result;
    }

    private string[] SupportedLanguagesExtensions(AnalysisConfig config) =>
        AllPropertyValues(config, SupportedLanguages).Select(EnsureDot).ToArray();

    private string[] WildcardExpressions(AnalysisConfig config) =>
        AllPropertyValues(config, GlobingExpressions).ToArray();

    private string[] SupportedLanguagesTestExtensions(AnalysisConfig config) =>
        AllPropertyValues(config, SupportedTestLanguages)
            .Select(EnsureDot)
            .SelectMany(x => SupportedTestInfixes.Select(infix => $".{infix}{x}"))
            .Distinct()
            .ToArray();

    private IEnumerable<string> AllPropertyValues(AnalysisConfig config, IReadOnlyList<string> ids) =>
        ids
            .Select(x => config.GetSettingOrDefault(x, true, null, runtime.Logger))
            .Where(x => x is not null)
            .SelectMany(x => x.Split(Comma, StringSplitOptions.RemoveEmptyEntries));

    private static string EnsureDot(string x)
    {
        x = x.Trim();
        return x.StartsWith(".") ? x : $".{x}";
    }
}

public sealed record AdditionalFiles(ICollection<FileInfo> Sources, ICollection<FileInfo> Tests);
