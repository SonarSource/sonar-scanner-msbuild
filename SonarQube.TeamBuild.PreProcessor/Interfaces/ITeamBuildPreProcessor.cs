namespace SonarQube.TeamBuild.PreProcessor
{
    public interface ITeamBuildPreProcessor
    {
        bool Execute(string[] args);
    }
}