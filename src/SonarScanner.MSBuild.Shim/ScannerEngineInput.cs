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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SonarScanner.MSBuild.Shim;

public class ScannerEngineInput
{
    private const string SonarSources = "sonar.sources";
    private const string SonarTests = "sonar.tests";
    private readonly AnalysisConfig config;

    private readonly HashSet<string> moduleKeys = [];
    // ScannerEngine takes 2 mandatory string (non-null) properties
    // https://xtranet-sonarsource.atlassian.net/wiki/spaces/CodeOrches/pages/3155001372/Scanner+Bootstrapping#Scanner-Engine-contract
    private readonly Dictionary<string, string> scannerProperties = [];

    public ScannerEngineInput(AnalysisConfig config) =>
        this.config = config ?? throw new ArgumentNullException(nameof(config));

    public ScannerEngineInput CloneWithoutSensitiveData()
    {
        var result = new ScannerEngineInput(config);
        result.moduleKeys.UnionWith(moduleKeys);
        foreach (var property in scannerProperties)
        {
            result.Add(property.Key, ProcessRunnerArguments.ContainsSensitiveData(property.Key) || ProcessRunnerArguments.ContainsSensitiveData(property.Value) ? "***" : property.Value);
        }
        return result;
    }

    public override string ToString() =>
        JsonConvert.SerializeObject(
            new JObject
            {
                new JProperty("scannerProperties", new JArray(scannerProperties.Select(x => new JObject
                    {
                        { "key", x.Key },
                        { "value", x.Value },
                    })))
            },
            Formatting.Indented);

    public void AddProject(ProjectData project)
    {
        _ = project ?? throw new ArgumentNullException(nameof(project));
        var guid = project.Guid;
        moduleKeys.Add(guid);
        Add("sonar.modules", string.Join(",", moduleKeys));
        Add(guid, SonarProperties.ProjectKey, config.SonarProjectKey + ":" + guid);
        Add(guid, SonarProperties.ProjectName, project.Project.ProjectName);
        Add(guid, SonarProperties.ProjectBaseDir, project.Project.ProjectFileDirectory().FullName);
        Add(guid, SonarProperties.WorkingDirectory, Path.Combine(config.SonarOutputDir, ".sonar", $"mod{moduleKeys.Count - 1}"));    // zero-based index
        if (!string.IsNullOrWhiteSpace(project.Project.Encoding))
        {
            Add(guid, SonarProperties.SourceEncoding, project.Project.Encoding.ToLowerInvariant());
        }
        Add(guid, project.Project.ProjectType == ProjectType.Product ? SonarTests : SonarSources, string.Empty);
        Add(guid, project.Project.ProjectType == ProjectType.Product ? SonarSources : SonarTests, project.SonarQubeModuleFiles);
    }

    public void AddVsTestReportPaths(string[] paths) =>
        Add(SonarProperties.VsTestReportsPaths, paths);

    public void AddVsXmlCoverageReportPaths(string[] paths) =>
        Add(SonarProperties.VsCoverageXmlReportsPaths, paths);

    public void AddGlobalSettings(AnalysisProperties properties)
    {
        _ = properties ?? throw new ArgumentNullException(nameof(properties));
        // https://github.com/SonarSource/sonar-scanner-msbuild/issues/543 We should no longer pass the sonar.verbose=true parameter to the scanner CLI
        foreach (var setting in properties.Where(x => x.Id != SonarProperties.Verbose))
        {
            Add(setting.Id, setting.Value);
        }
    }

    public void AddConfig(DirectoryInfo projectBaseDir)
    {
        Add(SonarProperties.ProjectKey, config.SonarProjectKey);
        Add(SonarProperties.ProjectName, config.SonarProjectName);
        Add(SonarProperties.ProjectVersion, config.SonarProjectVersion);
        Add(SonarProperties.WorkingDirectory, Path.Combine(config.SonarOutputDir, ".sonar"));
        Add(SonarProperties.ProjectBaseDir, projectBaseDir.FullName);
        Add(SonarProperties.PullRequestCacheBasePath, config.GetConfigValue(SonarProperties.PullRequestCacheBasePath, null));
    }

    public void AddSharedFiles(AnalysisFiles analysisFiles)
    {
        Add("sonar", "sources", analysisFiles.Sources);
        Add("sonar", "tests", analysisFiles.Tests);
    }

    public void Add(string keyPrefix, string keySuffix, IEnumerable<FileInfo> paths) =>
        Add(keyPrefix, keySuffix, paths.Select(x => x.FullName));

    public void Add(string keyPrefix, string keySuffix, string value) =>
        Add($"{keyPrefix}.{keySuffix}", value);

    internal void Add(string keyPrefix, string keySuffix, IEnumerable<string> values)
    {
        if (values.Any())
        {
            Add(keyPrefix, keySuffix, ToMultiValueProperty(values));
        }
    }

    private void Add(string key, IEnumerable<string> values) =>
        Add(key, ToMultiValueProperty(values));

    private void Add(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            scannerProperties[key] = value;
        }
    }

    private static string ToMultiValueProperty(IEnumerable<string> paths)
    {
        return string.Join(",", paths.Select(Encode));

        // RFC4180 2.5: Each field may or may not be enclosed in double quotes
        // RFC4180 2.6: Fields containing line breaks (CRLF), double quotes, and commas should be enclosed in double-quotes.
        // RFC4180 2.7: If double-quotes are used to enclose fields, then a double-quote appearing inside a field must be escaped by preceding it with another double quote.
        static string Encode(string value) =>
            value.IndexOfAny(['\r', '\n', '\"', ',']) >= 0
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
    }
}
