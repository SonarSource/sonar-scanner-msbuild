/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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
using System.Text;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.Shim;

/// <summary>
/// Outputs a report summarizing the project info files that were found.
/// This is not used by SonarQube: it is only for debugging purposes.
/// </summary>
public class ProjectInfoReportBuilder
{
    internal const string ReportFileName = "ProjectInfo.log";

    private readonly AnalysisConfig config;
    private readonly ProjectInfoAnalysisResult result;
    private readonly ILogger logger;

    private readonly StringBuilder sb;

    #region Public methods

    public static void WriteSummaryReport(AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
    {
        var builder = new ProjectInfoReportBuilder(config, result, logger);
        builder.Generate();
    }

    #endregion Public methods

    #region Private methods

    private ProjectInfoReportBuilder(AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.result = result ?? throw new ArgumentNullException(nameof(result));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        sb = new StringBuilder();
    }

    private void Generate()
    {
        IEnumerable<ProjectInfo> validProjects = result.ProjectsByStatus(ProjectInfoValidity.Valid);

        WriteTitle(Resources.REPORT_ProductProjectsTitle);
        WriteFileList(validProjects.Where(p => p.ProjectType == ProjectType.Product));
        WriteGroupSpacer();

        WriteTitle(Resources.REPORT_TestProjectsTitle);
        WriteFileList(validProjects.Where(p => p.ProjectType == ProjectType.Test));
        WriteGroupSpacer();

        WriteTitle(Resources.REPORT_InvalidProjectsTitle);
        WriteFilesByStatus(ProjectInfoValidity.InvalidGuid);
        WriteGroupSpacer();

        WriteTitle(Resources.REPORT_SkippedProjectsTitle);
        WriteFilesByStatus(ProjectInfoValidity.NoFilesToAnalyze);
        WriteGroupSpacer();

        WriteTitle(Resources.REPORT_ExcludedProjectsTitle);
        WriteFilesByStatus(ProjectInfoValidity.ExcludeFlagSet);
        WriteGroupSpacer();

        var reportFileName = Path.Combine(config.SonarOutputDir, ReportFileName);
        logger.LogDebug(Resources.MSG_WritingSummary, reportFileName);
        File.WriteAllText(reportFileName, sb.ToString());
    }

    private void WriteTitle(string title)
    {
        sb.AppendLine(title);
        sb.AppendLine("---------------------------------------");
    }

    private void WriteGroupSpacer()
    {
        sb.AppendLine();
        sb.AppendLine();
    }

    private void WriteFilesByStatus(params ProjectInfoValidity[] statuses)
    {
        var projects = Enumerable.Empty<ProjectInfo>();

        foreach (var status in statuses)
        {
            projects = projects.Concat(result.ProjectsByStatus(status));
        }

        if (!projects.Any())
        {
            sb.AppendLine(Resources.REPORT_NoProjectsOfType);
        }
        else
        {
            WriteFileList(projects);
        }
    }

    private void WriteFileList(IEnumerable<ProjectInfo> projects)
    {
        foreach(var project in projects)
        {
            sb.AppendLine(project.FullPath);
        }
    }

    #endregion Private methods
}
