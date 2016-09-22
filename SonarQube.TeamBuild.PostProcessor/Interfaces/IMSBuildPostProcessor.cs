using SonarQube.Common;
using SonarQube.TeamBuild.Integration.Interfaces;

namespace SonarQube.TeamBuild.PostProcessor.Interfaces
{
    public interface IMSBuildPostProcessor
    {
        bool Execute(string[] args, AnalysisConfig config, ITeamBuildSettings settings);
    }
}
