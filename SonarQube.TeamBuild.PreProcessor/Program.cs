//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.IO;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Wrapper around the pre-processor class. This class is responsible for
    /// checking the command line arguments and returning the appropriate exit
    /// code. The rest of the work is done by the pre-processor class.
    /// </summary>
    public class Program // was internal
    {
        private const int ErrorCode = 1;

        private static int Main(string[] args)
        {
            ILogger logger = new ConsoleLogger(includeTimestamp: true);

            if (IsInvokedByBootstrapper0_9())
            {
                logger.LogError(Resources.ERROR_InvokedFromBootstrapper0_9);
                return ErrorCode;
            }

            bool success;

            ProcessedArgs processedArgs = ArgumentProcessor.TryProcessArgs(args, logger);

            if (processedArgs == null)
            {
                success = false;
                logger.LogError(Resources.ERROR_InvalidCommandLineArgs);
            }
            else
            {
                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor();
                success = preProcessor.Execute(processedArgs, logger);
            }

            return success ? 0 : ErrorCode;
        }

        /// <summary>
        ///
        /// </summary>
        /// <remarks>The bootstrapper 0.9 downloads the preprocessor to "sqtemp" and the 1.0 to ".sonarqube"</remarks>
        private static bool IsInvokedByBootstrapper0_9()
        {
            return !String.Equals(
                FileConstants.RelativePathToTempDir,
                Path.GetFileName(Directory.GetCurrentDirectory()),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}