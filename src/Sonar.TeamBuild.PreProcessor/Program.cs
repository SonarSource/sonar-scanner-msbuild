//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using System.Diagnostics;
using System.IO;

namespace Sonar.TeamBuild.PreProcessor
{
    /// <summary>
    /// Wrapper around the pre-processor class. This class is responsible for
    /// checking the command line arguments and returning the appropriate exit
    /// code. The rest of the work is done by the pre-processor class.
    /// </summary>
    internal class Program
    {
        private const int ErrorCode = 1;

        static int Main(string[] args)
        {
            ILogger logger = new ConsoleLogger(includeTimestamp: true);

            bool success;

            ProcessedArgs processedArgs = ArgumentProcessor.TryProcessArgs(args, logger);

            if (processedArgs == null)
            {
                success = false;
                logger.LogError(Resources.ERROR_InvalidCommandLineArgs);
            }
            else
            {
                Debug.Assert(File.Exists(processedArgs.RunnerPropertiesPath), "Expecting the properties file to exist: " + processedArgs.RunnerPropertiesPath);
                TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor();
                success = preProcessor.Execute(logger, processedArgs.ProjectKey, processedArgs.ProjectName, processedArgs.ProjectVersion, processedArgs.RunnerPropertiesPath);
            }

            return success ? 0 : ErrorCode;
        }

    }
}
