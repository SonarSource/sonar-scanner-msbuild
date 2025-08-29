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

using System.Globalization;
using SonarScanner.MSBuild.Common.TFS;
using SonarScanner.MSBuild.Shim;

namespace SonarScanner.MSBuild.TFS;

/// <summary>
/// Generates summary reports for various build systems.
/// </summary>
public class SummaryReportBuilder
{
    private const string DashboardUrlFormat = "{0}/dashboard/index/{1}";
    private const string DashboardUrlFormatWithBranch = "{0}/dashboard/index/{1}:{2}";

    private readonly ILegacyTeamBuildFactory legacyTeamBuildFactory;
    private readonly AnalysisConfig config;
    private readonly IRuntime runtime;

    public SummaryReportBuilder(ILegacyTeamBuildFactory legacyTeamBuildFactory, AnalysisConfig config, IRuntime runtime)
    {
        this.legacyTeamBuildFactory = legacyTeamBuildFactory ?? throw new ArgumentNullException(nameof(legacyTeamBuildFactory));
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    /// <summary>
    /// Generates summary reports for LegacyTeamBuild and for Build VNext.
    /// </summary>
    public virtual void GenerateReports(IBuildSettings settings, bool ranToCompletion, string fullPropertiesFilePath)
    {
        _ = settings ?? throw new ArgumentNullException(nameof(settings));
        var allProjects = ProjectLoader.LoadFrom(config.SonarOutputDir).ToProjectData(runtime.OperatingSystem.IsWindows(), runtime.Logger);
        if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild && !BuildSettings.SkipLegacyCodeCoverageProcessing)
        {
            UpdateLegacyTeamBuildSummary(new SummaryReportData(config, allProjects, ranToCompletion, runtime.Logger));
        }
    }

    private void UpdateLegacyTeamBuildSummary(SummaryReportData summary)
    {
        runtime.Logger.LogInfo(Resources.Report_UpdatingTeamBuildSummary);
        using var summaryLogger = legacyTeamBuildFactory.BuildLegacyBuildSummaryLogger(config.GetTfsUri(), config.GetBuildUri());
        summaryLogger.WriteMessage(Resources.WARN_XamlBuildDeprecated);
        // Add a link to SonarQube dashboard if analysis succeeded
        if (summary.Succeeded)
        {
            summaryLogger.WriteMessage(Resources.Report_AnalysisSucceeded, summary.ProjectDescription, summary.DashboardUrl);
        }
        else
        {
            summaryLogger.WriteMessage(Resources.Report_AnalysisFailed, summary.ProjectDescription);
        }
        summaryLogger.WriteMessage(Resources.Report_ProductAndTestMessage, summary.ProductProjects, summary.TestProjects);
        summaryLogger.WriteMessage(Resources.Report_InvalidSkippedAndExcludedMessage, summary.InvalidProjects, summary.SkippedProjects, summary.ExcludedProjects);
    }

    public class SummaryReportData
    {
        public int ProductProjects { get; }
        public int TestProjects { get; }
        public int InvalidProjects { get; }
        public int SkippedProjects { get; }
        public int ExcludedProjects { get; }
        public bool Succeeded { get; }
        public Uri DashboardUrl { get; }
        public string ProjectDescription { get; }

        public SummaryReportData(AnalysisConfig config, ProjectData[] allProjects, bool ranToCompletion, ILogger logger)
        {
            _ = config ?? throw new ArgumentNullException(nameof(config));
            SkippedProjects = allProjects.Count(x => x.Status == ProjectInfoValidity.NoFilesToAnalyze);
            InvalidProjects = allProjects.Count(x => x.Status == ProjectInfoValidity.InvalidGuid || x.Status == ProjectInfoValidity.DuplicateGuid);
            ExcludedProjects = allProjects.Count(x => x.Status == ProjectInfoValidity.ExcludeFlagSet);
            ProductProjects = allProjects.Count(x => x.Status == ProjectInfoValidity.Valid && x.Project.ProjectType == ProjectType.Product);
            TestProjects = allProjects.Count(x => x.Status == ProjectInfoValidity.Valid && x.Project.ProjectType == ProjectType.Test);
            Succeeded = ranToCompletion;
            DashboardUrl = SonarDashboadUrl(config, logger);
            ProjectDescription = string.Format(CultureInfo.CurrentCulture, Resources.Report_SonarQubeProjectDescription, config.SonarProjectName, config.SonarProjectKey, config.SonarProjectVersion);
        }

        private static Uri SonarDashboadUrl(AnalysisConfig config, ILogger logger)
        {
            var hostUrl = config.SonarQubeHostUrl.TrimEnd('/');
            var branch = FindBranch(config, logger);
            return string.IsNullOrWhiteSpace(branch)
                ? new(string.Format(CultureInfo.InvariantCulture, DashboardUrlFormat, hostUrl, config.SonarProjectKey))
                : new(string.Format(CultureInfo.InvariantCulture, DashboardUrlFormatWithBranch, hostUrl, config.SonarProjectKey, branch));
        }

        private static string FindBranch(AnalysisConfig config, ILogger logger)
        {
            var localSettings = config.AnalysisSettings(includeServerSettings: false, logger);
            localSettings.TryGetValue(SonarProperties.ProjectBranch, out string branch);
            return branch;
        }
    }
}
