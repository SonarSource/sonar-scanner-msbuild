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
                else if (IsAscii(c))
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

            this.WriteSonarProjectInfo();
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
        }

        #endregion

        #region Private methods

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
            AppendKeyValue(sb, "sonar.projectKey", this.config.SonarProjectKey);
            AppendKeyValue(sb, "sonar.projectName", this.config.SonarProjectName);
            AppendKeyValue(sb, "sonar.projectVersion", this.config.SonarProjectVersion);
            AppendKeyValue(sb, "sonar.projectBaseDir", this.config.SonarOutputDir);
            sb.AppendLine();

            sb.AppendLine("# FIXME: Encoding is hardcoded");
            AppendKeyValue(sb, "sonar.sourceEncoding", "UTF-8");
            sb.AppendLine();
        }

        #endregion
    }
}
