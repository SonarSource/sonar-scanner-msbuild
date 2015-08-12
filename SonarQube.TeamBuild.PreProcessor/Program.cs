//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Entry point for the executable. All work is delegated to the pre-processor class.
    /// </summary>
    public static class Program // was internal
    {
        private const int ErrorCode = 1;

        private static int Main(string[] args)
        {
            ILogger logger = new ConsoleLogger(includeTimestamp: true);

            TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor();
            bool success = preProcessor.Execute(args, logger);
           
            return success ? 0 : ErrorCode;
        }
    }
}