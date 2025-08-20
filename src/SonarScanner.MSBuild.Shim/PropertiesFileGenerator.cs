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

using System.Runtime.InteropServices;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.Shim.Interfaces;
using EncodingProvider = SonarScanner.MSBuild.Common.EncodingProvider;

namespace SonarScanner.MSBuild.Shim;

public class PropertiesFileGenerator
{
    public const string ReportFilePathsCSharpPropertyKey = "sonar.cs.roslyn.reportFilePaths";
    public const string ReportFilePathsVbNetPropertyKey = "sonar.vbnet.roslyn.reportFilePaths";
    public const string ProjectOutPathsCsharpPropertyKey = "sonar.cs.analyzer.projectOutPaths";
    public const string ProjectOutPathsVbNetPropertyKey = "sonar.vbnet.analyzer.projectOutPaths";
    public const string TelemetryPathsCsharpPropertyKey = "sonar.cs.scanner.telemetry";
    public const string TelemetryPathsVbNetPropertyKey = "sonar.vbnet.scanner.telemetry";

    // This delimiter needs to be the same as the one used in the Integration.targets
    internal const char RoslynReportPathsDelimiter = '|';
    internal const char AnalyzerOutputPathsDelimiter = ',';

    private const string ProjectPropertiesFileName = "sonar-project.properties";

    private readonly AnalysisConfig analysisConfig;
    private readonly ILogger logger;
    private readonly IRoslynV1SarifFixer fixer;
    private readonly IRuntimeInformationWrapper runtimeInformationWrapper;
    private readonly IAdditionalFilesService additionalFilesService;
    private readonly StringComparer pathComparer;
    private readonly StringComparison pathComparison;

    public PropertiesFileGenerator(AnalysisConfig analysisConfig, ILogger logger)
        : this(analysisConfig, logger, new RoslynV1SarifFixer(logger), new RuntimeInformationWrapper(), new AdditionalFilesService(DirectoryWrapper.Instance, logger))
    {
    }

    internal /*for testing*/ PropertiesFileGenerator(AnalysisConfig analysisConfig,
                                                     ILogger logger,
                                                     IRoslynV1SarifFixer fixer,
                                                     IRuntimeInformationWrapper runtimeInformationWrapper,
                                                     IAdditionalFilesService additionalFilesService)
    {
        this.analysisConfig = analysisConfig ?? throw new ArgumentNullException(nameof(analysisConfig));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.fixer = fixer ?? throw new ArgumentNullException(nameof(fixer));
        this.runtimeInformationWrapper = runtimeInformationWrapper ?? throw new ArgumentNullException(nameof(runtimeInformationWrapper));
        this.additionalFilesService = additionalFilesService ?? throw new ArgumentNullException(nameof(additionalFilesService));
        if (runtimeInformationWrapper.IsOS(OSPlatform.Windows))
        {
            pathComparer = StringComparer.OrdinalIgnoreCase;
            pathComparison = StringComparison.OrdinalIgnoreCase;
        }
        else
        {
            pathComparer = StringComparer.Ordinal;
            pathComparison = StringComparison.Ordinal;
        }
    }

    public static bool IsReportFilePaths(string propertyKey) =>
        propertyKey == ReportFilePathsCSharpPropertyKey || propertyKey == ReportFilePathsVbNetPropertyKey;

    public static bool IsProjectOutPaths(string propertyKey) =>
        propertyKey == ProjectOutPathsCsharpPropertyKey || propertyKey == ProjectOutPathsVbNetPropertyKey;

    public static bool IsTelemetryPaths(string propertyKey) =>
        propertyKey == TelemetryPathsCsharpPropertyKey || propertyKey == TelemetryPathsVbNetPropertyKey;

    /// <summary>
    /// Locates the ProjectInfo.xml files and uses the information in them to generate a sonar-project.properties file.
    /// </summary>
    /// <returns>Information about each of the project info files that was processed, together with the full path to the generated sonar-project.properties file.
    /// Note: The path to the generated file will be null if the file could not be generated.</returns>
    public virtual ProjectInfoAnalysisResult GenerateFile()
    {
        var projectPropertiesPath = Path.Combine(analysisConfig.SonarOutputDir, ProjectPropertiesFileName);
        var result = new ProjectInfoAnalysisResult();
        var propertiesFileWriter = new PropertiesWriter(analysisConfig);
        var engineInput = new ScannerEngineInput(analysisConfig);
        logger.LogDebug(Resources.MSG_GeneratingProjectProperties, projectPropertiesPath);
        if (TryWriteProperties(propertiesFileWriter, engineInput, out var projects))
        {
            var contents = propertiesFileWriter.Flush();
            File.WriteAllText(projectPropertiesPath, contents, Encoding.ASCII);
            logger.LogDebug(Resources.DEBUG_DumpSonarProjectProperties, contents);
            result.FullPropertiesFilePath = projectPropertiesPath;
        }
        else
        {
            logger.LogInfo(Resources.MSG_PropertiesGenerationFailed);
        }
        result.Projects.AddRange(projects);
        return result;
    }

    // FixMe: Delete this method after implementing JsonWriter
    public virtual bool TryWriteProperties(PropertiesWriter writer, out IEnumerable<ProjectData> allProjects) =>
        TryWriteProperties(writer, null, ProjectLoader.LoadFrom(analysisConfig.SonarOutputDir).ToArray(), out allProjects);

    public bool TryWriteProperties(PropertiesWriter writer, ScannerEngineInput engineInput, out IEnumerable<ProjectData> allProjects) =>
        TryWriteProperties(writer, engineInput, ProjectLoader.LoadFrom(analysisConfig.SonarOutputDir).ToArray(), out allProjects);

    public bool TryWriteProperties(PropertiesWriter writer, ScannerEngineInput engineInput, IList<ProjectInfo> projects, out IEnumerable<ProjectData> allProjects)
    {
        if (projects.Count == 0)
        {
            logger.LogError(Resources.ERR_NoProjectInfoFilesFound);
            allProjects = [];
            return false;
        }

        var analysisProperties = analysisConfig.ToAnalysisProperties(logger);
        FixSarifAndEncoding(projects, analysisProperties);
        allProjects = projects.GroupBy(x => x.ProjectGuid).Select(ToProjectData).ToList();
        var validProjects = allProjects.Where(x => x.Status == ProjectInfoValidity.Valid).ToList();
        if (validProjects.Count == 0)
        {
            logger.LogError(Resources.ERR_NoValidProjectInfoFiles);
            return false;
        }

        var projectDirectories = validProjects.Select(x => x.Project.GetDirectory()).ToList();
        var projectBaseDir = ComputeProjectBaseDir(projectDirectories);
        if (projectBaseDir is null)
        {
            logger.LogError(Resources.ERR_ProjectBaseDirCannotBeAutomaticallyDetected);
            return false;
        }
        if (!projectBaseDir.Exists)
        {
            logger.LogError(Resources.ERR_ProjectBaseDirDoesNotExist);
            return false;
        }

        var analysisFiles = PutFilesToRightModuleOrRoot(validProjects, projectBaseDir);
        PostProcessProjectStatus(validProjects);

        if (analysisFiles.Sources.Count == 0
            && analysisFiles.Tests.Count == 0
            && validProjects.TrueForAll(x => x.Status == ProjectInfoValidity.NoFilesToAnalyze))
        {
            logger.LogError(Resources.ERR_NoValidProjectInfoFiles);
            return false;
        }

        writer.WriteSonarProjectInfo(projectBaseDir);
        writer.WriteSharedFiles(analysisFiles);
        foreach (var validProject in validProjects)
        {
            writer.WriteSettingsForProject(validProject);
        }
        writer.WriteGlobalSettings(analysisProperties);
        return true;
    }

    /// <summary>
    /// Appends the sonar.projectBaseDir value. This is calculated as follows:
    /// 1. the user supplied value, or if none
    /// 2. the sources directory if running from TFS Build or XAML Build, or
    /// 3. the SonarScannerWorkingDirectory if all analyzed path are within this directory, or
    /// 4. the common path prefix of projects in case there's a majority with a common root
    /// 5. otherwise, return null.
    /// </summary>
    public DirectoryInfo ComputeProjectBaseDir(IList<DirectoryInfo> projectPaths)
    {
        var projectBaseDir = analysisConfig.GetSettingOrDefault(SonarProperties.ProjectBaseDir, includeServerSettings: true, defaultValue: null, logger);
        if (!string.IsNullOrWhiteSpace(projectBaseDir))
        {
            var baseDirectory = new DirectoryInfo(projectBaseDir);
            logger.LogDebug(Resources.MSG_UsingUserSuppliedProjectBaseDir, baseDirectory.FullName);
            return baseDirectory;
        }
        else if (!string.IsNullOrWhiteSpace(analysisConfig.SourcesDirectory))
        {
            var baseDirectory = new DirectoryInfo(analysisConfig.SourcesDirectory);
            logger.LogDebug(Resources.MSG_UsingAzDoSourceDirectoryAsProjectBaseDir, baseDirectory.FullName);
            return baseDirectory;
        }
        logger.LogInfo(Resources.MSG_ProjectBaseDirChange);
        if (analysisConfig.SonarScannerWorkingDirectory is { } workingDirectoryPath
            && new DirectoryInfo(workingDirectoryPath) is { } workingDirectory
            && projectPaths.All(x => x.FullName.StartsWith(workingDirectory.FullName, pathComparison)))
        {
            logger.LogDebug(Resources.MSG_UsingWorkingDirectoryAsProjectBaseDir, workingDirectory.FullName);
            return workingDirectory;
        }
        else if (PathHelper.BestCommonPrefix(projectPaths, pathComparer) is { } commonPrefix)
        {
            logger.LogDebug(Resources.MSG_UsingLongestCommonBaseDir, commonPrefix.FullName, Environment.NewLine + string.Join($"{Environment.NewLine}", projectPaths.Select(x => x.FullName)));
            if (IsFileSystemRoot(commonPrefix))
            {
                // During build, depending on user configuration and dependencies, temporary projects can be created at locations that are not
                // under the user control. In such cases, the common root is wrongfully detected as the root of the file system.
                // Since we want to avoid using the root of the file system as the base directory, we will stop the automatic detection and ask the user
                // to provide a valid base directory.
                // A list of temporary projects that are automatically excluded can be found in `SonarQube.Integration.targets`. Look for `IsTempProject`.
                return null;
            }
            else
            {
                foreach (var projectOutsideCommonPrefix in projectPaths.Where(x => !x.FullName.StartsWith(commonPrefix.FullName, pathComparison)))
                {
                    logger.LogWarning(Resources.WARN_DirectoryIsOutsideBaseDir, projectOutsideCommonPrefix.FullName, commonPrefix.FullName);
                }
                return commonPrefix;
            }
        }
        else
        {
            return null;
        }
    }

    internal /* for testing */ static ProjectData SingleClosestProjectOrDefault(FileInfo fileInfo, IEnumerable<ProjectData> projects)
    {
        var length = 0;
        var closestProjects = new List<ProjectData>();
        foreach (var project in projects)
        {
            var projectDirectory = project.Project.GetDirectory();
            if (fileInfo.IsInDirectory(projectDirectory))
            {
                if (projectDirectory.FullName.Length == length)
                {
                    closestProjects.Add(project);
                }
                else if (projectDirectory.FullName.Length > length)
                {
                    length = projectDirectory.FullName.Length;
                    closestProjects = [project];
                }
                else
                {
                    // nothing to do
                }
            }
        }
        return closestProjects.Count >= 1 ? closestProjects[0] : null;
    }

    internal /* for testing */ ProjectData ToProjectData(IGrouping<Guid, ProjectInfo> projectsGroupedByGuid)
    {
        // To ensure consistently sending of metrics from the same configuration we sort the project outputs
        // and use only the first one for metrics.
        var orderedProjects = projectsGroupedByGuid.OrderBy(p => $"{p.Configuration}_{p.Platform}_{p.TargetFramework}").ToList();
        var projectData = new ProjectData(orderedProjects[0])
        {
            Status = ProjectInfoValidity.ExcludeFlagSet
        };
        // Find projects with different paths within the same group
        var isWindows = runtimeInformationWrapper.IsOS(OSPlatform.Windows);
        var projectPathsInGroup = projectsGroupedByGuid
            .Select(x => isWindows ? x.FullPath?.ToLowerInvariant() : x.FullPath)
            .Distinct()
            .ToList();

        if (projectPathsInGroup.Count > 1)
        {
            projectData.Status = ProjectInfoValidity.DuplicateGuid;
            foreach (var projectPath in projectPathsInGroup)
            {
                LogDuplicateGuidWarning(projectsGroupedByGuid.Key, projectPath);
            }
        }
        else if (projectsGroupedByGuid.Key == Guid.Empty)
        {
            projectData.Status = ProjectInfoValidity.InvalidGuid;
        }
        // If a project was created and destroyed during the build, it is no longer valid
        // For example, "dotnet ef bundle" scaffolds and then removes a project.
        else if (!File.Exists(projectData.Project.FullPath))
        {
            projectData.Status = ProjectInfoValidity.ProjectNotFound;
        }
        else
        {
            foreach (var project in orderedProjects)
            {
                var status = project.Classify(logger);
                // If we find just one valid configuration, everything is valid
                if (status == ProjectInfoValidity.Valid)
                {
                    projectData.Status = ProjectInfoValidity.Valid;
                    Array.ForEach(project.GetAllAnalysisFiles(logger), x => projectData.ReferencedFiles.Add(x));
                    AddRoslynOutputFilePaths(project, projectData);
                    AddAnalyzerOutputFilePaths(project, projectData);
                    AddTelemetryFilePaths(project, projectData);
                }
            }

            if (projectData.ReferencedFiles.Count == 0)
            {
                projectData.Status = ProjectInfoValidity.NoFilesToAnalyze;
            }
        }
        return projectData;
    }

    private static bool IsFileSystemRoot(DirectoryInfo directoryInfo) =>
        directoryInfo.Parent is null;

    /// <summary>
    ///     This method iterates through all referenced files and will either:
    ///     - Skip the file if:
    ///         - it doesn't exists
    ///         - it is located outside of the <see cref="rootProjectBaseDir"/> folder
    ///     - Add the file to the SonarQubeModuleFiles property of the only project it was referenced by (if the project was
    ///       found as being the closest folder to the file.
    ///     - Add the file to the list of files returns by this method in other cases.
    /// </summary>
    /// <remarks>
    ///     This method has some side effects.
    /// </remarks>
    /// <returns>The list of files to attach to the root module.</returns>
    private AnalysisFiles PutFilesToRightModuleOrRoot(IEnumerable<ProjectData> projects, DirectoryInfo baseDirectory)
    {
        var additionalFiles = additionalFilesService.AdditionalFiles(analysisConfig, baseDirectory);
        var projectsPerFile = ProjectsPerFile(projects);

        var rootSourceFiles = new HashSet<FileInfo>(new FileInfoEqualityComparer());
        foreach (var group in projectsPerFile)
        {
            var file = group.Key;
            if (!file.Exists)
            {
                logger.LogWarning(Resources.WARN_FileDoesNotExist, file);
                logger.LogDebug(Resources.DEBUG_FileReferencedByProjects, string.Join("', '", group.Value.Select(x => x.Project.FullPath)));
            }
            else if (!PathHelper.IsInDirectory(file, baseDirectory)) // File is outside of the SonarQube root module
            {
                if (!file.FullName.Contains(Path.Combine(".nuget", "packages")))
                {
                    logger.LogWarning(Resources.WARN_FileIsOutsideProjectDirectory, file, baseDirectory.FullName);
                }
                logger.LogDebug(Resources.DEBUG_FileReferencedByProjects, string.Join("', '", group.Value.Select(x => x.Project.FullPath)));
            }
            else if (group.Value.Length >= 1)
            {
                if (SingleClosestProjectOrDefault(file, group.Value) is { } closestProject)
                {
                    closestProject.SonarQubeModuleFiles.Add(file);
                }
                else
                {
                    rootSourceFiles.Add(file);
                }
            }
        }
        return new(rootSourceFiles, additionalFiles.Tests); // JS/TS does not use modules, so we don't need to put the test files in modules.

        Dictionary<FileInfo, ProjectData[]> ProjectsPerFile(IEnumerable<ProjectData> projectsData) =>
            projectsData
                .SelectMany(x => ProjectFiles(x).Where(f => !IsBinary(f)).Select(f => new { Project = x, File = f }))
                .GroupBy(x => x.File, FileInfoEqualityComparer.Instance)
                .ToDictionary(x => x.Key, x => x.Select(a => a.Project).ToArray());

        IEnumerable<FileInfo> ProjectFiles(ProjectData projectData) =>
            projectData.ReferencedFiles
                .Concat(additionalFiles.Sources)
                .Except(additionalFiles.Tests, FileInfoEqualityComparer.Instance); // the tests are removed here as they are reported at root level.

        bool IsBinary(FileInfo file) =>
            file.Extension.Equals(".exe", StringComparison.InvariantCultureIgnoreCase)
            || file.Extension.Equals(".dll", StringComparison.InvariantCultureIgnoreCase);
    }

    private static void PostProcessProjectStatus(IEnumerable<ProjectData> projects)
    {
        foreach (var project in projects)
        {
            if (project.SonarQubeModuleFiles.Count == 0)
            {
                project.Status = ProjectInfoValidity.NoFilesToAnalyze;
            }
        }
    }

    private void LogDuplicateGuidWarning(Guid projectGuid, string projectPath) =>
        logger.LogWarning(Resources.WARN_DuplicateProjectGuid, projectGuid, projectPath);

    private static void AddAnalyzerOutputFilePaths(ProjectInfo project, ProjectData projectData)
    {
        if (project.AnalysisSettings.FirstOrDefault(x => IsProjectOutPaths(x.Id)) is { } property)
        {
            foreach (var filePath in property.Value.Split(AnalyzerOutputPathsDelimiter))
            {
                projectData.AnalyzerOutPaths.Add(new FileInfo(filePath));
            }
        }
    }

    private static void AddRoslynOutputFilePaths(ProjectInfo project, ProjectData projectData)
    {
        if (project.AnalysisSettings.FirstOrDefault(x => IsReportFilePaths(x.Id)) is { } property)
        {
            foreach (var filePath in property.Value.Split(RoslynReportPathsDelimiter))
            {
                projectData.RoslynReportFilePaths.Add(new FileInfo(filePath));
            }
        }
    }

    private static void AddTelemetryFilePaths(ProjectInfo project, ProjectData projectData)
    {
        foreach (var property in project.AnalysisSettings.Where(x => IsTelemetryPaths(x.Id)))
        {
            projectData.TelemetryPaths.Add(new FileInfo(property.Value));
        }
    }

    private void FixSarifAndEncoding(IList<ProjectInfo> projects, AnalysisProperties analysisProperties)
    {
        var globalSourceEncoding = GetSourceEncoding(analysisProperties);
        Action logIfGlobalEncodingIsIgnored = () => logger.LogInfo(Resources.WARN_PropertyIgnored, SonarProperties.SourceEncoding);
        foreach (var project in projects)
        {
            TryFixSarifReport(project);
            project.FixEncoding(globalSourceEncoding, logIfGlobalEncodingIsIgnored);
        }

        static string GetSourceEncoding(AnalysisProperties properties)
        {
            try
            {
                var encodingProvider = new EncodingProvider();
                if (Property.TryGetProperty(SonarProperties.SourceEncoding, properties, out var encodingProperty))
                {
                    return encodingProvider.GetEncoding(encodingProperty.Value).WebName;
                }
            }
            catch
            {
                // encoding doesn't exist
            }
            return null;
        }
    }

    private void TryFixSarifReport(ProjectInfo project)
    {
        TryFixSarifReport(project, RoslynV1SarifFixer.CSharpLanguage, ReportFilePathsCSharpPropertyKey);
        TryFixSarifReport(project, RoslynV1SarifFixer.VBNetLanguage, ReportFilePathsVbNetPropertyKey);
    }

    /// <summary>
    /// Loads SARIF reports from the given projects and attempts to fix
    /// improper escaping from Roslyn V1 (VS 2015 RTM) where appropriate.
    /// </summary>
    private void TryFixSarifReport(ProjectInfo project, string language, string reportFilesPropertyKey)
    {
        if (project.TryGetAnalysisSetting(reportFilesPropertyKey, out Property reportPathsProperty))
        {
            project.AnalysisSettings.Remove(reportPathsProperty);
            var listOfPaths = reportPathsProperty.Value.Split(RoslynReportPathsDelimiter)
                .Select(x => fixer.LoadAndFixFile(x, language))
                .Where(x => x is not null)
                .ToArray();
            if (listOfPaths.Any())
            {
                project.AnalysisSettings.Add(new(reportFilesPropertyKey, string.Join(RoslynReportPathsDelimiter.ToString(), listOfPaths)));
            }
        }
    }
}
