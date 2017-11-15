/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using System.Text;
using SonarQube.Common;
using SonarQube.Common.Interfaces;

namespace SonarScanner.Shim
{
    public class PropertiesFileGenerator
    {
        private const string ProjectPropertiesFileName = "sonar-project.properties";
        public const string ReportFileCsharpPropertyKey = "sonar.cs.roslyn.reportFilePath";
        public const string ReportFilesCsharpPropertyKey = "sonar.cs.roslyn.reportFilePaths";
        public const string ReportFileVbnetPropertyKey = "sonar.vbnet.roslyn.reportFilePath";
        public const string ReportFilesVbnetPropertyKey = "sonar.vbnet.roslyn.reportFilePaths";

        private readonly AnalysisConfig analysisConfig;
        private readonly ILogger logger;
        private readonly IRoslynV1SarifFixer fixer;

        public /*for testing*/ PropertiesFileGenerator(AnalysisConfig analysisConfig, ILogger logger,
            IRoslynV1SarifFixer fixer)
        {
            this.analysisConfig = analysisConfig ?? throw new ArgumentNullException(nameof(analysisConfig));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.fixer = fixer ?? throw new ArgumentNullException(nameof(fixer));
        }

        public PropertiesFileGenerator(AnalysisConfig analysisConfig, ILogger logger)
            : this(analysisConfig, logger, new RoslynV1SarifFixer())
        {
        }

        /// <summary>
        /// Locates the ProjectInfo.xml files and uses the information in them to generate
        /// a sonar-scanner properties file
        /// </summary>
        /// <returns>Information about each of the project info files that was processed, together with
        /// the full path to generated file.
        /// Note: the path to the generated file will be null if the file could not be generated.</returns>
        public ProjectInfoAnalysisResult GenerateFile()
        {
            string projectPropertiesPath = Path.Combine(analysisConfig.SonarOutputDir, ProjectPropertiesFileName);
            logger.LogDebug(Resources.MSG_GeneratingProjectProperties, projectPropertiesPath);

            var result = new ProjectInfoAnalysisResult();

            var writer = new PropertiesWriter(analysisConfig);

            IEnumerable<ProjectData> projects;
            var success = TryWriteProperties(writer, out projects);

            if (success)
            {
                string contents = writer.Flush();

                File.WriteAllText(projectPropertiesPath, contents, Encoding.ASCII);

                result.FullPropertiesFilePath = projectPropertiesPath;
            }

            result.Projects.AddRange(projects);

            return result;
        }

        public bool TryWriteProperties(PropertiesWriter writer, out IEnumerable<ProjectData> allProjects)
        {
            var projects = ProjectLoader.LoadFrom(analysisConfig.SonarOutputDir);

            if (projects == null || !projects.Any())
            {
                logger.LogError(Resources.ERR_NoProjectInfoFilesFound);
                allProjects = Enumerable.Empty<ProjectData>();
                return false;
            }

            var projectPaths = projects.Select(p => p.GetProjectDirectory()).ToList();

            var analysisProperties = analysisConfig.ToAnalysisProperties(logger);

            FixSarifAndEncoding(projects, analysisProperties);

            var rootProjectBaseDir = ComputeRootProjectBaseDir(projectPaths);

            allProjects = projects
                .GroupBy(p => p.ProjectGuid)
                .Select(g => ToProjectData(g, rootProjectBaseDir))
                .ToList();

            var validProjects = allProjects
                .Where(d => d.Status == ProjectInfoValidity.Valid)
                .ToList();

            if (validProjects.Any())
            {
                var sharedFilePaths = validProjects
                    .SelectMany(p => p.ExternalFiles)
                    .Where(f => !PathHelper.IsPartOfAProject(f, projectPaths))
                    .Distinct(StringComparer.InvariantCultureIgnoreCase)
                    .ToList();

                writer.WriteSonarProjectInfo(rootProjectBaseDir);
                writer.WriteSharedFiles(sharedFilePaths);

                foreach (var projectData in validProjects)
                {
                    writer.WriteSettingsForProject(projectData,
                        projectData.CoverageAnalysisExists(logger) ? projectData.VisualStudioCoverageLocation : null);
                }

                // Handle global settings
                writer.WriteGlobalSettings(analysisProperties);

                return true;
            }
            else
            {
                logger.LogError(Resources.ERR_NoValidProjectInfoFiles);

                return false;
            }
        }

        private ProjectData ToProjectData(IGrouping<Guid, ProjectInfo> projects, string rootProjectBaseDir)
        {
            // Shouldn't really matter which project is taken
            var projectData = new ProjectData(projects.First())
            {
                Status = ProjectInfoValidity.ExcludeFlagSet
            };

            if (projects.Key == Guid.Empty)
            {
                projectData.Status = ProjectInfoValidity.InvalidGuid;
                return projectData;
            }

            foreach (var p in projects)
            {
                var status = p.Classify(logger);
                // If we find just one valid configuration, everything is valid
                if (status == ProjectInfoValidity.Valid)
                {
                    projectData.Status = ProjectInfoValidity.Valid;
                    AddProjectFiles(p, projectData, rootProjectBaseDir);
                    AddRoslynOutputFilePath(p, projectData);
                    AddAnalyzerOutputFilePath(p, projectData);
                }
            }

            if (!projectData.HasFiles)
            {
                projectData.Status = ProjectInfoValidity.NoFilesToAnalyze;
            }

            return projectData;
        }

        private void AddAnalyzerOutputFilePath(ProjectInfo project, ProjectData projectData)
        {
            var property = project.AnalysisSettings.FirstOrDefault(p => p.Id.EndsWith(".analyzer.projectOutPath"));
            if (property != null)
            {
                projectData.AnalyzerOutPaths.Add(property.Value);
            }
        }

        private void AddRoslynOutputFilePath(ProjectInfo project, ProjectData projectData)
        {
            var property = project.AnalysisSettings.FirstOrDefault(p => p.Id.EndsWith(".roslyn.reportFilePath"));
            if (property != null)
            {
                projectData.RoslynReportFilePaths.Add(property.Value);
            }
        }

        private void FixSarifAndEncoding(IList<ProjectInfo> projects, AnalysisProperties analysisProperties)
        {
            var globalSourceEncoding = GetSourceEncoding(analysisProperties, new EncodingProvider());

            foreach (var project in projects)
            {
                TryFixSarifReport(project);
                FixEncoding(project, globalSourceEncoding);
            }
        }

        private void TryFixSarifReport(ProjectInfo project)
        {
            TryFixSarifReport(project, RoslynV1SarifFixer.CSharpLanguage, ReportFileCsharpPropertyKey);
            TryFixSarifReport(project, RoslynV1SarifFixer.VBNetLanguage, ReportFileVbnetPropertyKey);
        }

        /// <summary>
        /// Appends the sonar.projectBaseDir value. This is calculated as follows:
        /// 1. the user supplied value, or if none
        /// 2. the sources directory if running from TFS Build or XAML Build, or
        /// 3. the common root path of projects, or if there isn't any
        /// 4. the .sonarqube/out directory
        /// </summary>
        public string ComputeRootProjectBaseDir(IEnumerable<string> projectPaths)
        {
            string projectBaseDir = analysisConfig.GetConfigValue(SonarProperties.ProjectBaseDir, null);
            if (!string.IsNullOrWhiteSpace(projectBaseDir))
            {
                return projectBaseDir;
            }

            projectBaseDir = analysisConfig.SourcesDirectory;
            if (!string.IsNullOrWhiteSpace(projectBaseDir))
            {
                return projectBaseDir;
            }

            projectBaseDir = PathHelper.GetCommonRoot(projectPaths);
            if (!string.IsNullOrWhiteSpace(projectBaseDir))
            {
                return projectBaseDir;
            }

            return analysisConfig.SonarOutputDir;
        }

        /// <summary>
        /// Loads SARIF reports from the given projects and attempts to fix
        /// improper escaping from Roslyn V1 (VS 2015 RTM) where appropriate.
        /// </summary>
        private void TryFixSarifReport(ProjectInfo project, string language, string reportFilePropertyKey)
        {
            Property reportPathProperty;
            bool tryResult = project.TryGetAnalysisSetting(reportFilePropertyKey, out reportPathProperty);
            if (tryResult)
            {
                string reportPath = reportPathProperty.Value;
                string fixedPath = fixer.LoadAndFixFile(reportPath, language, logger);

                if (!reportPath.Equals(fixedPath)) // only need to alter the property if there was no change
                {
                    // remove the property ahead of changing it
                    // if the new path is null, the file was unfixable and we should leave the property out
                    project.AnalysisSettings.Remove(reportPathProperty);

                    if (fixedPath != null)
                    {
                        // otherwise, set the property value (results in no change if the file was already valid)
                        var newReportPathProperty = new Property
                        {
                            Id = reportFilePropertyKey,
                            Value = fixedPath,
                        };
                        project.AnalysisSettings.Add(newReportPathProperty);
                    }
                }
            }
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

        /// <summary>
        /// Returns all of the valid files that can be analyzed. Logs warnings/info about
        /// files that cannot be analyzed.
        /// </summary>
        private void AddProjectFiles(ProjectInfo projectInfo, ProjectData projectData, string rootProjectBaseDir)
        {
            var projectDirectory = projectInfo.GetProjectDirectory();

            foreach (string file in projectInfo.GetAllAnalysisFiles())
            {
                if (File.Exists(file))
                {
                    if (PathHelper.IsInFolder(file, projectDirectory))
                    {
                        projectData.ProjectFiles.Add(file);
                    }
                    else if (PathHelper.IsInFolder(file, rootProjectBaseDir))
                    {
                        projectData.ExternalFiles.Add(file);
                    }
                    else
                    {
                        logger.LogWarning(Resources.WARN_FileIsOutsideProjectDirectory, file, projectInfo.FullPath);
                    }
                }
                else
                {
                    logger.LogWarning(Resources.WARN_FileDoesNotExist, file);
                }
            }
        }
    }
}