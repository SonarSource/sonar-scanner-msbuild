//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;

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

            if (args.Length != 4)
            {
                logger.LogError(Resources.ERROR_InvalidCommandLineArgs);
                return ErrorCode;
            }

            string projectKey = args[0];
            string projectName = args[1];
            string projectVersion = args[2];
            string propertiesPath = args[3];

            TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor();
            bool success = preProcessor.Execute(logger, projectKey, projectName, projectVersion, propertiesPath);

            return success ? 0 : ErrorCode;
        }

    
    }
}
