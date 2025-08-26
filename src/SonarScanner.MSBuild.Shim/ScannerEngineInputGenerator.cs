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

using SonarScanner.MSBuild.Shim.Interfaces;
using EncodingProvider = SonarScanner.MSBuild.Common.EncodingProvider;

namespace SonarScanner.MSBuild.Shim;

public class ScannerEngineInputGenerator
{
    public const string ReportFilePathsKeyCS = "sonar.cs.roslyn.reportFilePaths";
    public const string ReportFilePathsKeyVB = "sonar.vbnet.roslyn.reportFilePaths";
    public const string ProjectOutPathsKeyCS = "sonar.cs.analyzer.projectOutPaths";
    public const string ProjectOutPathsKeyVB = "sonar.vbnet.analyzer.projectOutPaths";
    public const string TelemetryPathsKeyCS = "sonar.cs.scanner.telemetry";
    public const string TelemetryPathsKeyVB = "sonar.vbnet.scanner.telemetry";

    // This delimiter needs to be the same as the one used in the Integration.targets
    internal const char RoslynReportPathsDelimiter = '|';
    internal const char AnalyzerOutputPathsDelimiter = ',';

    private const string ProjectPropertiesFileName = "sonar-project.properties";

    private readonly AnalysisConfig analysisConfig;
    private readonly ILogger logger;
    private readonly IRoslynV1SarifFixer fixer;
    private readonly RuntimeInformationWrapper runtimeInformation;
    private readonly IAdditionalFilesService additionalFilesService;
    private readonly StringComparer pathComparer;
    private readonly StringComparison pathComparison;

    public ScannerEngineInputGenerator(AnalysisConfig analysisConfig, ILogger logger)
        : this(analysisConfig, logger, new RoslynV1SarifFixer(logger), new RuntimeInformationWrapper(), new AdditionalFilesService(DirectoryWrapper.Instance, logger))
    {
    }

    internal ScannerEngineInputGenerator(AnalysisConfig analysisConfig,
                                         ILogger logger,
                                         IRoslynV1SarifFixer fixer,
                                         RuntimeInformationWrapper runtimeInformation,
                                         IAdditionalFilesService additionalFilesService)
    {
        this.analysisConfig = analysisConfig ?? throw new ArgumentNullException(nameof(analysisConfig));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.fixer = fixer ?? throw new ArgumentNullException(nameof(fixer));
        this.runtimeInformation = runtimeInformation ?? throw new ArgumentNullException(nameof(runtimeInformation));
        this.additionalFilesService = additionalFilesService ?? throw new ArgumentNullException(nameof(additionalFilesService));
        if (runtimeInformation.IsWindows)
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
        propertyKey == ReportFilePathsKeyCS || propertyKey == ReportFilePathsKeyVB;

    public static bool IsProjectOutPaths(string propertyKey) =>
        propertyKey == ProjectOutPathsKeyCS || propertyKey == ProjectOutPathsKeyVB;

    public static bool IsTelemetryPaths(string propertyKey) =>
        propertyKey == TelemetryPathsKeyCS || propertyKey == TelemetryPathsKeyVB;

    /// <summary>
    /// Locates the ProjectInfo.xml files and uses the information in them to generate a sonar-project.properties file.
    /// </summary>
    /// <returns>Information about each of the project info files that was processed, together with the full path to the generated sonar-project.properties file.
    /// Note: The path to the generated file will be null if the file could not be generated.</returns>
    public virtual ProjectInfoAnalysisResult GenerateFile()
    {
        var projectPropertiesPath = Path.Combine(analysisConfig.SonarOutputDir, ProjectPropertiesFileName);
        var legacyWriter = new PropertiesWriter(analysisConfig);
        var engineInput = new ScannerEngineInput(analysisConfig);
        logger.LogDebug(Resources.MSG_GeneratingProjectProperties, projectPropertiesPath);
        var projects = ProjectLoader.LoadFrom(analysisConfig.SonarOutputDir).ToArray();
        if (projects.Length == 0)
        {
            logger.LogError(Resources.ERR_NoProjectInfoFilesFound);
            logger.LogInfo(Resources.MSG_PropertiesGenerationFailed);
            return new ProjectInfoAnalysisResult([]);
        }
        var analysisProperties = analysisConfig.ToAnalysisProperties(logger);
        FixSarifAndEncoding(projects, analysisProperties);
        var allProjects = projects.ToProjectData(runtimeInformation.IsWindows, logger);
        if (TryWriteProperties(analysisProperties, allProjects, legacyWriter, engineInput))
        {
            var contents = legacyWriter.Flush();
            File.WriteAllText(projectPropertiesPath, contents, Encoding.ASCII);
            logger.LogDebug(Resources.DEBUG_DumpSonarProjectProperties, contents);
            return new ProjectInfoAnalysisResult(allProjects, engineInput, projectPropertiesPath);
        }
        else
        {
            logger.LogInfo(Resources.MSG_PropertiesGenerationFailed);
            return new ProjectInfoAnalysisResult(allProjects);
        }
    }

    public bool TryWriteProperties(AnalysisProperties analysisProperties, ProjectData[] allProjects, PropertiesWriter legacyWriter, ScannerEngineInput engineInput)
    {
        var validProjects = allProjects.Where(x => x.Status == ProjectInfoValidity.Valid).ToArray();
        if (validProjects.Length == 0)
        {
            logger.LogError(Resources.ERR_NoValidProjectInfoFiles);
            return false;
        }

        var projectDirectories = validProjects.Select(x => x.Project.GetDirectory()).ToArray();
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
            && validProjects.All(x => x.Status == ProjectInfoValidity.NoFilesToAnalyze))
        {
            logger.LogError(Resources.ERR_NoValidProjectInfoFiles);
            return false;
        }

        legacyWriter.WriteSonarProjectInfo(projectBaseDir);
        engineInput.AddConfig(projectBaseDir);
        legacyWriter.WriteSharedFiles(analysisFiles);
        engineInput.AddSharedFiles(analysisFiles);
        foreach (var project in validProjects)
        {
            legacyWriter.WriteSettingsForProject(project);
            engineInput.AddProject(project);
            if (project.Project.AnalysisSettings is not null && project.Project.AnalysisSettings.Any())
            {
                foreach (var setting in project.Project.AnalysisSettings.Where(x =>
                    !IsProjectOutPaths(x.Id)
                    && !IsReportFilePaths(x.Id)
                    && !IsTelemetryPaths(x.Id)))
                {
                    engineInput.Add(project.Guid, setting.Id, setting.Value);
                }
            }
            AddProperty(engineInput, project, ProjectOutPathsKeyCS, ProjectOutPathsKeyVB, project.AnalyzerOutPaths);
            AddProperty(engineInput, project, ReportFilePathsKeyCS, ReportFilePathsKeyVB, project.RoslynReportFilePaths);
            AddProperty(engineInput, project, TelemetryPathsKeyCS, TelemetryPathsKeyVB, project.TelemetryPaths);
        }
        legacyWriter.WriteGlobalSettings(analysisProperties);
        engineInput.AddGlobalSettings(analysisProperties);
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
        TryFixSarifReport(project, RoslynV1SarifFixer.CSharpLanguage, ReportFilePathsKeyCS);
        TryFixSarifReport(project, RoslynV1SarifFixer.VBNetLanguage, ReportFilePathsKeyVB);
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

    private static void AddProperty(ScannerEngineInput engineInput, ProjectData project, string keySuffixCS, string keySuffixVB, IEnumerable<FileInfo> paths)
    {
        if (KeySuffix() is { } keySuffix)
        {
            engineInput.Add(project.Guid, keySuffix, paths);
        }

        string KeySuffix()
        {
            if (ProjectLanguages.IsCSharpProject(project.Project.ProjectLanguage))
            {
                return keySuffixCS;
            }
            else if (ProjectLanguages.IsVbProject(project.Project.ProjectLanguage))
            {
                return keySuffixVB;
            }
            else
            {
                return null;
            }
        }
    }
}
