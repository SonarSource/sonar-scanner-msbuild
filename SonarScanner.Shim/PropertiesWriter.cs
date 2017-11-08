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

            StringBuilder builder = new StringBuilder();

            foreach (char c in value)
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
            this.config = config ?? throw new ArgumentNullException("config");
            this.sb = new StringBuilder();
            this.projects = new List<ProjectInfo>();
        }

        public bool FinishedWriting { get; private set; }

        /// <summary>
        /// Finishes writing out any additional data then returns the whole of the content
        /// </summary>
        public string Flush()
        {
            if (this.FinishedWriting)
            {
                throw new InvalidOperationException();
            }

            this.FinishedWriting = true;

            Debug.Assert(this.projects.Select(p => p.ProjectGuid).Distinct().Count() == projects.Count(),
                "Expecting the project guids to be unique");

            AppendKeyValue(sb, "sonar.modules", string.Join(",", this.projects.Select(p => p.GetProjectGuidAsString())));
            sb.AppendLine();

            return sb.ToString();
        }

        public void WriteSettingsForProject(ProjectInfo project, IEnumerable<string> files, string codeCoverageFilePath)
        {
            if (this.FinishedWriting)
            {
                throw new InvalidOperationException();
            }

            if (project == null)
            {
                throw new ArgumentNullException("project");
            }
            if (files == null)
            {
                throw new ArgumentNullException("files");
            }

            Debug.Assert(files.Any(), "Expecting a project to have files to analyze");
            Debug.Assert(files.All(f => File.Exists(f)), "Expecting all of the specified files to exist");

            this.projects.Add(project);

            string guid = project.GetProjectGuidAsString();

            AppendKeyValue(sb, guid, SonarProperties.ProjectKey, this.config.SonarProjectKey + ":" + guid);
            AppendKeyValue(sb, guid, SonarProperties.ProjectName, project.ProjectName);
            AppendKeyValue(sb, guid, SonarProperties.ProjectBaseDir, project.GetProjectDirectory());

            if (!string.IsNullOrWhiteSpace(project.Encoding))
            {
                AppendKeyValue(sb, guid, SonarProperties.SourceEncoding, project.Encoding.ToLowerInvariant());
            }

            if (codeCoverageFilePath != null)
            {
                AppendKeyValue(sb, guid, "sonar.cs.vscoveragexml.reportsPaths", codeCoverageFilePath);
            }

            if (project.ProjectType == ProjectType.Product)
            {
                sb.AppendLine(guid + @".sonar.sources=\");
            }
            else
            {
                AppendKeyValue(sb, guid, "sonar.sources", "");
                sb.AppendLine(guid + @".sonar.tests=\");
            }

            IEnumerable<string> escapedFiles = files.Select(Escape);
            sb.AppendLine(string.Join(@",\" + Environment.NewLine, escapedFiles));

            sb.AppendLine();

            if (project.AnalysisSettings != null && project.AnalysisSettings.Any())
            {
                foreach (Property setting in project.AnalysisSettings)
                {
                    sb.AppendFormat("{0}.{1}={2}", guid, setting.Id, Escape(setting.Value));
                    sb.AppendLine();
                }
                sb.AppendLine();
            }
        }

        /// <summary>
        /// Write the supplied global settings into the file
        /// </summary>
        public void WriteGlobalSettings(AnalysisProperties properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }

            foreach (Property setting in properties)
            {
                AppendKeyValue(this.sb, setting.Id, setting.Value);
            }
            sb.AppendLine();
        }

        public void WriteSonarProjectInfo(string projectBaseDir, ICollection<string> sharedFiles)
        {
            AppendKeyValue(sb, SonarProperties.ProjectKey, this.config.SonarProjectKey);
            AppendKeyValueIfNotEmpty(sb, SonarProperties.ProjectName, this.config.SonarProjectName);
            AppendKeyValueIfNotEmpty(sb, SonarProperties.ProjectVersion, this.config.SonarProjectVersion);
            AppendKeyValue(sb, SonarProperties.WorkingDirectory, Path.Combine(this.config.SonarOutputDir, ".sonar"));
            AppendKeyValue(sb, SonarProperties.ProjectBaseDir, projectBaseDir);

            this.projects.Select((p, index) =>
            {
                string moduleWorkdir = Path.Combine(this.config.SonarOutputDir, ".sonar", "mod" + index);
                return new { ProjectInfo = p, Workdir = moduleWorkdir };
            }).ToList().ForEach(t => AppendKeyValue(sb, t.ProjectInfo.GetProjectGuidAsString(), SonarProperties.WorkingDirectory, t.Workdir));

            if (sharedFiles.Count > 0)
            {
                sb.AppendLine(@"sonar.sources=\");
                var escapedFiles = sharedFiles.Select(Escape);
                sb.AppendLine(string.Join(@",\" + Environment.NewLine, escapedFiles));
            }

            sb.AppendLine();

        }

        #endregion

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

        #endregion
    }
}
