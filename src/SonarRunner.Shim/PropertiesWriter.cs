//-----------------------------------------------------------------------
// <copyright file="PropertiesWriter.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SonarRunner.Shim
{
    public static class PropertiesWriter
    {
        #region Public methods

        public static string ToString(ILogger logger, AnalysisConfig config, List<ProjectInfo> projects)
        {
            var uniqueProjects = projects.GroupBy(p => p.ProjectGuid).Where(g => g.Count() == 1).Select(g => g.First());
            foreach (var duplicatedProject in projects.Where(p => !uniqueProjects.Any(p2 => p.ProjectGuid.Equals(p2.ProjectGuid))))
            {
                logger.LogWarning(Resources.WARN_DuplicateProjectGuid, duplicatedProject.GetProjectGuidAsString(), duplicatedProject.FullPath);
            }

            StringBuilder sb = new StringBuilder();

            AppendKeyValue(sb, "sonar.projectKey", config.SonarProjectKey);
            AppendKeyValue(sb, "sonar.projectName", config.SonarProjectName);
            AppendKeyValue(sb, "sonar.projectVersion", config.SonarProjectVersion);
            AppendKeyValue(sb, "sonar.projectBaseDir", config.SonarOutputDir);
            sb.AppendLine();

            sb.AppendLine("# FIXME: Encoding is hardcoded");
            AppendKeyValue(sb, "sonar.sourceEncoding", "UTF-8");
            sb.AppendLine();

            AppendKeyValue(sb, "sonar.modules", string.Join(",", uniqueProjects.Select(p => p.GetProjectGuidAsString())));
            sb.AppendLine();

            foreach (var project in uniqueProjects)
            {
                IList<string> files = GetFilesToAnalyze(project);
                if (files.Count == 0)
                {
                    continue; // skip projects that don't have any files
                }

                string guid = project.GetProjectGuidAsString();

                AppendKeyValue(sb, guid, "sonar.projectKey", config.SonarProjectKey + ":" + guid);
                AppendKeyValue(sb, guid, "sonar.projectName", project.ProjectName);
                AppendKeyValue(sb, guid, "sonar.projectBaseDir", project.GetProjectDir());
                string fxCopReport = project.TryGetAnalysisFileLocation(AnalysisType.FxCop);
                if (fxCopReport != null)
                {
                    AppendKeyValue(sb, guid, "sonar.cs.fxcop.reportPath", fxCopReport);
                }
                string vsCoverageReport = project.TryGetAnalysisFileLocation(AnalysisType.VisualStudioCodeCoverage);
                if (vsCoverageReport != null)
                {
                    AppendKeyValue(sb, guid, "sonar.cs.vscoveragexml.reportsPaths", vsCoverageReport);
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

                for (int i = 0; i < files.Count(); i++)
                {
                    var file = files[i];
                    sb.Append(Escape(file));
                    if (i != files.Count() - 1)
                    {
                        sb.Append(@",\");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static string Escape(string value)
        {
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

        /// <summary>
        /// Returns the files that should be analyzed. Files outside the project
        /// path should be ignored (currently the SonarQube server expects all
        /// files to be under the root project directory)
        /// </summary>
        private static IList<string> GetFilesToAnalyze(ProjectInfo projectInfo)
        {
            var result = new List<string>();
            var baseDir = projectInfo.GetProjectDir();

            foreach (string file in GetAllFiles(projectInfo))
            {
                if (IsInFolder(file, baseDir))
                {
                    result.Add(file);
                }
            }

            return result;
        }

        /// <summary>
        /// Aggregates together all of the files listed in the analysis results
        /// and returns the aggregated list
        /// </summary>
        private static IList<string> GetAllFiles(ProjectInfo projectInfo)
        {
            List<String> files = new List<string>();
            var compiledFilesPath = projectInfo.TryGetAnalysisFileLocation(AnalysisType.ManagedCompilerInputs);
            if (compiledFilesPath != null)
            {
                files.AddRange(File.ReadAllLines(compiledFilesPath));
            }
            var contentFilesPath = projectInfo.TryGetAnalysisFileLocation(AnalysisType.ContentFiles);
            if (contentFilesPath != null)
            {
                files.AddRange(File.ReadAllLines(contentFilesPath));
            }
            return files;
        }

        private static bool IsInFolder(string filePath, string folder)
        {
            // FIXME This test is not sufficient...
            return filePath.StartsWith(folder + Path.DirectorySeparatorChar);
        }

        #endregion
    }
}
