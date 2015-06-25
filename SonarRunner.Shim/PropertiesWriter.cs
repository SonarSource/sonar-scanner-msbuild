//-----------------------------------------------------------------------
// <copyright file="PropertiesWriter.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SonarRunner.Shim
{
    public class PropertiesWriter
    {
        private StringBuilder sb;

        private AnalysisConfig config;

        /// <summary>
        /// List of projects that for which settings have been written
        /// </summary>
        private IList<ProjectInfo> projects;

        #region Public methods

        public static string Escape(string value)
        {
            if (value == null)
            {
                return null;
            }
            
            StringBuilder sb = new StringBuilder();

            foreach (char c in value)
            {
                if (c == '\\')
                {
                    sb.Append("\\\\");
                }
                else if (IsAscii(c) && !char.IsControl(c))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append("\\u");
                    sb.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                }
            }

            return sb.ToString();
        }

        public PropertiesWriter(AnalysisConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            this.config = config;
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

            WriteSonarProjectInfo();

            AppendKeyValue(sb, "sonar.modules", string.Join(",", this.projects.Select(p => p.GetProjectGuidAsString())));
            sb.AppendLine();

            WriteSonarRunnerProperties();

            return sb.ToString();
        }

        public void WriteSettingsForProject(ProjectInfo project, IEnumerable<string> files, string fxCopReportFilePath, string codeCoverageFilePath)
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
            Debug.Assert(files.All(f => File.Exists(f)), "Expecting all of the specified files to exiest");

            this.projects.Add(project);

            string guid = project.GetProjectGuidAsString();

            AppendKeyValue(sb, guid, "sonar.projectKey", this.config.SonarProjectKey + ":" + guid);
            AppendKeyValue(sb, guid, "sonar.projectName", project.ProjectName);
            AppendKeyValue(sb, guid, "sonar.projectBaseDir", project.GetProjectDirectory());

            if (fxCopReportFilePath != null)
            {
                AppendKeyValue(sb, guid, "sonar.cs.fxcop.reportPath", fxCopReportFilePath);
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

            IEnumerable<string> escapedFiles = files.Select(f => Escape(f));
            sb.AppendLine(string.Join(@",\" + Environment.NewLine, escapedFiles));

            sb.AppendLine();

            if (project.AnalysisSettings != null && project.AnalysisSettings.Any())
            {
                foreach(AnalysisSetting setting in project.AnalysisSettings)
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
        public void WriteGlobalSettings(IEnumerable<AnalysisSetting> settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            foreach(AnalysisSetting setting in settings)
            {
                AppendKeyValue(this.sb, setting.Id, setting.Value);
            }
            sb.AppendLine();
        }

        #endregion

        #region Private methods

        // Should be dropped with SONARMSBRU-84
        private void WriteSonarRunnerProperties()
        {
            if (config.SonarRunnerPropertiesPath != null)
            {
                var sonarRunnerProperties = new FilePropertiesProvider(config.SonarRunnerPropertiesPath);
                foreach (var property in sonarRunnerProperties.GetProperties())
                {
                    AppendKeyValue(sb, property.Key, property.Value);
                }
                sb.AppendLine();
            }
        }

        private static void AppendKeyValue(StringBuilder sb, string keyPrefix, string keySuffix, string value)
        {
            AppendKeyValue(sb, keyPrefix + "." + keySuffix, value);
        }

        private static void AppendKeyValue(StringBuilder sb, string key, string value)
        {
            sb.Append(key);
            sb.Append('=');
            sb.AppendLine(Escape(value));
        }

        private static bool IsAscii(char c)
        {
            return c <= sbyte.MaxValue;
        }

        private void WriteSonarProjectInfo()
        {
            AppendKeyValue(sb, SonarProperties.ProjectKey, this.config.SonarProjectKey);
            AppendKeyValue(sb, SonarProperties.ProjectName, this.config.SonarProjectName);
            AppendKeyValue(sb, SonarProperties.ProjectVersion, this.config.SonarProjectVersion);
            AppendKeyValue(sb, SonarProperties.ProjectBaseDir, ComputeProjectBaseDir(projects, this.config.SonarOutputDir));
            AppendKeyValue(sb, SonarProperties.WorkingDirectory, Path.Combine(this.config.SonarOutputDir, ".sonar"));

            sb.AppendLine();

            sb.AppendLine("# FIXME: Encoding is hardcoded");
            AppendKeyValue(sb, SonarProperties.SourceEncoding, "UTF-8");
            sb.AppendLine();
        }

        private static string ComputeProjectBaseDir(IEnumerable<ProjectInfo> projects, string defaultValue)
        {
            var projectDirs = projects.Select(p => p.GetProjectDirectory());

            var pathPartEnumerators = projectDirs.Select(s => s.Split(Path.DirectorySeparatorChar).AsEnumerable().GetEnumerator()).ToArray();
            try
            {
                var commonParts = new List<string>();
                if (pathPartEnumerators.Length > 0)
                {
                    while (pathPartEnumerators.All(e => e.MoveNext()) && pathPartEnumerators.All(e => e.Current == pathPartEnumerators.First().Current))
                    {
                        commonParts.Add(pathPartEnumerators.First().Current);
                    }
                }

                if (!commonParts.Any())
                {
                    return defaultValue;
                }

                return string.Join(Path.DirectorySeparatorChar.ToString(), commonParts);
            }
            finally
            {
                Array.ForEach(pathPartEnumerators, e => e.Dispose());
            }
        }

        #endregion
    }
}
