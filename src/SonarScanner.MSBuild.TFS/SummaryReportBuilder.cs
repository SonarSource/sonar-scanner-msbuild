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
using System.Diagnostics;
using SonarScanner.MSBuild.Shim;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.Common.TFS;
using System.Linq;

namespace SonarScanner.MSBuild.TFS;

/// <summary>
/// Generates summary reports for various build systems
/// </summary>
public class SummaryReportBuilder : ISummaryReportBuilder
{
    public class SummaryReportData
    {
        public int ProductProjects { get; set; }
        public int TestProjects { get; set; }
        public int InvalidProjects { get; set; }
        public int SkippedProjects { get; set; }
        public int ExcludedProjects { get; set; }
        public bool Succeeded { get; set; }
        public string DashboardUrl { get; set; } // should be Uri https://github.com/SonarSource/sonar-scanner-msbuild/issues/1252
        public string ProjectDescription { get; set; }
    }

    public /* for test purposes */ const string DashboardUrlFormat = "{0}/dashboard/index/{1}";
    public /* for test purposes */ const string DashboardUrlFormatWithBranch = "{0}/dashboard/index/{1}:{2}";

    private readonly ILegacyTeamBuildFactory legacyTeamBuildFactory;
    private readonly ILogger logger;

    private AnalysisConfig config;
    private ProjectInfoAnalysisResult result;
    private IBuildSettings settings;


    public SummaryReportBuilder(ILegacyTeamBuildFactory legacyTeamBuildFactory, ILogger logger)
    {
        this.legacyTeamBuildFactory
            = legacyTeamBuildFactory ?? throw new ArgumentNullException(nameof(legacyTeamBuildFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region IReportBuilder interface methods

    /// <summary>
    /// Generates summary reports for LegacyTeamBuild and for Build Vnext
    /// </summary>
    public void GenerateReports(IBuildSettings settings, AnalysisConfig config, bool ranToCompletion, string fullPropertiesFilePath, ILogger logger)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.config = config ?? throw new ArgumentNullException(nameof(config));

        result = new ProjectInfoAnalysisResult();
        result.RanToCompletion = ranToCompletion;
        result.FullPropertiesFilePath = fullPropertiesFilePath;

        new PropertiesFileGenerator(config, logger).TryWriteProperties(new PropertiesWriter(config, logger), out var allProjects);

        result.Projects.AddRange(allProjects);

        GenerateReports(logger);
    }

    #endregion IReportBuilder interface methods

    private void GenerateReports(ILogger logger)
    {
        var summaryData = CreateSummaryData(config, result, logger);

        if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild && !BuildSettings.SkipLegacyCodeCoverageProcessing)
        {
            UpdateLegacyTeamBuildSummary(summaryData);
        }
    }

    public /* for test purposes */ static SummaryReportData CreateSummaryData(
        AnalysisConfig config,
        ProjectInfoAnalysisResult result,
        ILogger logger)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var validProjects = result.GetProjectsByStatus(ProjectInfoValidity.Valid);

        var summaryData = new SummaryReportData
        {
            SkippedProjects = result.Projects.Count(p => p.Status == ProjectInfoValidity.NoFilesToAnalyze),
            InvalidProjects = result.Projects.Count(p => p.Status == ProjectInfoValidity.InvalidGuid || p.Status == ProjectInfoValidity.DuplicateGuid),
            ExcludedProjects = result.Projects.Count(p => p.Status == ProjectInfoValidity.ExcludeFlagSet),
            ProductProjects = validProjects.Count(p => p.ProjectType == ProjectType.Product),
            TestProjects = validProjects.Count(p => p.ProjectType == ProjectType.Test),
            Succeeded = result.RanToCompletion,
            DashboardUrl = GetSonarDashboadUrl(config, logger),
            ProjectDescription = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                Resources.Report_SonarQubeProjectDescription, config.SonarProjectName,
                config.SonarProjectKey, config.SonarProjectVersion)
        };
        return summaryData;
    }

    private static string GetSonarDashboadUrl(AnalysisConfig config, ILogger logger)
    {
        var hostUrl = config.SonarQubeHostUrl.TrimEnd('/');
        var branch = FindBranch(config, logger);

        string sonarUrl;

        if (string.IsNullOrWhiteSpace(branch))
        {
            sonarUrl = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                DashboardUrlFormat,
                hostUrl,
                config.SonarProjectKey);
        }
        else
        {
            sonarUrl = string.Format(
               System.Globalization.CultureInfo.InvariantCulture,
               DashboardUrlFormatWithBranch,
               hostUrl,
               config.SonarProjectKey,
               branch);
        }

        return sonarUrl;
    }

    private static string FindBranch(AnalysisConfig config, ILogger logger)
    {
        var localSettings = config.GetAnalysisSettings(includeServerSettings: false, logger);
        Debug.Assert(localSettings != null);

        localSettings.TryGetValue(SonarProperties.ProjectBranch, out string branch);

        return branch;
    }

    private void UpdateLegacyTeamBuildSummary(SummaryReportData summaryData)
    {
        logger.LogInfo(Resources.Report_UpdatingTeamBuildSummary);

        using (var summaryLogger = legacyTeamBuildFactory.BuildLegacyBuildSummaryLogger(config.GetTfsUri(), config.GetBuildUri()))
        {
            summaryLogger.WriteMessage(Resources.WARN_XamlBuildDeprecated);

            // Add a link to SonarQube dashboard if analysis succeeded
            if (summaryData.Succeeded)
            {
                summaryLogger.WriteMessage(Resources.Report_AnalysisSucceeded, summaryData.ProjectDescription, summaryData.DashboardUrl);
            }
            else
            {
                summaryLogger.WriteMessage(Resources.Report_AnalysisFailed, summaryData.ProjectDescription);
            }

            summaryLogger.WriteMessage(Resources.Report_ProductAndTestMessage, summaryData.ProductProjects, summaryData.TestProjects);
            summaryLogger.WriteMessage(Resources.Report_InvalidSkippedAndExcludedMessage, summaryData.InvalidProjects, summaryData.SkippedProjects, summaryData.ExcludedProjects);
        }
    }
}
