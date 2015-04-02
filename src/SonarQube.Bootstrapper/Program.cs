//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;

namespace SonarQube.Bootstrapper
{
    class Program
    {
        private const int preprocessorTimeoutInMs = 1000 * 60 * 5;
        private const int postprocessorTimeoutInMs = 1000 * 60 * 60;

        private const string SonarQubeIntegrationFilename = "SonarQube.TeamBuild.Integration.zip";
        private const string IntegrationUrlFormat = "{0}/static/csharp/" + SonarQubeIntegrationFilename;

        private const string PreprocessorFilename = "SonarQube.TeamBuild.PreProcessor.exe";
        private const string PostprocessorFilename = "SonarQube.TeamBuild.PostProcessor.exe";

        static void Main(string[] args)
        {
            var logger = new ConsoleLogger();

            var teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            var downloadBinPath = Path.Combine(teamBuildSettings.BuildDirectory, @"SQTemp\Bin");

            if (args.Any())
            {
                logger.LogMessage("Preprocessing ({0} arguments passed)", args.Length);
                preprocess(logger, downloadBinPath, args);
            }
            else
            {
                logger.LogMessage("Postprocessing (no arguments passed)");
                postprocess(logger, downloadBinPath);
            }
        }

        static void preprocess(ILogger logger, string downloadBinPath, string[] args)
        {
            if (Directory.Exists(downloadBinPath))
            {
                Directory.Delete(downloadBinPath, true);
            }
            Directory.CreateDirectory(downloadBinPath);

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

            var downloadedZipFilePath = Path.Combine(downloadBinPath, SonarQubeIntegrationFilename);
            var preprocessorFilePath = Path.Combine(downloadBinPath, PreprocessorFilename);

            using (WebClient client = new WebClient())
            {
                logger.LogMessage("Downloading {0} from {1} to {2}", SonarQubeIntegrationFilename, integrationUrl, downloadedZipFilePath);
                client.DownloadFile(integrationUrl, downloadedZipFilePath);
                ZipFile.ExtractToDirectory(downloadedZipFilePath, downloadBinPath);

                var processRunner = new ProcessRunner();
                processRunner.Execute(preprocessorFilePath, string.Join(" ", args.Select(a => "\"" + a + "\"")), downloadBinPath, preprocessorTimeoutInMs, logger);
            }
        }

        static void postprocess(ILogger logger, string downloadBinPath)
        {
            var postprocessorFilePath = Path.Combine(downloadBinPath, PostprocessorFilename);

            var processRunner = new ProcessRunner();
            processRunner.Execute(postprocessorFilePath, "", downloadBinPath, postprocessorTimeoutInMs, logger);
        }
    }
}
