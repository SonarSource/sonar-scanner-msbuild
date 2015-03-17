//-----------------------------------------------------------------------
// <copyright file="SummaryReportBuilder.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using Sonar.TeamBuild.Integration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sonar.TeamBuild.PostProcessor
{
    /// <summary>
    /// Outputs a summary report of the post-processing activities.
    /// This is not used by SonarQube: it is only for debugging purposes.
    /// </summary>
    internal static class SummaryReportBuilder
    {
        private const string ReportFileName = "PostProcessingSummary.log";

        public static void WriteSummaryReport(AnalysisConfig context, ILogger logger)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            
            StringBuilder sb = new StringBuilder();

            List<string> testProjects = new List<string>();
            List<string> productProjects = new List<string>();

            foreach(ProjectInfo projectInfo in WalkProjects(context.SonarOutputDir))
            {
                if (projectInfo.ProjectType == ProjectType.Product)
                {
                    productProjects.Add(projectInfo.FullPath);
                }
                else
                {
                    testProjects.Add(projectInfo.FullPath);
                }
            }

            productProjects.Sort();
            testProjects.Sort();

            sb.AppendLine("Project files");
            AppendFileList(productProjects, sb);
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Test files");
            AppendFileList(testProjects, sb);

            string reportFileName = Path.Combine(context.SonarOutputDir, ReportFileName);
            logger.LogMessage("Writing post-processing summary to {0}", reportFileName);
            File.WriteAllText(reportFileName, sb.ToString());
        }

        private static IEnumerable<ProjectInfo> WalkProjects(string sonarOutputDir)
        {
            foreach (string projectFolderPath in Directory.GetDirectories(sonarOutputDir))
            {
                ProjectInfo projectInfo = null;

                string projectInfoPath = Path.Combine(projectFolderPath, FileConstants.ProjectInfoFileName);

                if (File.Exists(projectInfoPath))
                {
                    projectInfo = ProjectInfo.Load(projectInfoPath);
                    yield return projectInfo;
                }
            }
        }

        private static void AppendSeparator(StringBuilder sb)
        {
            sb.AppendLine("*************************************");
        }

        private static void AppendFileList(IEnumerable<string> fileList, StringBuilder sb)
        {
            foreach(string file in fileList)
            {
                sb.AppendLine(file);
            }
        }

    }
}
