/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2021 SonarSource SA
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
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.CommandLine;

namespace SonarScanner.MSBuild
{
    /// <summary>
    /// Processes the command line arguments.
    /// Supports the standard property-related arguments automatically (i.e. /d: and /s:).
    /// The appropriate "additionalDescriptors" should be supplied to provide support for other command line arguments.
    /// </summary>
    public static class ArgumentProcessor
    {
        // FIX: this code is very similar to that in the pre-processor. Consider refactoring to avoid duplication
        // once the other argument and properties-writing tickets have been completed.

        #region Arguments definitions

        private const string BeginId = "begin.id";
        private const string EndId = "end.id";
        public const string HelpId = "help.id";

        public const string BeginVerb = "begin";
        public const string EndVerb = "end";

        private static IList<ArgumentDescriptor> Descriptors;

        static ArgumentProcessor()
        {
            // Initialize the set of valid descriptors.
            // To add a new argument, just add it to the list.
            Descriptors = new List<ArgumentDescriptor>
            {
                new ArgumentDescriptor(
                id: BeginId, prefixes: new string[] { BeginVerb }, required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_Begin, isVerb: true),

                new ArgumentDescriptor(
                id: EndId, prefixes: new string[] { EndVerb }, required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_End, isVerb: true),

                FilePropertyProvider.Descriptor,
                CmdLineArgPropertyProvider.Descriptor
            };

            Debug.Assert(Descriptors.All(d => d.Prefixes != null && d.Prefixes.Any()), "All descriptors must provide at least one prefix");
            Debug.Assert(Descriptors.Select(d => d.Id).Distinct().Count() == Descriptors.Count, "All descriptors must have a unique id");
        }

        #endregion Arguments definitions

        #region Public methods

        public static bool IsHelp(string[] commandLineArgs)
        {
            var helpPrefixes = CommandLineFlagPrefix.GetPrefixedFlags("h", "?");

            foreach (var helpPrefix in helpPrefixes)
            {
                if (commandLineArgs.Contains(helpPrefix))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to process the supplied command line arguments and reports any errors using the logger.
        /// Returns false if any parsing errors were encountered.
        /// </summary>
        public static bool TryProcessArgs(string[] commandLineArgs, ILogger logger, out IBootstrapperSettings settings)
        {
            if (commandLineArgs == null)
            {
                throw new ArgumentNullException(nameof(commandLineArgs));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            settings = null;

            // This call will fail if there are duplicate or missing arguments
            var parser = new CommandLineParser(Descriptors, true /* allow unrecognized arguments*/);
            var parsedOk = parser.ParseArguments(commandLineArgs, logger, out IEnumerable<ArgumentInstance> arguments);

            // Handler for command line analysis properties
            parsedOk &= CmdLineArgPropertyProvider.TryCreateProvider(arguments, logger, out IAnalysisPropertyProvider cmdLineProperties);

            // Handler for property file
            var asmPath = Path.GetDirectoryName(typeof(ArgumentProcessor).Assembly.Location);
            parsedOk &= FilePropertyProvider.TryCreateProvider(arguments, asmPath, logger, out IAnalysisPropertyProvider globalFileProperties);

            parsedOk &= TryGetPhase(commandLineArgs.Length, arguments, logger, out AnalysisPhase phase);

            Debug.Assert(!parsedOk || cmdLineProperties != null);
            Debug.Assert(!parsedOk || globalFileProperties != null);

            if (parsedOk)
            {
                Debug.Assert(cmdLineProperties != null);
                Debug.Assert(globalFileProperties != null);
                IAnalysisPropertyProvider properties = new AggregatePropertiesProvider(cmdLineProperties, globalFileProperties);

                var baseChildArgs = RemoveBootstrapperArgs(commandLineArgs);

                if (phase == AnalysisPhase.PreProcessing)
                {
                    settings = CreatePreProcessorSettings(baseChildArgs, properties, globalFileProperties, logger);
                }
                else
                {
                    settings = CreatePostProcessorSettings(baseChildArgs, properties, logger);
                }
            }

            return settings != null;
        }

        #endregion Public methods

        #region Private methods

        private static bool TryGetPhase(int originalArgCount, IEnumerable<ArgumentInstance> arguments, ILogger logger, out AnalysisPhase phase)
        {
            // The command line parser will already have checked for duplicates
            var hasBeginVerb = ArgumentInstance.TryGetArgument(BeginId, arguments, out ArgumentInstance argumentInstance);
            var hasEndVerb = ArgumentInstance.TryGetArgument(EndId, arguments, out argumentInstance);

            if (hasBeginVerb && hasEndVerb) // both
            {
                phase = AnalysisPhase.Unspecified;
                logger.LogError(Resources.ERROR_CmdLine_BothBeginAndEndSupplied);
            }
            else if (!hasBeginVerb && !hasEndVerb) // neither
            {
                // Backwards compatibility - decide the phase based on the number of arguments passed
                phase = originalArgCount == 0 ? AnalysisPhase.PostProcessing : AnalysisPhase.PreProcessing;
                logger.LogWarning(Resources.WARN_CmdLine_v09_Compat);
            }
            else // begin or end
            {
                phase = hasBeginVerb ? AnalysisPhase.PreProcessing : AnalysisPhase.PostProcessing;
            }

            return phase != AnalysisPhase.Unspecified;
        }

        private static IBootstrapperSettings CreatePreProcessorSettings(ICollection<string> childArgs, IAnalysisPropertyProvider properties, IAnalysisPropertyProvider globalFileProperties, ILogger logger)
        {
            // If we're using the default properties file then we need to pass it
            // explicitly to the pre-processor (it's in a different folder and won't
            // be able to find it otherwise).
            if (globalFileProperties is FilePropertyProvider fileProvider && fileProvider.IsDefaultSettingsFile)
            {
                Debug.Assert(fileProvider.PropertiesFile != null);
                Debug.Assert(!string.IsNullOrEmpty(fileProvider.PropertiesFile.FilePath), "Expecting the properties file path to be set");
                childArgs.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}{1}", FilePropertyProvider.Prefix, fileProvider.PropertiesFile.FilePath));
            }

            return CreateSettings(AnalysisPhase.PreProcessing, childArgs, properties, logger);
        }

        private static IBootstrapperSettings CreatePostProcessorSettings(IEnumerable<string> childArgs, IAnalysisPropertyProvider properties, ILogger logger)
        {
            return CreateSettings(AnalysisPhase.PostProcessing, childArgs, properties, logger);
        }

        private static IBootstrapperSettings CreateSettings(AnalysisPhase phase, IEnumerable<string> childArgs, IAnalysisPropertyProvider properties, ILogger logger)
        {
            return new BootstrapperSettings(
                phase,
                childArgs,
                VerbosityCalculator.ComputeVerbosity(properties, logger),
                logger);
        }

        /// <summary>
        /// Strips out any arguments that are only relevant to the bootstrapper from the user-supplied
        /// command line arguments
        /// </summary>
        /// <remarks>We don't want to forward these arguments to the pre- or post- processor</remarks>
        private static IList<string> RemoveBootstrapperArgs(string[] commandLineArgs)
        {
            if (commandLineArgs == null)
            {
                throw new ArgumentNullException(nameof(commandLineArgs));
            }

            var excludedVerbs = new string[] { BeginVerb, EndVerb };
            var excludedPrefixes = new string[] { };

            return commandLineArgs
                .Except(excludedVerbs)
                .Where(arg => !excludedPrefixes.Any(e => arg.StartsWith(e, ArgumentDescriptor.IdComparison)))
                .ToList();
        }

        #endregion Private methods
    }
}
