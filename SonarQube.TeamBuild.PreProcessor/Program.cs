/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
 
using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Entry point for the executable. All work is delegated to the pre-processor class.
    /// </summary>
    public static class Program // was internal
    {
        private const int SuccessCode = 0;
        private const int ErrorCode = 1;

        private static int Main(string[] args)
        {
            ILogger logger = new ConsoleLogger(includeTimestamp: false);
            Utilities.LogAssemblyVersion(logger, typeof(Program).Assembly, Resources.AssemblyDescription);
            logger.IncludeTimestamp = true;

            IPreprocessorObjectFactory factory = new PreprocessorObjectFactory();
            TeamBuildPreProcessor preProcessor = new TeamBuildPreProcessor(factory, logger);
            bool success = preProcessor.Execute(args);

            return success ? SuccessCode : ErrorCode;
        }
    }
}