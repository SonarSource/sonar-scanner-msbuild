/*
 * SonarQube Scanner for MSBuild
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
using SonarQube.Common;

namespace SonarScanner.Shim
{
    public class PropertiesWriter
    {
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

        public PropertiesWriter(AnalysisConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            sb = new StringBuilder();
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

            Debug.Assert(projects.Select(p => p.ProjectGuid).Distinct().Count() == projects.Count(),
                "Expecting the project guids to be unique");

            AppendKeyValue(sb, "sonar.modules", string.Join(",", projects.Select(p => p.GetProjectGuidAsString())));
            sb.AppendLine();

            return sb.ToString();
        }

        public void WriteSettingsForProject(ProjectData project)
        {
            if (FinishedWriting)
            {
                throw new InvalidOperationException();
            }

            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            Debug.Assert(project.ReferencedFiles.Count > 0, "Expecting a project to have files to analyze");
            Debug.Assert(project.SonarQubeModuleFiles.All(File.Exists), "Expecting all of the specified files to exist");

            projects.Add(project.Project);

            var guid = project.Project.GetProjectGuidAsString();

            AppendKeyValue(sb, guid, SonarProperties.ProjectKey, config.SonarProjectKey + ":" + guid);
            AppendKeyValue(sb, guid, SonarProperties.ProjectName, project.Project.ProjectName);
            AppendKeyValue(sb, guid, SonarProperties.ProjectBaseDir, project.Project.GetProjectDirectory());

            if (!string.IsNullOrWhiteSpace(project.Project.Encoding))
            {
                AppendKeyValue(sb, guid, SonarProperties.SourceEncoding, project.Project.Encoding.ToLowerInvariant());
            }

            if (project.Project.ProjectType == ProjectType.Product)
            {
                sb.AppendLine(guid + @".sonar.sources=\");
            }
            else
            {
                AppendKeyValue(sb, guid, "sonar.sources", "");
                sb.AppendLine(guid + @".sonar.tests=\");
            }

            var escapedFiles = project.SonarQubeModuleFiles.Select(Escape);
            sb.AppendLine(string.Join(@",\" + Environment.NewLine, escapedFiles));

            sb.AppendLine();

            if (project.Project.AnalysisSettings != null && project.Project.AnalysisSettings.Any())
            {
                foreach (var setting in project.Project.AnalysisSettings)
                {
                    sb.AppendFormat("{0}.{1}={2}", guid, setting.Id, Escape(setting.Value));
                    sb.AppendLine();
                }

                WriteAnalyzerOutputPaths(project);
                WriteRoslynOutputPaths(project);

                sb.AppendLine();
            }
        }

        public void WriteAnalyzerOutputPaths(ProjectData project)
        {
            if (!project.AnalyzerOutPaths.Any())
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
            sb.AppendLine(string.Join(@",\" + Environment.NewLine, project.AnalyzerOutPaths.Select(Escape)));
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
            sb.AppendLine(string.Join(@",\" + Environment.NewLine, project.RoslynReportFilePaths.Select(Escape)));
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

        public void WriteSonarProjectInfo(string projectBaseDir)
        {
            AppendKeyValue(sb, SonarProperties.ProjectKey, config.SonarProjectKey);
            AppendKeyValueIfNotEmpty(sb, SonarProperties.ProjectName, config.SonarProjectName);
            AppendKeyValueIfNotEmpty(sb, SonarProperties.ProjectVersion, config.SonarProjectVersion);
            AppendKeyValue(sb, SonarProperties.WorkingDirectory, Path.Combine(config.SonarOutputDir, ".sonar"));
            AppendKeyValue(sb, SonarProperties.ProjectBaseDir, projectBaseDir);

            projects.Select((p, index) =>
            {
                var moduleWorkdir = Path.Combine(config.SonarOutputDir, ".sonar", "mod" + index);
                return new { ProjectInfo = p, Workdir = moduleWorkdir };
            })
                .ToList()
                .ForEach(t => AppendKeyValue(sb, t.ProjectInfo.GetProjectGuidAsString(), SonarProperties.WorkingDirectory,
                    t.Workdir));
        }

        public void WriteSharedFiles(IEnumerable<string> sharedFiles)
        {
            if (sharedFiles.Any())
            {
                sb.AppendLine(@"sonar.sources=\");
                var escapedFiles = sharedFiles.Select(Escape);
                sb.AppendLine(string.Join(@",\" + Environment.NewLine, escapedFiles));
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

        #endregion Private methods
    }
}
