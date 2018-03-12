/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.Shim
{
    public class PropertiesWriter
    {
        private readonly ILogger logger;
        private readonly StringBuilder sb;
        private readonly AnalysisConfig config;

        /// <summary>
        /// List of projects that for which settings have been written
        /// </summary>
        private readonly IList<ProjectInfo> projects;

        #region Public methods

        public static string Escape(string value)
        {
            if (value == null)
            {
                return null;
            }

            var builder = new StringBuilder();

            foreach (var c in value)
            {
                if (c == '\\')
                {
                    builder.Append("\\\\");
                }
                else if (IsAscii(c) && !char.IsControl(c))
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append("\\u");
                    builder.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                }
            }

            return builder.ToString();
        }

        public static string Escape(FileInfo fileInfo) => Escape(fileInfo.FullName);

        public PropertiesWriter(AnalysisConfig config, ILogger logger)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.sb = new StringBuilder();
            projects = new List<ProjectInfo>();
        }

        public bool FinishedWriting { get; private set; }

        /// <summary>
        /// Finishes writing out any additional data then returns the whole of the content
        /// </summary>
        public string Flush()
        {
            if (FinishedWriting)
            {
                throw new InvalidOperationException();
            }

            FinishedWriting = true;

            Debug.Assert(projects.Select(p => p.ProjectGuid).Distinct().Count() == projects.Count,
                "Expecting the project guids to be unique");

            AppendKeyValue(sb, "sonar.modules", string.Join(",", projects.Select(p => p.GetProjectGuidAsString())));
            sb.AppendLine();

            return sb.ToString();
        }

        public void WriteSettingsForProject(ProjectData projectData)
        {
            if (FinishedWriting)
            {
                throw new InvalidOperationException();
            }

            if (projectData == null)
            {
                throw new ArgumentNullException(nameof(projectData));
            }

            Debug.Assert(projectData.ReferencedFiles.Count > 0, "Expecting a project to have files to analyze");
            Debug.Assert(projectData.SonarQubeModuleFiles.All(f => f.Exists), "Expecting all of the specified files to exist");

            projects.Add(projectData.Project);

            var guid = projectData.Project.GetProjectGuidAsString();

            AppendKeyValue(sb, guid, SonarProperties.ProjectKey, config.SonarProjectKey + ":" + guid);
            AppendKeyValue(sb, guid, SonarProperties.ProjectName, projectData.Project.ProjectName);
            AppendKeyValue(sb, guid, SonarProperties.ProjectBaseDir, projectData.Project.GetDirectory().FullName);

            if (!string.IsNullOrWhiteSpace(projectData.Project.Encoding))
            {
                AppendKeyValue(sb, guid, SonarProperties.SourceEncoding, projectData.Project.Encoding.ToLowerInvariant());
            }

            if (projectData.Project.ProjectType == ProjectType.Product)
            {
                sb.AppendLine(guid + @".sonar.sources=\");
            }
            else
            {
                AppendKeyValue(sb, guid, "sonar.sources", "");
                sb.AppendLine(guid + @".sonar.tests=\");
            }

            sb.AppendLine(EncodeAsSonarQubeMultiValueProperty(projectData.SonarQubeModuleFiles.Select(Escape)));
            sb.AppendLine();

            if (projectData.Project.AnalysisSettings != null && projectData.Project.AnalysisSettings.Any())
            {
                foreach (var setting in projectData.Project.AnalysisSettings)
                {
                    sb.AppendFormat("{0}.{1}={2}", guid, setting.Id, Escape(setting.Value));
                    sb.AppendLine();
                }

                WriteAnalyzerOutputPaths(projectData);
                WriteRoslynOutputPaths(projectData);

                sb.AppendLine();
            }
        }

        public void WriteAnalyzerOutputPaths(ProjectData project)
        {
            if (project.AnalyzerOutPaths.Count == 0)
            {
                return;
            }

            string property = null;
            if (ProjectLanguages.IsCSharpProject(project.Project.ProjectLanguage))
            {
                property = "sonar.cs.analyzer.projectOutPaths";
            }
            else if (ProjectLanguages.IsVbProject(project.Project.ProjectLanguage))
            {
                property = "sonar.vbnet.analyzer.projectOutPaths";
            }

            sb.AppendLine($"{project.Guid}.{property}=\\");
            sb.AppendLine(EncodeAsSonarQubeMultiValueProperty(project.AnalyzerOutPaths.Select(Escape)));
        }

        public void WriteRoslynOutputPaths(ProjectData project)
        {
            if (!project.RoslynReportFilePaths.Any())
            {
                return;
            }

            string property = null;
            if (ProjectLanguages.IsCSharpProject(project.Project.ProjectLanguage))
            {
                property = "sonar.cs.roslyn.reportFilePaths";
            }
            else if (ProjectLanguages.IsVbProject(project.Project.ProjectLanguage))
            {
                property = "sonar.vbnet.roslyn.reportFilePaths";
            }

            sb.AppendLine($"{project.Guid}.{property}=\\");
            sb.AppendLine(EncodeAsSonarQubeMultiValueProperty(project.RoslynReportFilePaths.Select(Escape)));
        }

        /// <summary>
        /// Write the supplied global settings into the file
        /// </summary>
        public void WriteGlobalSettings(AnalysisProperties properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            foreach (var setting in properties)
            {
                AppendKeyValue(sb, setting.Id, setting.Value);
            }
            sb.AppendLine();
        }

        public void WriteSonarProjectInfo(DirectoryInfo projectBaseDir)
        {
            AppendKeyValue(sb, SonarProperties.ProjectKey, config.SonarProjectKey);
            AppendKeyValueIfNotEmpty(sb, SonarProperties.ProjectName, config.SonarProjectName);
            AppendKeyValueIfNotEmpty(sb, SonarProperties.ProjectVersion, config.SonarProjectVersion);
            AppendKeyValue(sb, SonarProperties.WorkingDirectory, Path.Combine(config.SonarOutputDir, ".sonar"));
            AppendKeyValue(sb, SonarProperties.ProjectBaseDir, projectBaseDir.FullName);

            projects
                .Select(
                    (p, index) =>
                    {
                        var moduleWorkdir = Path.Combine(config.SonarOutputDir, ".sonar", "mod" + index);
                        return new { ProjectInfo = p, Workdir = moduleWorkdir };
                    })
                .ToList()
                .ForEach(t => AppendKeyValue(sb, t.ProjectInfo.GetProjectGuidAsString(), SonarProperties.WorkingDirectory,
                    t.Workdir));
        }

        public void WriteSharedFiles(IEnumerable<FileInfo> sharedFiles)
        {
            if (sharedFiles.Any())
            {
                sb.AppendLine(@"sonar.sources=\");
                sb.AppendLine(EncodeAsSonarQubeMultiValueProperty(sharedFiles.Select(Escape)));
            }

            sb.AppendLine();
        }

        #endregion Public methods

        #region Private methods

        private static void AppendKeyValue(StringBuilder sb, string keyPrefix, string keySuffix, string value)
        {
            AppendKeyValue(sb, keyPrefix + "." + keySuffix, value);
        }

        private static void AppendKeyValue(StringBuilder sb, string key, string value)
        {
            Debug.Assert(!ProcessRunnerArguments.ContainsSensitiveData(key) && !ProcessRunnerArguments.ContainsSensitiveData(value),
                "Not expecting sensitive data to be written to the sonar-project properties file. Key: {0}", key);

            sb.Append(key);
            sb.Append('=');
            sb.AppendLine(Escape(value));
        }

        private static void AppendKeyValueIfNotEmpty(StringBuilder sb, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                AppendKeyValue(sb, key, value);
            }
        }

        private static bool IsAscii(char c)
        {
            return c <= sbyte.MaxValue;
        }

        internal /* for testing purposes */ string EncodeAsSonarQubeMultiValueProperty(IEnumerable<string> paths)
        {
            var multiValuesPropertySeparator = $@",\{Environment.NewLine}";

            if (Version.TryParse(this.config.SonarQubeVersion, out var sonarqubeVersion) &&
                sonarqubeVersion.CompareTo(new Version(6, 5)) >= 0)
            {
                return string.Join(multiValuesPropertySeparator, paths.Select(path => $"\"{path.Replace("\"", "\"\"")}\""));
            }
            else
            {
                Func<string, bool> invalidPathPredicate = path => path.Contains(",");
                var invalidPaths = paths.Where(invalidPathPredicate);
                if (invalidPaths.Any())
                {
                    this.logger.LogWarning(Resources.WARN_InvalidCharacterInPaths, string.Join(", ", invalidPaths));
                }

                return string.Join(multiValuesPropertySeparator, paths.Where(path => !invalidPathPredicate(path)));
            }
        }

        #endregion Private methods
    }
}
