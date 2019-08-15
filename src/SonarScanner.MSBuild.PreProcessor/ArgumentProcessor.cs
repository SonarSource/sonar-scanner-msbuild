/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.CommandLine;

namespace SonarScanner.MSBuild.PreProcessor
{
    /// <summary>
    /// Process and validates the pre-processor command line arguments and reports any errors
    /// </summary>
    public static class ArgumentProcessor // was internal
    {
        /// <summary>
        /// Regular expression to validate a project key.
        /// See http://docs.sonarqube.org/display/SONAR/Project+Administration#ProjectAdministration-AddingaProject
        /// </summary>
        /// <remarks>Should match the java regex here: https://github.com/SonarSource/sonarqube/blob/5.1.1/sonar-core/src/main/java/org/sonar/core/component/ComponentKeys.java#L36
        /// "Allowed characters are alphanumeric, '-', '_', '.' and ':', with at least one non-digit"
        /// </remarks>
        private static readonly Regex ProjectKeyRegEx = new Regex(@"^[a-zA-Z0-9:\-_\.]*[a-zA-Z:\-_\.]+[a-zA-Z0-9:\-_\.]*$", RegexOptions.Compiled | RegexOptions.Singleline);

        #region Argument definitions

        /// <summary>
        /// Ids for supported arguments
        /// </summary>
        private static class KeywordIds
        {
            public const string ProjectKey = "projectKey.id";
            public const string ProjectName = "projectName.id";
            public const string ProjectVersion = "projectVersion.id";
            public const string Organization = "organization.id";
            public const string InstallLoaderTargets = "installLoaderTargets.id";
        }

        private static IList<ArgumentDescriptor> Descriptors;

        static ArgumentProcessor()
        {
            // Initialize the set of valid descriptors.
            // To add a new argument, just add it to the list.
            Descriptors = new List<ArgumentDescriptor>
            {
                new ArgumentDescriptor(
                id: KeywordIds.ProjectKey, prefixes:  CommandLineFlagPrefix.GetPrefixFlag(new string[] { "key:","k:" }), required: true, allowMultiple: false,
                description: Resources.CmdLine_ArgDescription_ProjectKey),

                new ArgumentDescriptor(
                id: KeywordIds.ProjectName, prefixes: CommandLineFlagPrefix.GetPrefixFlag(new string[] { "name:","n:" }), required: false, allowMultiple: false,
                description: Resources.CmdLine_ArgDescription_ProjectName),

                new ArgumentDescriptor(
                id: KeywordIds.ProjectVersion, prefixes: CommandLineFlagPrefix.GetPrefixFlag(new string[] { "version:","v:" }), required: false,
                allowMultiple: false, description: Resources.CmdLine_ArgDescription_ProjectVersion),

                new ArgumentDescriptor(
                id: KeywordIds.Organization, prefixes: CommandLineFlagPrefix.GetPrefixFlag(new string[] { "organization:","o:" }), required: false,
                allowMultiple: false, description: Resources.CmdLine_ArgDescription_Organization),

                new ArgumentDescriptor(
                id: KeywordIds.InstallLoaderTargets,prefixes:CommandLineFlagPrefix.GetPrefixFlag(new string[] { "install:"}), required: false,
                allowMultiple: false, description: Resources.CmdLine_ArgDescription_InstallTargets),

                FilePropertyProvider.Descriptor,
                CmdLineArgPropertyProvider.Descriptor
            };

            Debug.Assert(Descriptors.All(d => d.Prefixes != null && d.Prefixes.Any()), "All descriptors must provide at least one prefix");
            Debug.Assert(Descriptors.Select(d => d.Id).Distinct().Count() == Descriptors.Count, "All descriptors must have a unique id");
        }

        #endregion Argument definitions

        #region Public methods

        /// <summary>
        /// Attempts to process the supplied command line arguments and
        /// reports any errors using the logger.
        /// Returns null unless all of the properties are valid.
        /// </summary>
        public static ProcessedArgs TryProcessArgs(string[] commandLineArgs, ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            ProcessedArgs processed = null;

            // This call will fail if there are duplicate, missing, or unrecognized arguments
            var parser = new CommandLineParser(Descriptors, false /* don't allow unrecognized */);
            var parsedOk = parser.ParseArguments(commandLineArgs, logger, out var arguments);

            // Handle the /install: command line only argument
            parsedOk &= TryGetInstallTargetsEnabled(arguments, logger, out var installLoaderTargets);

            // Handler for command line analysis properties
            parsedOk &= CmdLineArgPropertyProvider.TryCreateProvider(arguments, logger,
                out var cmdLineProperties);

            // Handler for scanner environment properties
            parsedOk &= EnvScannerPropertiesProvider.TryCreateProvider(logger, out var scannerEnvProperties);

            // Handler for property file
            var asmPath = Path.GetDirectoryName(typeof(ArgumentProcessor).Assembly.Location);
            parsedOk &= FilePropertyProvider.TryCreateProvider(arguments, asmPath, logger,
                out var globalFileProperties);

            if (parsedOk)
            {
                Debug.Assert(cmdLineProperties != null);
                Debug.Assert(globalFileProperties != null);

                processed = new ProcessedArgs(
                    GetArgumentValue(KeywordIds.ProjectKey, arguments),
                    GetArgumentValue(KeywordIds.ProjectName, arguments),
                    GetArgumentValue(KeywordIds.ProjectVersion, arguments),
                    GetArgumentValue(KeywordIds.Organization, arguments),
                    installLoaderTargets,
                    cmdLineProperties,
                    globalFileProperties,
                    scannerEnvProperties);

                if (!AreParsedArgumentsValid(processed, logger))
                {
                    processed = null;
                }
            }

            return processed;
        }

        #endregion Public methods

        #region Private methods

        private static string GetArgumentValue(string id, IEnumerable<ArgumentInstance> arguments)
        {
            return arguments.Where(a => a.Descriptor.Id == id).Select(a => a.Value).SingleOrDefault();
        }

        /// <summary>
        /// Performs any additional validation on the parsed arguments and logs errors
        /// if necessary.
        /// </summary>
        /// <returns>True if the arguments are valid, otherwise false</returns>
        private static bool AreParsedArgumentsValid(ProcessedArgs args, ILogger logger)
        {
            var areValid = true;

            var projectKey = args.ProjectKey;
            if (!IsValidProjectKey(projectKey))
            {
                logger.LogError(Resources.ERROR_InvalidProjectKeyArg);
                areValid = false;
            }

            return areValid;
        }

        private static bool TryGetInstallTargetsEnabled(IEnumerable<ArgumentInstance> arguments, ILogger logger, out bool installTargetsEnabled)
        {
            var hasInstallTargetsVerb = ArgumentInstance.TryGetArgument(KeywordIds.InstallLoaderTargets, arguments, out var argumentInstance);

            if (hasInstallTargetsVerb)
            {
                var canParse = bool.TryParse(argumentInstance.Value, out installTargetsEnabled);

                if (!canParse)
                {
                    logger.LogError(Resources.ERROR_CmdLine_InvalidInstallTargetsValue, argumentInstance.Value);
                    return false;
                }
            }
            else
            {
                installTargetsEnabled = TargetsInstaller.DefaultInstallSetting;
            }

            return true;
        }

        private static bool IsValidProjectKey(string projectKey)
        {
            return ProjectKeyRegEx.IsMatch(projectKey);
        }

        #endregion Private methods
    }
}
