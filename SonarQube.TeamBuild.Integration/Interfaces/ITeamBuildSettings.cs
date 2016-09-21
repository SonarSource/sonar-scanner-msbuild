using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SonarQube.TeamBuild.Integration.Interfaces
{
    public interface ITeamBuildSettings
    {
        BuildEnvironment BuildEnvironment { get; }
        string TfsUri { get; }
        string BuildUri { get; }
        string SourcesDirectory { get; }
        string AnalysisBaseDirectory { get; }
        string BuildDirectory { get; }
        string SonarConfigDirectory { get; }
        string SonarOutputDirectory { get; }
        string SonarBinDirectory { get; }
        string AnalysisConfigFilePath { get; }
    }
}
