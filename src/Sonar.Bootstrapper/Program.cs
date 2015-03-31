using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System.IO;
using System.IO.Compression;
using System.Net;

namespace Sonar.Bootstrapper
{
    class Program
    {
        private const int preprocessorTimeoutInMs = 1000 * 60 * 5;
        private const int postprocessorTimeoutInMs = 1000 * 60 * 60;

        private const string SonarIntegrationFilename = "Sonar.TeamBuild.Integration.zip";
        private const string IntegrationUrlFormat = "{0}/static/csharp/" + SonarIntegrationFilename;

        private const string PostprocessingMarkerFilename = "PostprocessingMarker.txt";
        private const string PreprocessorFilename = "Sonar.TeamBuild.PreProcessor.exe";
        private const string PostprocessorFilename = "Sonar.TeamBuild.PostProcessor.exe";

        static void Main(string[] args)
        {
            var logger = new ConsoleLogger();

            var teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            var sonarBinPath = Path.Combine(teamBuildSettings.BuildDirectory, "SonarBin");
            var postprocessingMarkerPath = Path.Combine(sonarBinPath, PostprocessingMarkerFilename);

            logger.LogMessage("Locating the file: {0}", postprocessingMarkerPath);
            if (!File.Exists(postprocessingMarkerPath))
            {
                logger.LogMessage("Not found -> Preprocessing");
                preprocess(logger, sonarBinPath, args);
                File.Create(postprocessingMarkerPath);
            }
            else
            {
                logger.LogMessage("Found -> Postprocessing");
                postprocess(logger, sonarBinPath);
                File.Delete(postprocessingMarkerPath);
            }
        }

        static void preprocess(ILogger logger, string sonarBinPath, string[] args)
        {
            if (Directory.Exists(sonarBinPath))
            {
                Directory.Delete(sonarBinPath, true);
            }
            Directory.CreateDirectory(sonarBinPath);

            var sonarRunnerProperties = FileLocator.FindDefaultSonarRunnerProperties();
            if (sonarRunnerProperties == null)
            {
                throw new ArgumentException("Could not find the sonar-project.properties from the sonar-runner in %PATH%.");
            }

            var propertiesProvider = new FilePropertiesProvider(sonarRunnerProperties);

            var server = propertiesProvider.GetProperty(SonarProperties.HostUrl);
            if (server.EndsWith("/"))
            {
                server = server.Substring(0, server.Length - 1);
            }

            var integrationUrl = string.Format(IntegrationUrlFormat, server);

            var sonarIntegrationFilePath = Path.Combine(sonarBinPath, SonarIntegrationFilename);
            var preprocessorFilePath = Path.Combine(sonarBinPath, PreprocessorFilename);

            using (WebClient client = new WebClient())
            {
                logger.LogMessage("Downloading {0} from {1} to {2}", SonarIntegrationFilename, integrationUrl, sonarIntegrationFilePath);
                client.DownloadFile(integrationUrl, sonarIntegrationFilePath);
                ZipFile.ExtractToDirectory(sonarIntegrationFilePath, sonarBinPath);

                var processRunner = new ProcessRunner();
                processRunner.Execute(preprocessorFilePath, string.Join(" ", args.Select(a => "\"" + a + "\"")), sonarBinPath, preprocessorTimeoutInMs, logger);
            }
        }

        static void postprocess(ILogger logger, string sonarBinPath)
        {
            var postprocessorFilePath = Path.Combine(sonarBinPath, PostprocessorFilename);

            var processRunner = new ProcessRunner();
            processRunner.Execute(postprocessorFilePath, "", sonarBinPath, postprocessorTimeoutInMs, logger);
        }
    }
}
