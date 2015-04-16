//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SonarQube.Bootstrapper
{
    static class Program
    {
        static void Main(string[] args)
        {
            var logger = new ConsoleLogger();

            IBootstrapperSettings settings = new BootstrapperSettings(logger);
            var downloadBinPath = settings.DownloadDirectory;

            if (args.Any())
            {
                logger.LogMessage(Resources.INFO_PreProcessing, args.Length);
                preprocess(logger, settings, args);
            }
            else
            {
                logger.LogMessage(Resources.INFO_PostProcessing);
                postprocess(logger, settings);
            }
        }

        static void preprocess(ILogger logger, IBootstrapperSettings settings, string[] args)
        {
            string downloadBinPath = settings.DownloadDirectory;

            logger.LogMessage("Creating the analysis bin directory: {0}", downloadBinPath);
            if (Directory.Exists(downloadBinPath))
            {
                Directory.Delete(downloadBinPath, true);
            }
            Directory.CreateDirectory(downloadBinPath);

            string server = settings.SonarQubeUrl;
            Debug.Assert(!string.IsNullOrWhiteSpace(server), "Not expecting the server url to be null/empty");
            logger.LogMessage("SonarQube server url: {0}", server);

            IBuildAgentUpdater updater = new BuildAgentUpdater();
            updater.Update(server, downloadBinPath, logger);

            var preprocessorFilePath = settings.PreProcessorFilePath;
            var processRunner = new ProcessRunner();
            processRunner.Execute(preprocessorFilePath, string.Join(" ", args.Select(a => "\"" + a + "\"")), downloadBinPath, settings.PreProcessorTimeoutInMs, logger);
            
        }

        static void postprocess(ILogger logger, IBootstrapperSettings settings)
        {
            var postprocessorFilePath = settings.PostProcessorFilePath;

            var processRunner = new ProcessRunner();
            processRunner.Execute(postprocessorFilePath, "", settings.DownloadDirectory, settings.PostProcessorTimeoutInMs, logger);
        }

    }
}
