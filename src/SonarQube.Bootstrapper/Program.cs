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
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var logger = new ConsoleLogger();

            IBootstrapperSettings settings = new BootstrapperSettings(logger);

            int exitCode;

            if (args.Any())
            {
                logger.LogMessage(Resources.INFO_PreProcessing, args.Length);
                exitCode = preprocess(logger, settings, args);
            }
            else
            {
                logger.LogMessage(Resources.INFO_PostProcessing);
                exitCode = postprocess(logger, settings);
            }

            return exitCode;
        }

        private static int preprocess(ILogger logger, IBootstrapperSettings settings, string[] args)
        {
            string downloadBinPath = settings.DownloadDirectory;

            logger.LogMessage(Resources.INFO_CreatingBinDir, downloadBinPath);
            if (Directory.Exists(downloadBinPath))
            {
                Directory.Delete(downloadBinPath, true);
            }
            Directory.CreateDirectory(downloadBinPath);

            string server = settings.SonarQubeUrl;
            Debug.Assert(!string.IsNullOrWhiteSpace(server), "Not expecting the server url to be null/empty");
            logger.LogMessage(Resources.INFO_ServerUrl, server);

            IBuildAgentUpdater updater = new BuildAgentUpdater();
            if (!updater.TryUpdate(server, downloadBinPath, logger))
            {
                logger.LogError(Resources.ERROR_CouldNotFindIntegrationZip);
                return 1;
            }

            var preprocessorFilePath = settings.PreProcessorFilePath;
            var processRunner = new ProcessRunner();
            processRunner.Execute(preprocessorFilePath, string.Join(" ", args.Select(a => "\"" + a + "\"")), downloadBinPath, settings.PreProcessorTimeoutInMs, logger);
            
            return processRunner.ExitCode;
        }

        private static int postprocess(ILogger logger, IBootstrapperSettings settings)
        {
            var postprocessorFilePath = settings.PostProcessorFilePath;

            var processRunner = new ProcessRunner();
            processRunner.Execute(postprocessorFilePath, "", settings.DownloadDirectory, settings.PostProcessorTimeoutInMs, logger);
            return processRunner.ExitCode;
        }

    }
}
