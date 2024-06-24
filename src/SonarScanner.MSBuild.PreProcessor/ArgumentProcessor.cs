/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using static SonarScanner.MSBuild.Common.CommandLine.CommandLineFlagPrefix;

namespace SonarScanner.MSBuild.PreProcessor
{
    /// <summary>
    /// Process and validates the pre-processor command line arguments and reports any errors
    /// </summary>
    public static class ArgumentProcessor // was internal
    {
        private const string ProjectKeyId = "projectKey.id";
        private const string ProjectNameId = "projectName.id";
        private const string ProjectVersionId = "projectVersion.id";
        private const string OrganizationId = "organization.id";
        private const string InstallLoaderTargetsId = "installLoaderTargets.id";

        /// <summary>
        /// Regular expression to validate a project key.
        /// See http://docs.sonarqube.org/display/SONAR/Project+Administration#ProjectAdministration-AddingaProject
        /// </summary>
        /// <remarks>Should match the java regex here: https://github.com/SonarSource/sonarqube/blob/5.1.1/sonar-core/src/main/java/org/sonar/core/component/ComponentKeys.java#L36
        /// "Allowed characters are alphanumeric, '-', '_', '.' and ':', with at least one non-digit"
        /// </remarks>
        private static readonly Regex ProjectKeyRegEx = new(@"^[a-zA-Z0-9:\-_\.]*[a-zA-Z:\-_\.]+[a-zA-Z0-9:\-_\.]*$", RegexOptions.Compiled | RegexOptions.Singleline, RegexConstants.DefaultTimeout);

        private static readonly IList<ArgumentDescriptor> Descriptors = new List<ArgumentDescriptor>
        {
            new(id: ProjectKeyId, prefixes: GetPrefixedFlags("key:", "k:"), required: true, allowMultiple: false, description: Resources.CmdLine_ArgDescription_ProjectKey),
            new(id: ProjectNameId, prefixes: GetPrefixedFlags("name:", "n:"), required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_ProjectName),
            new(id: ProjectVersionId, prefixes: GetPrefixedFlags("version:", "v:"), required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_ProjectVersion),
            new(id: OrganizationId, prefixes: GetPrefixedFlags("organization:", "o:"), required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_Organization),
            new(id: InstallLoaderTargetsId, prefixes: GetPrefixedFlags("install:"), required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_InstallTargets),

            FilePropertyProvider.Descriptor,
            CmdLineArgPropertyProvider.Descriptor
        };

        static ArgumentProcessor()
        {
            Debug.Assert(Descriptors.All(d => d.Prefixes != null && d.Prefixes.Any()), "All descriptors must provide at least one prefix");
            Debug.Assert(Descriptors.Select(d => d.Id).Distinct().Count() == Descriptors.Count, "All descriptors must have a unique id");
        }

        /// <summary>
        /// Attempts to process the supplied command line arguments and
        /// reports any errors using the logger.
        /// Returns null unless all of the properties are valid.
        /// </summary>
        public static ProcessedArgs TryProcessArgs(IEnumerable<string> commandLineArgs, ILogger logger)
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
                Debug.Assert(cmdLineProperties != null, "When parse is valid, expected cmd line properties to be non-null");
                Debug.Assert(globalFileProperties != null, "When parse is valid, expected global file properties to be non-null");

                processed = new ProcessedArgs(
                    ArgumentValue(ProjectKeyId, arguments),
                    ArgumentValue(ProjectNameId, arguments),
                    ArgumentValue(ProjectVersionId, arguments),
                    ArgumentValue(OrganizationId, arguments),
                    installLoaderTargets,
                    cmdLineProperties,
                    globalFileProperties,
                    scannerEnvProperties,
                    logger);

                if (!AreParsedArgumentsValid(processed, logger))
                {
                    processed = null;
                }
            }

            return processed;
        }

        private static string ArgumentValue(string id, IEnumerable<ArgumentInstance> arguments) =>
            arguments.Where(a => a.Descriptor.Id == id).Select(a => a.Value).SingleOrDefault();

        /// <summary>
        /// Performs any additional validation on the parsed arguments and logs errors
        /// if necessary.
        /// </summary>
        /// <returns>True if the arguments are valid, otherwise false.</returns>
        private static bool AreParsedArgumentsValid(ProcessedArgs args, ILogger logger)
        {
            var areValid = true;

            var projectKey = args.ProjectKey;
            if (!IsValidProjectKey(projectKey))
            {
                logger.LogError(Resources.ERROR_InvalidProjectKeyArg);
                areValid = false;
            }

            if (!args.IsOrganizationValid)
            {
                areValid = false;
            }

            if (args.SonarServer is null)
            {
                areValid = false;
            }

            return areValid;
        }

        private static bool TryGetInstallTargetsEnabled(IEnumerable<ArgumentInstance> arguments, ILogger logger, out bool installTargetsEnabled)
        {
            var hasInstallTargetsVerb = ArgumentInstance.TryGetArgument(InstallLoaderTargetsId, arguments, out var argumentInstance);

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

        private static bool IsValidProjectKey(string projectKey) =>
            ProjectKeyRegEx.IsMatch(projectKey);
    }
}
