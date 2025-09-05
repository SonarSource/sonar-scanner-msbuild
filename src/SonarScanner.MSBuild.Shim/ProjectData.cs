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

namespace SonarScanner.MSBuild.Shim;

/// <summary>
/// Contains the aggregated data from multiple ProjectInfos sharing the same GUID.
/// </summary>
public class ProjectData
{
    public string Guid => Project.ProjectGuidAsString();

    public ProjectInfoValidity Status { get; set; }

    public ProjectInfo Project { get; }

    /// <summary>
    /// All files referenced by the given project.
    /// </summary>
    public ISet<FileInfo> ReferencedFiles { get; } = new HashSet<FileInfo>(new FileInfoEqualityComparer());

    /// <summary>
    /// All files that belongs to this SonarQube module.
    /// </summary>
    public ICollection<FileInfo> SonarQubeModuleFiles { get; } = new List<FileInfo>();

    /// <summary>
    /// Roslyn analysis output files (json).
    /// </summary>
    public ICollection<FileInfo> RoslynReportFilePaths { get; } = new HashSet<FileInfo>(new FileInfoEqualityComparer());

    /// <summary>
    /// The folders where the protobuf files are generated.
    /// </summary>
    public ICollection<FileInfo> AnalyzerOutPaths { get; } = new HashSet<FileInfo>(new FileInfoEqualityComparer());

    /// <summary>
    /// The files where the Telemetry.json files are generated.
    /// </summary>
    public ICollection<FileInfo> TelemetryPaths { get; } = new HashSet<FileInfo>(new FileInfoEqualityComparer());

    public ProjectData(IGrouping<Guid, ProjectInfo> projectsGroupedByGuid, IRuntime runtime)
    {
        // To ensure consistently sending of metrics from the same configuration we sort the project outputs and use only the first one for metrics.
        var orderedProjects = projectsGroupedByGuid.OrderBy(x => $"{x.Configuration}_{x.Platform}_{x.TargetFramework}").ToList();
        Project = orderedProjects[0];
        // Find projects with different paths within the same group
        var isWindows = runtime.OperatingSystem.IsWindows();
        var projectPathsInGroup = projectsGroupedByGuid.Select(x => isWindows ? x.FullPath?.ToLowerInvariant() : x.FullPath).Distinct().ToList();
        if (projectPathsInGroup.Count > 1)
        {
            foreach (var projectPath in projectPathsInGroup)
            {
                runtime.LogWarning(Resources.WARN_DuplicateProjectGuid, projectsGroupedByGuid.Key, projectPath);
            }
            Status = ProjectInfoValidity.DuplicateGuid;
        }
        else if (projectsGroupedByGuid.Key == System.Guid.Empty)
        {
            Status = ProjectInfoValidity.InvalidGuid;
        }
        else if (File.Exists(Project.FullPath))
        {
            foreach (var project in orderedProjects.Where(x => x.IsValid(runtime.Logger)))
            {
                // If we find just one valid configuration, everything is valid
                ReferencedFiles.UnionWith(project.AllAnalysisFiles(runtime.Logger));
                AddRoslynOutputFilePaths(project);
                AddAnalyzerOutputFilePaths(project);
                AddTelemetryFilePaths(project);
            }
            Status = ReferencedFiles.Any() ? ProjectInfoValidity.Valid : ProjectInfoValidity.NoFilesToAnalyze;
        }
        else
        {
            // If a project was created and destroyed during the build, it is no longer valid. For example, "dotnet ef bundle" scaffolds and then removes a project.
            Status = ProjectInfoValidity.ProjectNotFound;
        }
    }

    private void AddAnalyzerOutputFilePaths(ProjectInfo project)
    {
        if (project.AnalysisSettings.FirstOrDefault(x => ScannerEngineInputGenerator.IsProjectOutPaths(x.Id)) is { } property)
        {
            foreach (var filePath in property.Value.Split(ScannerEngineInputGenerator.AnalyzerOutputPathsDelimiter))
            {
                AnalyzerOutPaths.Add(new FileInfo(filePath));
            }
        }
    }

    private void AddRoslynOutputFilePaths(ProjectInfo project)
    {
        if (project.AnalysisSettings.FirstOrDefault(x => ScannerEngineInputGenerator.IsReportFilePaths(x.Id)) is { } property)
        {
            foreach (var filePath in property.Value.Split(ScannerEngineInputGenerator.RoslynReportPathsDelimiter))
            {
                RoslynReportFilePaths.Add(new FileInfo(filePath));
            }
        }
    }

    private void AddTelemetryFilePaths(ProjectInfo project)
    {
        foreach (var property in project.AnalysisSettings.Where(x => ScannerEngineInputGenerator.IsTelemetryPaths(x.Id)))
        {
            TelemetryPaths.Add(new FileInfo(property.Value));
        }
    }
}
