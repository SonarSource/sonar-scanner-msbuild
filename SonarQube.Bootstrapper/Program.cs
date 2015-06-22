//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
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
                exitCode = PreProcess(logger, settings, args);
            }
            else
            {
                logger.LogMessage(Resources.INFO_PostProcessing);
                exitCode = PostProcess(logger, settings);
            }

            return exitCode;
        }

        private static int PreProcess(ILogger logger, IBootstrapperSettings settings, string[] args)
        {
            string downloadBinPath = settings.DownloadDirectory;

            Utilities.EnsureEmptyDirectory(settings.TempDirectory, logger);
            Utilities.EnsureEmptyDirectory(downloadBinPath, logger);

            string server = settings.SonarQubeUrl;
            Debug.Assert(!string.IsNullOrWhiteSpace(server), "Not expecting the server url to be null/empty");
            logger.LogMessage(Resources.INFO_ServerUrl, server);

            BuildAgentUpdater updater = new BuildAgentUpdater(logger);
            if (!updater.TryUpdate(server, downloadBinPath))
            {
                logger.LogError(Resources.ERROR_CouldNotFindIntegrationZip);
                return 1;
            }

            if (!BuildAgentUpdater.CheckBootstrapperVersion(settings.SupportedBootstrapperVersionsFilePath, settings.BootstrapperVersion))
            {
                logger.LogError(Resources.ERROR_VersionMismatch);
                return 1;
            }

            var preprocessorFilePath = settings.PreProcessorFilePath;
            var processRunner = new ProcessRunner();
            processRunner.Execute(preprocessorFilePath, string.Join(" ", args.Select(a => "\"" + a + "\"")), settings.TempDirectory, logger);

            return processRunner.ExitCode;
        }

        private static int PostProcess(ILogger logger, IBootstrapperSettings settings)
        {
            var postprocessorFilePath = settings.PostProcessorFilePath;

            var processRunner = new ProcessRunner();
            processRunner.Execute(postprocessorFilePath, "", settings.TempDirectory, logger);
            return processRunner.ExitCode;
        }


    }
}
