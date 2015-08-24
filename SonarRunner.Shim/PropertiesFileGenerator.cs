//-----------------------------------------------------------------------
// <copyright file="PropertiesFileGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SonarRunner.Shim
{
    public static class PropertiesFileGenerator
    {
        private const string ProjectPropertiesFileName = "sonar-project.properties";

        public const string VSBootstrapperPropertyKey = "sonar.visualstudio.enable";

        #region Public methods

        /// <summary>
        /// Locates the ProjectInfo.xml files and uses the information in them to generate
        /// a sonar-runner properties file
        /// </summary>
        /// <returns>Information about each of the project info files that was processed, together with
        /// the full path to generated file.
        /// Note: the path to the generated file will be null if the file could not be generated.</returns>
        public static ProjectInfoAnalysisResult GenerateFile(AnalysisConfig config, ILogger logger)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            
            string fileName = Path.Combine(config.SonarOutputDir, ProjectPropertiesFileName);
            logger.LogDebug(Resources.MSG_GeneratingProjectProperties, fileName);

            IEnumerable<ProjectInfo> projects = ProjectLoader.LoadFrom(config.SonarOutputDir);
            if (projects == null || !projects.Any())
            {
                logger.LogError(Resources.ERR_NoProjectInfoFilesFound);
                return new ProjectInfoAnalysisResult();
            }

            PropertiesWriter writer = new PropertiesWriter(config);

            ProjectInfoAnalysisResult result = ProcessProjectInfoFiles(projects, writer, logger);
    
            IEnumerable<ProjectInfo> validProjects = result.GetProjectsByStatus(ProjectInfoValidity.Valid);

            if (validProjects.Any())
            {
                // Handle global settings
                AnalysisProperties properties = GetAnalysisProperties(config, logger);
                writer.WriteGlobalSettings(properties);

                string contents = writer.Flush();

                result.FullPropertiesFilePath = fileName;
                File.WriteAllText(result.FullPropertiesFilePath, contents, Encoding.ASCII);
            }
            else
            {
                // if the user tries to build multiple configurations at once there will be duplicate projects
                if (result.GetProjectsByStatus(ProjectInfoValidity.DuplicateGuid).Any())
                {
                    logger.LogError(Resources.ERR_NoValidButDuplicateProjects);
                }
                else
                {
                    logger.LogError(Resources.ERR_NoValidProjectInfoFiles);
                }
            }
            return result;
        }

        #endregion

        #region Private methods

        private static ProjectInfoAnalysisResult ProcessProjectInfoFiles(IEnumerable<ProjectInfo> projects, PropertiesWriter writer, ILogger logger)
        {
            ProjectInfoAnalysisResult result = new ProjectInfoAnalysisResult();

            foreach (ProjectInfo projectInfo in projects)
            {
                ProjectInfoValidity status = ClassifyProject(projectInfo, projects, logger);

                if (status == ProjectInfoValidity.Valid)
                {
                    IEnumerable<string> files = GetFilesToAnalyze(projectInfo, logger);
                    if (files == null || !files.Any())
                    {
                        status = ProjectInfoValidity.NoFilesToAnalyze;
                    }
                    else
                    {
                        string fxCopReport = TryGetFxCopReport(projectInfo, logger);
                        string vsCoverageReport = TryGetCodeCoverageReport(projectInfo, logger);
                        writer.WriteSettingsForProject(projectInfo, files, fxCopReport, vsCoverageReport);
                    }
                }

                result.Projects.Add(projectInfo, status);
            }
            return result;
        }

        private static ProjectInfoValidity ClassifyProject(ProjectInfo projectInfo, IEnumerable<ProjectInfo> projects, ILogger logger)
        {
            if (projectInfo.IsExcluded)
            {
                logger.LogInfo(Resources.MSG_ProjectIsExcluded, projectInfo.FullPath);
                return ProjectInfoValidity.ExcludeFlagSet;
            }

            if (!IsProjectGuidValue(projectInfo))
            {
                logger.LogWarning(Resources.WARN_InvalidProjectGuid, projectInfo.ProjectGuid, projectInfo.FullPath);
                return ProjectInfoValidity.InvalidGuid;
            }

            if (HasDuplicateGuid(projectInfo, projects))
            {
                logger.LogWarning(Resources.WARN_DuplicateProjectGuid, projectInfo.ProjectGuid, projectInfo.FullPath);
                return ProjectInfoValidity.DuplicateGuid;
            }

            return ProjectInfoValidity.Valid;
        }

        private static bool IsProjectGuidValue(ProjectInfo project)
        {
            return project.ProjectGuid != Guid.Empty;
        }

        private static bool HasDuplicateGuid(ProjectInfo projectInfo, IEnumerable<ProjectInfo> projects)
        {
            return projects.Count(p => !p.IsExcluded && p.ProjectGuid == projectInfo.ProjectGuid) > 1;
        }

        /// <summary>
        /// Returns all of the valid files that can be analyzed. Logs warnings/info about
        /// files that cannot be analyzed.
        /// </summary>
        private static IEnumerable<string> GetFilesToAnalyze(ProjectInfo projectInfo, ILogger logger)
        {
            // We're only interested in files that exist and that are under the project root
            var result = new List<string>();
            var baseDir = projectInfo.GetProjectDirectory();

            foreach (string file in projectInfo.GetAllAnalysisFiles())
            {
                if (File.Exists(file))
                {
                    if (IsInFolder(file, baseDir))
                    {
                        result.Add(file);
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
            return result;

        }

        private static bool IsInFolder(string filePath, string folder)
        {
            string normalizedPath = Path.GetDirectoryName(Path.GetFullPath(filePath));
            return normalizedPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        }

        private static string TryGetFxCopReport(ProjectInfo project, ILogger logger)
        {
            string fxCopReport = project.TryGetAnalysisFileLocation(AnalysisType.FxCop);
            if (fxCopReport != null)
            {
                if (!File.Exists(fxCopReport))
                {
                    fxCopReport = null;
                    logger.LogWarning(Resources.WARN_FxCopReportNotFound, fxCopReport);
                }
            }

            return fxCopReport;
        }

        private static string TryGetCodeCoverageReport(ProjectInfo project, ILogger logger)
        {
            string vsCoverageReport = project.TryGetAnalysisFileLocation(AnalysisType.VisualStudioCodeCoverage);
            if (vsCoverageReport != null)
            {
                if (!File.Exists(vsCoverageReport))
                {
                    vsCoverageReport = null;
                    logger.LogWarning(Resources.WARN_CodeCoverageReportNotFound, vsCoverageReport);
                }
            }
            return vsCoverageReport;
        }

        private static AnalysisProperties GetAnalysisProperties(AnalysisConfig config, ILogger logger)
        {
            AnalysisProperties properties = config.LocalSettings ?? new AnalysisProperties();

            // There are some properties we want to override regardless of what the user sets
            AddOrSetProperty(VSBootstrapperPropertyKey, "false", properties, logger);

            return properties;
        }

        private static void AddOrSetProperty(string key, string value, AnalysisProperties properties, ILogger logger)
        {
            Property property;
            Property.TryGetProperty(key, properties, out property);
            if (property == null)
            {
                logger.LogDebug(Resources.MSG_SettingAnalysisProperty, key, value);
                property = new Property() { Id = key, Value = value };
                properties.Add(property);
            }
            else
            {
                if (string.Equals(property.Value, value, StringComparison.InvariantCulture))
                {
                    logger.LogDebug(Resources.MSG_MandatorySettingIsCorrectlySpecified, key, value);
                }
                else
                {
                    logger.LogWarning(Resources.WARN_OverridingAnalysisProperty, key, value);
                    property.Value = value;
                }
            }
        }

        #endregion

    }
}
