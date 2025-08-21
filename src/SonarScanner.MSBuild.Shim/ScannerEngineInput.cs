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

    /// <summary>
    /// Project guids that have been processed. This is used in <see cref="Flush"/> to write the module keys in the end.
    /// </summary>
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
        modules = AppendKeyValue("sonar.modules", string.Empty);
    }

    public override string ToString() =>
        JsonConvert.SerializeObject(root, Formatting.Indented);

    public void WriteSettingsForProject(ProjectData projectData)
    {
        if (projectData is null)
        {
            throw new ArgumentNullException(nameof(projectData));
        }

        var guid = projectData.Guid;
        Debug.Assert(projectData.ReferencedFiles.Count > 0, "Expecting a project to have files to analyze");
        Debug.Assert(projectData.SonarQubeModuleFiles.All(x => x.Exists), "Expecting all of the specified files to exist");
        Debug.Assert(!moduleKeys.Contains(guid), "Expecting the project guids to be unique.");

        moduleKeys.Add(guid);
        modules.Value = string.Join(",", moduleKeys);

        AppendKeyValue(guid, SonarProperties.ProjectKey, config.SonarProjectKey + ":" + guid);
        AppendKeyValue(guid, SonarProperties.ProjectName, projectData.Project.ProjectName);
        AppendKeyValue(guid, SonarProperties.ProjectBaseDir, projectData.Project.GetDirectory().FullName);

        if (!string.IsNullOrWhiteSpace(projectData.Project.Encoding))
        {
            AppendKeyValue(guid, SonarProperties.SourceEncoding, projectData.Project.Encoding.ToLowerInvariant());
        }

        AppendKeyValue(guid, projectData.Project.ProjectType == ProjectType.Product ? SonarTests : SonarSources, string.Empty);
        AppendKeyValue(guid, projectData.Project.ProjectType == ProjectType.Product ? SonarSources : SonarTests, projectData.SonarQubeModuleFiles);

        if (projectData.Project.AnalysisSettings is not null && projectData.Project.AnalysisSettings.Any())
        {
            foreach (var setting in projectData.Project.AnalysisSettings.Where(x =>
                !PropertiesFileGenerator.IsProjectOutPaths(x.Id)
                && !PropertiesFileGenerator.IsReportFilePaths(x.Id)
                && !PropertiesFileGenerator.IsTelemetryPaths(x.Id)))
            {
                AppendKeyValue($"{guid}.{setting.Id}", setting.Value);
            }

            WriteAnalyzerOutputPaths(projectData);
            WriteRoslynReportPaths(projectData);
            WriteTelemetryPaths(projectData);
        }

        var moduleWorkdir = Path.Combine(config.SonarOutputDir, ".sonar", $"mod{moduleKeys.Count - 1}"); // zero-based index
        AppendKeyValue(guid, SonarProperties.WorkingDirectory, moduleWorkdir);
    }

    public void WriteTelemetryPaths(ProjectData project)
    {
        if (project.TelemetryPaths.Count == 0)
        {
            return;
        }

        string property;
        if (ProjectLanguages.IsCSharpProject(project.Project.ProjectLanguage))
        {
            property = PropertiesFileGenerator.TelemetryPathsCsharpPropertyKey;
        }
        else if (ProjectLanguages.IsVbProject(project.Project.ProjectLanguage))
        {
            property = PropertiesFileGenerator.TelemetryPathsVbNetPropertyKey;
        }
        else
        {
            return;
        }

        AppendKeyValue(project.Guid, property, project.TelemetryPaths);
    }

    public void WriteAnalyzerOutputPaths(ProjectData project)
    {
        if (project.AnalyzerOutPaths.Count == 0)
        {
            return;
        }

        string property;
        if (ProjectLanguages.IsCSharpProject(project.Project.ProjectLanguage))
        {
            property = PropertiesFileGenerator.ProjectOutPathsCsharpPropertyKey;
        }
        else if (ProjectLanguages.IsVbProject(project.Project.ProjectLanguage))
        {
            property = PropertiesFileGenerator.ProjectOutPathsVbNetPropertyKey;
        }
        else
        {
            return;
        }

        AppendKeyValue(project.Guid, property, project.AnalyzerOutPaths);
    }

    public void WriteRoslynReportPaths(ProjectData project)
    {
        if (!project.RoslynReportFilePaths.Any())
        {
            return;
        }

        string property;
        if (ProjectLanguages.IsCSharpProject(project.Project.ProjectLanguage))
        {
            property = PropertiesFileGenerator.ReportFilePathsCSharpPropertyKey;
        }
        else if (ProjectLanguages.IsVbProject(project.Project.ProjectLanguage))
        {
            property = PropertiesFileGenerator.ReportFilePathsVbNetPropertyKey;
        }
        else
        {
            return;
        }

        AppendKeyValue(project.Guid, property, project.RoslynReportFilePaths);
    }

    public void WriteVsTestReportPaths(string[] paths) =>
        AppendKeyValue(SonarProperties.VsTestReportsPaths, paths);

    public void WriteVsXmlCoverageReportPaths(string[] paths) =>
        AppendKeyValue(SonarProperties.VsCoverageXmlReportsPaths, paths);

    /// <summary>
    /// Write the supplied global settings into the file.
    /// </summary>
    public void WriteGlobalSettings(AnalysisProperties properties)
    {
        if (properties is null)
        {
            throw new ArgumentNullException(nameof(properties));
        }

        foreach (var setting in properties)
        {
            // We should no longer pass the sonar.verbose=true parameter to the scanner CLI.
            // See: https://github.com/SonarSource/sonar-scanner-msbuild/issues/543
            if (setting.Id != SonarProperties.Verbose)
            {
                AppendKeyValue(setting.Id, setting.Value);
            }
        }
    }

    public void WriteSonarProjectInfo(DirectoryInfo projectBaseDir)
    {
        AppendKeyValue(SonarProperties.ProjectKey, config.SonarProjectKey);
        AppendKeyValueIfNotEmpty(SonarProperties.ProjectName, config.SonarProjectName);
        AppendKeyValueIfNotEmpty(SonarProperties.ProjectVersion, config.SonarProjectVersion);
        AppendKeyValue(SonarProperties.WorkingDirectory, Path.Combine(config.SonarOutputDir, ".sonar"));
        AppendKeyValue(SonarProperties.ProjectBaseDir, projectBaseDir.FullName);
        AppendKeyValue(SonarProperties.PullRequestCacheBasePath, config.GetConfigValue(SonarProperties.PullRequestCacheBasePath, null));
    }

    public void WriteSharedFiles(AnalysisFiles analysisFiles)
    {
        if (analysisFiles.Sources.Count > 0)
        {
            AppendKeyValue("sonar", "sources", analysisFiles.Sources);
        }
        if (analysisFiles.Tests.Count > 0)
        {
            AppendKeyValue("sonar", "tests", analysisFiles.Tests);
        }
    }

    internal void AppendKeyValue(string keyPrefix, string keySuffix, IEnumerable<string> values) =>
        AppendKeyValue($"{keyPrefix}.{keySuffix}", ToMultiValueProperty(values));

    internal void AppendKeyValue(string key, IEnumerable<string> values) =>
        AppendKeyValue(key, ToMultiValueProperty(values));

    private void AppendKeyValue(string keyPrefix, string keySuffix, IEnumerable<FileInfo> paths) =>
        AppendKeyValue(keyPrefix, keySuffix, paths.Select(x => x.FullName));

    private void AppendKeyValue(string keyPrefix, string keySuffix, string value) =>
        AppendKeyValue(keyPrefix + "." + keySuffix, value);

    private JProperty AppendKeyValue(string key, string value)
    {
        Debug.Assert(
            !ProcessRunnerArguments.ContainsSensitiveData(key) && !ProcessRunnerArguments.ContainsSensitiveData(value),
            "Not expecting sensitive data to be written to the sonar-project properties file. Key: {0}",
            key);
        var valueProperty = new JProperty("value", value);
        scannerProperties.Add(new JObject
        {
            new JProperty("key", key),
            valueProperty
        });
        return valueProperty;
    }

    private void AppendKeyValueIfNotEmpty(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            AppendKeyValue(key, value);
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
