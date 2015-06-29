//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System.Diagnostics;
using System.Linq;

namespace SonarQube.Bootstrapper
{
    public static class Program
    {
        public const int ErrorCode = 1;

        private static int Main(string[] args)
        {
            var logger = new ConsoleLogger();
            BuildAgentUpdater updater = new BuildAgentUpdater();

            return Execute(args, updater, logger);
        }

        public static int Execute(string[] args, IBuildAgentUpdater updater, ILogger logger)
        {
            int exitCode;

            IBootstrapperSettings settings;
            if (ArgumentProcessor.TryProcessArgs(args, logger, out settings))
            {
                Debug.Assert(settings.Phase != AnalysisPhase.Unspecified, "Expecting the processing phase to be specified");
                if (settings.Phase == AnalysisPhase.PreProcessing)
                {
                    logger.LogMessage(Resources.INFO_PreProcessing, args.Length);
                    exitCode = PreProcess(args, updater, settings, logger);
                }
                else
                {
                    logger.LogMessage(Resources.INFO_PostProcessing, args.Length);
                    exitCode = PostProcess(logger, settings);
                }
            }
            else
            {
                // The argument processor will have logged errors
                exitCode = ErrorCode;
            }
            return exitCode;
        }

        private static int PreProcess(string[] args, IBuildAgentUpdater updater, IBootstrapperSettings settings, ILogger logger)
        {
            string downloadBinPath = settings.DownloadDirectory;

            Utilities.EnsureEmptyDirectory(settings.TempDirectory, logger);
            Utilities.EnsureEmptyDirectory(downloadBinPath, logger);

            string server = settings.SonarQubeUrl;
            Debug.Assert(!string.IsNullOrWhiteSpace(server), "Not expecting the server url to be null/empty");
            logger.LogMessage(Resources.INFO_ServerUrl, server);

            if (!updater.TryUpdate(server, downloadBinPath, logger))
            {
                logger.LogError(Resources.ERROR_CouldNotFindIntegrationZip);
                return 1;
            }

            if (!updater.CheckBootstrapperApiVersion(settings.SupportedBootstrapperVersionsFilePath, settings.BootstrapperVersion))
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
