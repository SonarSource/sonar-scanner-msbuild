/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2021 SonarSource SA
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
using System.Runtime.InteropServices;
using System.Text;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.Shim
{
    public class PropertiesFileGenerator : IPropertiesFileGenerator
    {
        private const string ProjectPropertiesFileName = "sonar-project.properties";
        public const string ReportFilePathsCSharpPropertyKey = "sonar.cs.roslyn.reportFilePaths";
        public const string ReportFilePathsVbNetPropertyKey = "sonar.vbnet.roslyn.reportFilePaths";
        public const string ProjectOutPathsCsharpPropertyKey = "sonar.cs.analyzer.projectOutPaths";
        public const string ProjectOutPathsVbNetPropertyKey = "sonar.vbnet.analyzer.projectOutPaths";

        // This delimiter needs to be the same as the one used in the Integration.targets
        internal const char RoslynReportPathsDelimiter = '|';
        internal const char AnalyzerOutputPathsDelimiter = ',';

        private readonly AnalysisConfig analysisConfig;
        private readonly ILogger logger;
        private readonly IRoslynV1SarifFixer fixer;
        private readonly IRuntimeInformationWrapper runtimeInformationWrapper;

        public /*for testing*/ PropertiesFileGenerator(AnalysisConfig analysisConfig, ILogger logger, IRoslynV1SarifFixer fixer, IRuntimeInformationWrapper runtimeInformationWrapper)
        {
            this.analysisConfig = analysisConfig ?? throw new ArgumentNullException(nameof(analysisConfig));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.fixer = fixer ?? throw new ArgumentNullException(nameof(fixer));
            this.runtimeInformationWrapper = runtimeInformationWrapper ?? throw new ArgumentNullException(nameof(runtimeInformationWrapper));
        }

        public PropertiesFileGenerator(AnalysisConfig analysisConfig, ILogger logger)
            : this(analysisConfig, logger, new RoslynV1SarifFixer(logger), new RuntimeInformationWrapper())
        {
        }

        public static bool IsReportFilePaths(string propertyKey) =>
            propertyKey == ReportFilePathsCSharpPropertyKey || propertyKey == ReportFilePathsVbNetPropertyKey;

        public static bool IsProjectOutPaths(string propertyKey) =>
            propertyKey == ProjectOutPathsCsharpPropertyKey || propertyKey == ProjectOutPathsVbNetPropertyKey;

        /// <summary>
        /// Locates the ProjectInfo.xml files and uses the information in them to generate
        /// a sonar-scanner properties file
        /// </summary>
        /// <returns>Information about each of the project info files that was processed, together with
        /// the full path to generated file.
        /// Note: the path to the generated file will be null if the file could not be generated.</returns>
        public ProjectInfoAnalysisResult GenerateFile()
        {
            var projectPropertiesPath = Path.Combine(analysisConfig.SonarOutputDir, ProjectPropertiesFileName);
            logger.LogDebug(Resources.MSG_GeneratingProjectProperties, projectPropertiesPath, SonarProduct.GetSonarProductToLog(analysisConfig.SonarQubeHostUrl));

            var result = new ProjectInfoAnalysisResult();
            var writer = new PropertiesWriter(analysisConfig, logger);
            var success = TryWriteProperties(writer, out IEnumerable<ProjectData> projects);
            if (success)
            {
                var contents = writer.Flush();
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

        public bool TryWriteProperties(PropertiesWriter writer, out IEnumerable<ProjectData> allProjects)
        {
            var projects = ProjectLoader.LoadFrom(analysisConfig.SonarOutputDir);

            if (!projects.Any())
            {
                logger.LogError(Resources.ERR_NoProjectInfoFilesFound, SonarProduct.GetSonarProductToLog(analysisConfig.SonarQubeHostUrl));
                allProjects = Enumerable.Empty<ProjectData>();
                return false;
            }

            var projectsWithoutGuid = projects.Where(p => p.ProjectGuid == Guid.Empty).ToList();
            if (projectsWithoutGuid.Count > 0)
            {
                logger.LogWarning(Resources.WARN_EmptyProjectGuids, string.Join(", ", projectsWithoutGuid.Select(p => p.FullPath)));
            }

            var projectDirectories = projects.Select(p => p.GetDirectory()).ToList();
            var analysisProperties = analysisConfig.ToAnalysisProperties(logger);
            FixSarifAndEncoding(projects, analysisProperties);

            allProjects = projects.GroupBy(p => p.ProjectGuid).Select(ToProjectData).ToList();
            var validProjects = allProjects.Where(p => p.Status == ProjectInfoValidity.Valid).ToList();

            if (validProjects.Count == 0)
            {
                logger.LogError(Resources.ERR_NoValidProjectInfoFiles, SonarProduct.GetSonarProductToLog(analysisConfig.SonarQubeHostUrl));
                return false;
            }

            var rootProjectBaseDir = ComputeRootProjectBaseDir(projectDirectories);
            if (rootProjectBaseDir == null ||
                !rootProjectBaseDir.Exists)
            {
                logger.LogError(Resources.ERR_ProjectBaseDirDoesNotExist);
                return false;
            }

            var rootModuleFiles = PutFilesToRightModuleOrRoot(validProjects, rootProjectBaseDir);
            PostProcessProjectStatus(validProjects);

            if (rootModuleFiles.Count == 0 &&
                validProjects.All(p => p.Status == ProjectInfoValidity.NoFilesToAnalyze))
            {
                logger.LogError(Resources.ERR_NoValidProjectInfoFiles, SonarProduct.GetSonarProductToLog(analysisConfig.SonarQubeHostUrl));
                return false;
            }

            writer.WriteSonarProjectInfo(rootProjectBaseDir);
            writer.WriteSharedFiles(rootModuleFiles);
            validProjects.ForEach(writer.WriteSettingsForProject);
            // Handle global settings
            writer.WriteGlobalSettings(analysisProperties);
            return true;
        }

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
        private ICollection<FileInfo> PutFilesToRightModuleOrRoot(IEnumerable<ProjectData> projects, DirectoryInfo baseDirectory)
        {
            var fileWithProjects = projects
                .SelectMany(p => p.ReferencedFiles.Select(f => new { Project = p, File = f }))
                .GroupBy(group => group.File, new FileInfoEqualityComparer())
                .ToDictionary(group => group.Key, group => group.Select(x => x.Project).ToList());

            var rootModuleFiles = new HashSet<FileInfo>(new FileInfoEqualityComparer());

            foreach (var group in fileWithProjects)
            {
                var file = group.Key;

                if (!file.Exists)
                {
                    logger.LogWarning(Resources.WARN_FileDoesNotExist, file);
                    logger.LogDebug(Resources.DEBUG_FileReferencedByProjects, string.Join("', '",
                        group.Value.Select(x => x.Project.FullPath)));
                    continue;
                }

                if (!PathHelper.IsInDirectory(file, baseDirectory)) // File is outside of the SonarQube root module
                {
                    logger.LogWarning(Resources.WARN_FileIsOutsideProjectDirectory, file, baseDirectory.FullName);
                    logger.LogDebug(Resources.DEBUG_FileReferencedByProjects, string.Join("', '",
                        group.Value.Select(x => x.Project.FullPath)));
                    continue;
                }

                if (group.Value.Count >= 1)
                {
                    var closestProject = GetSingleClosestProjectOrDefault(file, group.Value);

                    if (closestProject == null)
                    {
                        rootModuleFiles.Add(file);
                    }
                    else
                    {
                        closestProject.SonarQubeModuleFiles.Add(file);
                    }
                }
            }

            return rootModuleFiles;
        }

        private void PostProcessProjectStatus(IEnumerable<ProjectData> projects)
        {
            foreach (var project in projects)
            {
                if (project.SonarQubeModuleFiles.Count == 0)
                {
                    project.Status = ProjectInfoValidity.NoFilesToAnalyze;
                }
            }
        }

        internal /* for testing */ static ProjectData GetSingleClosestProjectOrDefault(FileInfo fileInfo,
            IEnumerable<ProjectData> projects)
        {
            var closestProjects = (Length: 0, Items: new List<ProjectData>());

            foreach (var project in projects)
            {
                var projectDirectory = project.Project.GetDirectory();

                if (!fileInfo.IsInDirectory(projectDirectory))
                {
                    continue;
                }

                if (projectDirectory.FullName.Length == closestProjects.Length)
                {
                    closestProjects.Items.Add(project);
                }
                else if (projectDirectory.FullName.Length > closestProjects.Length)
                {
                    closestProjects = (Length: projectDirectory.FullName.Length, Items: new List<ProjectData> { project });
                }
                else
                {
                    // nothing to do
                }
            }

            return closestProjects.Items.Count >= 1
                ? closestProjects.Items[0]
                : null;
        }

        internal /* for testing */ ProjectData ToProjectData(IGrouping<Guid, ProjectInfo> projectsGroupedByGuid)
        {
            // To ensure consistently sending of metrics from the same configuration we sort the project outputs
            // and use only the first one for metrics.
            var orderedProjects = projectsGroupedByGuid
                .OrderBy(p => $"{p.Configuration}_{p.Platform}_{p.TargetFramework}")
                .ToList();

            var projectData = new ProjectData(orderedProjects[0])
            {
                Status = ProjectInfoValidity.ExcludeFlagSet
            };

            // Find projects with different paths within the same group
            List<string> projectPathsInGroup = null;

            if (runtimeInformationWrapper.IsOS(OSPlatform.Windows))
            {
                projectPathsInGroup = projectsGroupedByGuid
                .Select(x => x.FullPath?.ToLowerInvariant())
                .Distinct()
                .ToList();
            }
            else
            {
                projectPathsInGroup = projectsGroupedByGuid
                .Select(x => x.FullPath)
                .Distinct()
                .ToList();
            }

            if (projectPathsInGroup.Count > 1)
            {
                projectData.Status = ProjectInfoValidity.DuplicateGuid;
                projectPathsInGroup.ForEach(path => LogDuplicateGuidWarning(projectsGroupedByGuid.Key, path));
                return projectData;
            }

            if (projectsGroupedByGuid.Key == Guid.Empty)
            {
                projectData.Status = ProjectInfoValidity.InvalidGuid;
                return projectData;
            }

            foreach (var p in orderedProjects)
            {
                var status = p.Classify(logger);
                // If we find just one valid configuration, everything is valid
                if (status == ProjectInfoValidity.Valid)
                {
                    projectData.Status = ProjectInfoValidity.Valid;
                    p.GetAllAnalysisFiles().ToList().ForEach(path => projectData.ReferencedFiles.Add(path));
                    AddRoslynOutputFilePaths(p, projectData);
                    AddAnalyzerOutputFilePaths(p, projectData);
                }
            }

            if (projectData.ReferencedFiles.Count == 0)
            {
                projectData.Status = ProjectInfoValidity.NoFilesToAnalyze;
            }

            return projectData;
        }

        private void LogDuplicateGuidWarning(Guid projectGuid, string projectPath) =>
            logger.LogWarning(Resources.WARN_DuplicateProjectGuid, projectGuid, projectPath, SonarProduct.GetSonarProductToLog(analysisConfig.SonarQubeHostUrl));

        private void AddAnalyzerOutputFilePaths(ProjectInfo project, ProjectData projectData)
        {
            var property = project.AnalysisSettings.FirstOrDefault(p => IsProjectOutPaths(p.Id));
            if (property != null)
            {
                foreach (var filePath in property.Value.Split(AnalyzerOutputPathsDelimiter))
                {
                    projectData.AnalyzerOutPaths.Add(new FileInfo(filePath));
                }
            }
        }

        private void AddRoslynOutputFilePaths(ProjectInfo project, ProjectData projectData)
        {
            var property = project.AnalysisSettings.FirstOrDefault(x => IsReportFilePaths(x.Id));
            if (property != null)
            {
                foreach (var filePath in property.Value.Split(RoslynReportPathsDelimiter))
                {
                    projectData.RoslynReportFilePaths.Add(new FileInfo(filePath));
                }
            }
        }

        private void FixSarifAndEncoding(IList<ProjectInfo> projects, AnalysisProperties analysisProperties)
        {
            var globalSourceEncoding = GetSourceEncoding(analysisProperties, new SonarScanner.MSBuild.Common.EncodingProvider());

            foreach (var project in projects)
            {
                TryFixSarifReport(project);
                FixEncoding(project, globalSourceEncoding);
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
            var tryResult = project.TryGetAnalysisSetting(reportFilesPropertyKey, out Property reportPathsProperty);
            if (tryResult)
            {
                var listOfPaths = new List<string>();
                project.AnalysisSettings.Remove(reportPathsProperty);
                foreach (var reportPath in reportPathsProperty.Value.Split(RoslynReportPathsDelimiter))
                {
                    var fixedPath = fixer.LoadAndFixFile(reportPath, language);

                    if (fixedPath != null)
                    {
                        listOfPaths.Add(fixedPath);
                    }
                }

                if (listOfPaths.Any())
                {
                    var newReportPathProperty = new Property
                    {
                        Id = reportFilesPropertyKey,
                        Value = string.Join(RoslynReportPathsDelimiter.ToString(), listOfPaths)
                    };
                    project.AnalysisSettings.Add(newReportPathProperty);
                }
            }
        }

        /// <summary>
        /// Appends the sonar.projectBaseDir value. This is calculated as follows:
        /// 1. the user supplied value, or if none
        /// 2. the sources directory if running from TFS Build or XAML Build, or
        /// 3. the common root path of projects, or if there isn't any
        /// 4. the .sonarqube/out directory
        /// </summary>
        public DirectoryInfo ComputeRootProjectBaseDir(IEnumerable<DirectoryInfo> projectPaths)
        {
            DirectoryInfo rootDirectory;

            var projectBaseDir = analysisConfig.LocalSettings
                ?.FirstOrDefault(p => ConfigSetting.SettingKeyComparer.Equals(SonarProperties.ProjectBaseDir, p.Id))
                ?.Value;
            if (!string.IsNullOrWhiteSpace(projectBaseDir))
            {
                rootDirectory = new DirectoryInfo(projectBaseDir);
                logger.LogDebug(Resources.MSG_UsingUserSuppliedProjectBaseDir, rootDirectory.FullName);
                return rootDirectory;
            }

            if (!string.IsNullOrWhiteSpace(analysisConfig.SourcesDirectory))
            {
                rootDirectory = new DirectoryInfo(analysisConfig.SourcesDirectory);
                logger.LogDebug(Resources.MSG_UsingAzDoSourceDirectoryAsProjectBaseDir, rootDirectory.FullName);
                return rootDirectory;
            }

            var commonRoot = PathHelper.GetCommonRoot(projectPaths);
            if (commonRoot != null)
            {
                logger.LogDebug(Resources.MSG_UsingLongestCommonRootProjectBaseDir, commonRoot.FullName);
                return commonRoot;
            }

            rootDirectory = new DirectoryInfo(analysisConfig.SonarOutputDir);
            logger.LogWarning(Resources.WARN_UsingFallbackProjectBaseDir, rootDirectory.FullName);
            return rootDirectory;
        }

        private static string GetSourceEncoding(AnalysisProperties properties, IEncodingProvider encodingProvider)
        {
            try
            {
                if (Property.TryGetProperty(SonarProperties.SourceEncoding, properties, out var encodingProperty))
                {
                    return encodingProvider.GetEncoding(encodingProperty.Value).WebName;
                }
            }
            catch (Exception)
            {
                // encoding doesn't exist
            }

            return null;
        }

        private void FixEncoding(ProjectInfo projectInfo, string globalSourceEncoding)
        {
            if (projectInfo.Encoding != null)
            {
                if (globalSourceEncoding != null)
                {
                    logger.LogInfo(Resources.WARN_PropertyIgnored, SonarProperties.SourceEncoding);
                }
            }
            else
            {
                if (globalSourceEncoding == null)
                {
                    if (ProjectLanguages.IsCSharpProject(projectInfo.ProjectLanguage) ||
                        ProjectLanguages.IsVbProject(projectInfo.ProjectLanguage))
                    {
                        projectInfo.Encoding = Encoding.UTF8.WebName;
                    }
                }
                else
                {
                    projectInfo.Encoding = globalSourceEncoding;
                }
            }
        }
    }
}
