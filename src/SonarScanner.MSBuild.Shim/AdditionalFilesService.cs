/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.Shim;

// Scanner engine code for language detection:
// https://github.com/SonarSource/sonar-scanner-engine/blob/0d222f01c0b3a15e95c5c7d335d29c40ddf5d628/sonarcloud/sonar-scanner-engine/src/main/java/org/sonar/scanner/scan/filesystem/ProjectFilePreprocessor.java#L96
// and
// https://github.com/SonarSource/sonar-scanner-engine/blob/0d222f01c0b3a15e95c5c7d335d29c40ddf5d628/sonarcloud/sonar-scanner-engine/src/main/java/org/sonar/scanner/scan/filesystem/LanguageDetection.java#L70
public class AdditionalFilesService(IDirectoryWrapper directoryWrapper, ILogger logger) : IAdditionalFilesService
{
    private static readonly char[] Comma = [','];

    private static readonly IReadOnlyList<string> ExcludedDirectories =
    [
        ".sonarqube",
        ".sonar"
    ];

    private static readonly IReadOnlyList<string> SupportedLanguages =
    [
        "sonar.tsql.file.suffixes",
        "sonar.plsql.file.suffixes",
        "sonar.yaml.file.suffixes",
        "sonar.xml.file.suffixes",
        "sonar.json.file.suffixes",
        "sonar.css.file.suffixes",
        "sonar.html.file.suffixes",
        "sonar.javascript.file.suffixes",
        "sonar.typescript.file.suffixes"
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
        if (!analysisConfig.MultiFileAnalysis)
        {
            return new([], []);
        }
        var extensions = GetExtensions(analysisConfig);
        if (extensions.Length == 0)
        {
            return new([], []);
        }
        // Respect user defined parameters and do not re-populate sources or test.
        // This might lead to some files considered as both source and test, in which case the user should exclude them via sonar.exclusions.
        if (FirstUserSpecifiedSonarParameter(analysisConfig) is { } userDefinedParameter)
        {
            logger.LogWarning(Resources.WARN_DisableMultiFileAnalysisWhenProvidingParameters, userDefinedParameter);
            return new([], []);
        }
        return PartitionAdditionalFiles(GetAllFiles(extensions, projectBaseDir), analysisConfig);
    }

    private FileInfo[] GetAllFiles(IEnumerable<string> extensions, DirectoryInfo projectBaseDir) =>
        directoryWrapper
            .EnumerateDirectories(projectBaseDir, "*", SearchOption.AllDirectories)
            .Concat([projectBaseDir]) // also include the root directory
            .Where(x => !IsExcludedDirectory(x))
            .SelectMany(x => directoryWrapper.EnumerateFiles(x, "*", SearchOption.TopDirectoryOnly))
            .Where(x => extensions.Any(e => x.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase) && !x.Name.Equals(e, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

    private static bool IsExcludedDirectory(DirectoryInfo directory) =>
        ExcludedDirectories.Any(x => x.Equals(directory.Name, StringComparison.OrdinalIgnoreCase));

    private static string FirstUserSpecifiedSonarParameter(AnalysisConfig analysisConfig) =>
        SonarProperties.ScanAllWarningParameters.FirstOrDefault(x => analysisConfig.LocalSettings.Exists(setting => setting.Id == x));

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
        GetProperties(config, SupportedLanguages);

    private static string[] GetTestExtensions(AnalysisConfig config) =>
        GetProperties(config, SupportedTestLanguages)
            .SelectMany(x => SupportedTestInfixes.Select(infix => $".{infix}{x}"))
            .Distinct()
            .ToArray();

    private static string[] GetProperties(AnalysisConfig config, IReadOnlyList<string> ids) =>
        ids
            .Select(x => GetProperty(config, x))
            .Where(x => x is { Value: { } })
            .SelectMany(x => x.Value.Split(Comma, StringSplitOptions.RemoveEmptyEntries))
            .Select(EnsureDot)
            .ToArray();

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
