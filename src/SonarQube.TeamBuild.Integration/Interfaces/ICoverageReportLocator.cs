using SonarQube.Common;

namespace SonarQube.TeamBuild.Integration
{
    public interface ICoverageReportLocator
    {
        bool TryGetBinaryCoveragePath(AnalysisConfig config, ITeamBuildSettings settings, out string binaryCoveragePath);
        bool TryGetTestResultsPath(AnalysisConfig config, ITeamBuildSettings settings, out string testResultsPath);
    }
}
