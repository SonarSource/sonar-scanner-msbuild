using Microsoft.VisualStudio.Setup.Configuration;

namespace SonarQube.TeamBuild.Integration
{
    public interface IVisualStudioSetupConfigurationFactory
    {
        /// <summary>
        /// Attempts to instantiate a queryable setup configuration object.
        /// </summary>
        /// <returns></returns>
        ISetupConfiguration GetSetupConfigurationQuery();
    }
}