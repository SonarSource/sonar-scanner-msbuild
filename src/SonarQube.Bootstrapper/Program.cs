/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;

namespace SonarQube.Bootstrapper
{
    public static class Program
    {
        public const int ErrorCode = 1;
        public const int SuccessCode = 0;

        public static int Main(string[] args)
        {
            var logger = new ConsoleLogger(includeTimestamp: false);
            Utilities.LogAssemblyVersion(logger, Resources.AssemblyDescription);
            return Execute(args, logger);
        }

        public static int Execute(string[] args, ILogger logger)
        {
            logger.SuspendOutput();

            if (ArgumentProcessor.IsHelp(args))
            {
                logger.LogInfo("");
                logger.LogInfo("Usage: ");
                logger.LogInfo("");
                logger.LogInfo("  {0} [begin|end] /key:project_key [/name:project_name] [/version:project_version] [/d:sonar.key=value] [/s:settings_file]", System.AppDomain.CurrentDomain.FriendlyName);
                logger.LogInfo("");
                logger.LogInfo("    When executing the begin phase, at least the project key must be defined.");
                logger.LogInfo("    Other properties can dynamically be defined with '/d:'. For example, '/d:sonar.verbose=true'.");
                logger.LogInfo("    A settings file can be used to define properties. If no settings file path is given, the file SonarQube.Analysis.xml in the installation directory will be used.");
                logger.LogInfo("    Only the token should be passed during the end phase, if it was used during the begin phase.");

                logger.ResumeOutput();
                return SuccessCode;
            }

            if (!ArgumentProcessor.TryProcessArgs(args, logger, out IBootstrapperSettings settings))
            {
                logger.ResumeOutput();
                // The argument processor will have logged errors
                Environment.ExitCode = ErrorCode;
                return ErrorCode;
            }

            var processorFactory = new DefaultProcessorFactory(logger, GetLegacyTeamBuildFactory());
            var bootstrapper = new BootstrapperClass(processorFactory, settings, logger);
            var exitCode = bootstrapper.Execute();
            Environment.ExitCode = exitCode;
            return exitCode;
        }

        private static ILegacyTeamBuildFactory GetLegacyTeamBuildFactory()
        {
#if IS_NET_FRAMEWORK
            return new TeamBuild.Integration.XamlBuild.LegacyTeamBuildFactory();
#else
            return new NotSupportedLegacyTeamBuildFactory();
#endif
        }
    }
}
