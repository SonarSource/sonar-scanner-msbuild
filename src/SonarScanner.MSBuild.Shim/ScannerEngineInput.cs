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
    private readonly JObject root;
    private readonly JArray scannerProperties = [];
    private readonly JProperty modules;

    public ScannerEngineInput(AnalysisConfig config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        root = new JObject
        {
            new JProperty("scannerProperties", scannerProperties)
        };
        modules = new JProperty("value", string.Empty);
        AppendKeyValue("sonar.modules", modules);
    }

    public override string ToString() =>
        JsonConvert.SerializeObject(root, Formatting.Indented);

    public void WriteSettingsForProject(ProjectData project)
    {
        _ = project ?? throw new ArgumentNullException(nameof(project));
        var guid = project.Guid;
        moduleKeys.Add(guid);
        modules.Value = string.Join(",", moduleKeys);
        AppendKeyValue(guid, SonarProperties.ProjectKey, config.SonarProjectKey + ":" + guid);
        AppendKeyValue(guid, SonarProperties.ProjectName, project.Project.ProjectName);
        AppendKeyValue(guid, SonarProperties.ProjectBaseDir, project.Project.GetDirectory().FullName);
        AppendKeyValue(guid, SonarProperties.WorkingDirectory, Path.Combine(config.SonarOutputDir, ".sonar", $"mod{moduleKeys.Count - 1}"));    // zero-based index
        if (!string.IsNullOrWhiteSpace(project.Project.Encoding))
        {
            AppendKeyValue(guid, SonarProperties.SourceEncoding, project.Project.Encoding.ToLowerInvariant());
        }
        AppendKeyValue(guid, project.Project.ProjectType == ProjectType.Product ? SonarTests : SonarSources, string.Empty);
        AppendKeyValue(guid, project.Project.ProjectType == ProjectType.Product ? SonarSources : SonarTests, project.SonarQubeModuleFiles);
    }

    public void AddVsTestReportPaths(string[] paths) =>
        AppendKeyValue(SonarProperties.VsTestReportsPaths, paths);

    public void AddVsXmlCoverageReportPaths(string[] paths) =>
        AppendKeyValue(SonarProperties.VsCoverageXmlReportsPaths, paths);

    public void AddGlobalSettings(AnalysisProperties properties)
    {
        _ = properties ?? throw new ArgumentNullException(nameof(properties));
        // https://github.com/SonarSource/sonar-scanner-msbuild/issues/543 We should no longer pass the sonar.verbose=true parameter to the scanner CLI
        foreach (var setting in properties.Where(x => x.Id != SonarProperties.Verbose))
        {
            AppendKeyValue(setting.Id, setting.Value);
        }
    }

    public void WriteSonarProjectInfo(DirectoryInfo projectBaseDir)
    {
        AppendKeyValue(SonarProperties.ProjectKey, config.SonarProjectKey);
        AppendKeyValue(SonarProperties.ProjectName, config.SonarProjectName);
        AppendKeyValue(SonarProperties.ProjectVersion, config.SonarProjectVersion);
        AppendKeyValue(SonarProperties.WorkingDirectory, Path.Combine(config.SonarOutputDir, ".sonar"));
        AppendKeyValue(SonarProperties.ProjectBaseDir, projectBaseDir.FullName);
        AppendKeyValue(SonarProperties.PullRequestCacheBasePath, config.GetConfigValue(SonarProperties.PullRequestCacheBasePath, null));
    }

    public void AddSharedFiles(AnalysisFiles analysisFiles)
    {
        AppendKeyValue("sonar", "sources", analysisFiles.Sources);
        AppendKeyValue("sonar", "tests", analysisFiles.Tests);
    }

    public void AppendKeyValue(string keyPrefix, string keySuffix, IEnumerable<FileInfo> paths) =>
        AppendKeyValue(keyPrefix, keySuffix, paths.Select(x => x.FullName));

    public void AppendKeyValue(string keyPrefix, string keySuffix, string value) =>
        AppendKeyValue(keyPrefix + "." + keySuffix, value);

    internal void AppendKeyValue(string keyPrefix, string keySuffix, IEnumerable<string> values)
    {
        if (values.Any())
        {
            AppendKeyValue($"{keyPrefix}.{keySuffix}", ToMultiValueProperty(values));
        }
    }

    private void AppendKeyValue(string key, IEnumerable<string> values) =>
        AppendKeyValue(key, ToMultiValueProperty(values));

    private void AppendKeyValue(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            AppendKeyValue(key, new JProperty("value", value));
        }
    }

    private void AppendKeyValue(string key, JProperty value) =>
        scannerProperties.Add(new JObject { new JProperty("key", key), value });

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
