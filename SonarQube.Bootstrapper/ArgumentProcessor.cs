//-----------------------------------------------------------------------
// <copyright file="ArgumentProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SonarQube.Bootstrapper
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

        public const string BeginVerb = "begin";
        public const string EndVerb = "end";

        private static IList<ArgumentDescriptor> Descriptors;

        static ArgumentProcessor()
        {
            // Initialise the set of valid descriptors.
            // To add a new argument, just add it to the list.
            Descriptors = new List<ArgumentDescriptor>();

            Descriptors.Add(new ArgumentDescriptor(
                id: BeginId, prefixes: new string[] { BeginVerb }, required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_Begin, isVerb: true));

            Descriptors.Add(new ArgumentDescriptor(
                id: EndId, prefixes: new string[] { EndVerb }, required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_End, isVerb: true));

            Descriptors.Add(FilePropertyProvider.Descriptor);
            Descriptors.Add(CmdLineArgPropertyProvider.Descriptor);

            Debug.Assert(Descriptors.All(d => d.Prefixes != null && d.Prefixes.Any()), "All descriptors must provide at least one prefix");
            Debug.Assert(Descriptors.Select(d => d.Id).Distinct().Count() == Descriptors.Count, "All descriptors must have a unique id");
        }

        #endregion Arguments definitions

        #region Public methods

        /// <summary>
        /// Attempts to process the supplied command line arguments and
        /// reports any errors using the logger.
        /// Returns false if any parsing errors were encountered.
        /// </summary>
        public static bool TryProcessArgs(string[] commandLineArgs, ILogger logger, out IBootstrapperSettings settings)
        {
            if (commandLineArgs == null)
            {
                throw new ArgumentNullException("commandLineArgs");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            settings = null;

            IEnumerable<ArgumentInstance> arguments;

            // This call will fail if there are duplicate or missing arguments
            CommandLineParser parser = new CommandLineParser(Descriptors, true /* allow unrecognized arguments*/);
            bool parsedOk = parser.ParseArguments(commandLineArgs, logger, out arguments);

            // Handler for command line analysis properties
            IAnalysisPropertyProvider cmdLineProperties;
            parsedOk &= CmdLineArgPropertyProvider.TryCreateProvider(arguments, logger, out cmdLineProperties);

            // Handler for property file
            IAnalysisPropertyProvider globalFileProperties;
            string asmPath = Path.GetDirectoryName(typeof(Bootstrapper.ArgumentProcessor).Assembly.Location);
            parsedOk &= FilePropertyProvider.TryCreateProvider(arguments, asmPath, logger, out globalFileProperties);

            AnalysisPhase phase;
            parsedOk &= TryGetPhase(commandLineArgs.Length, arguments, logger, out phase);

            Debug.Assert(!parsedOk || cmdLineProperties != null);
            Debug.Assert(!parsedOk || globalFileProperties != null);

            if (parsedOk)
            {
                Debug.Assert(cmdLineProperties != null);
                Debug.Assert(globalFileProperties != null);
                IAnalysisPropertyProvider properties = new AggregatePropertiesProvider(cmdLineProperties, globalFileProperties);

                IList<string> baseChildArgs = RemoveBootstrapperArgs(commandLineArgs);

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
            ArgumentInstance argumentInstance;
            bool hasBeginVerb = ArgumentInstance.TryGetArgument(BeginId, arguments, out argumentInstance);
            bool hasEndVerb = ArgumentInstance.TryGetArgument(EndId, arguments, out argumentInstance);

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

        private static IBootstrapperSettings CreatePreProcessorSettings(IList<string> childArgs, IAnalysisPropertyProvider properties, IAnalysisPropertyProvider globalFileProperties, ILogger logger)
        {
            string hostUrl = TryGetHostUrl(properties, logger);
            if (hostUrl == null)
            {
                return null; // URL is a required parameter in the pre-process phase
            }

            // If we're using the default properties file then we need to pass it
            // explicitly to the pre-processor (it's in a different folder and won't
            // be able to find it otherwise).
            FilePropertyProvider fileProvider = globalFileProperties as FilePropertyProvider;
            if (fileProvider != null && fileProvider.IsDefaultSettingsFile)
            {
                Debug.Assert(fileProvider.PropertiesFile != null);
                Debug.Assert(!string.IsNullOrEmpty(fileProvider.PropertiesFile.FilePath), "Expecting the properties file path to be set");
                childArgs.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}{1}", FilePropertyProvider.Prefix, fileProvider.PropertiesFile.FilePath));
            }

            IBootstrapperSettings settings = new BootstrapperSettings(
                AnalysisPhase.PreProcessing,
                childArgs,
                hostUrl,
                VerbosityCalculator.ComputeVerbosity(properties, logger),
                logger);

            return settings;
        }

        private static string TryGetHostUrl(IAnalysisPropertyProvider properties, ILogger logger)
        {
            string url;
            if (properties.TryGetValue(SonarProperties.HostUrl, out url))
            {
                return url;
            }

            logger.LogError(Resources.ERROR_Args_UrlRequired);
            return null;
        }

        private static IBootstrapperSettings CreatePostProcessorSettings(IList<string> childArgs, IAnalysisPropertyProvider properties, ILogger logger)
        {
            IBootstrapperSettings settings = new BootstrapperSettings(
                AnalysisPhase.PostProcessing,
                childArgs,
                string.Empty,
                VerbosityCalculator.ComputeVerbosity(properties, logger),
                logger);

            return settings;
        }

        /// <summary>
        /// Strips out any arguments that are only relevant to the boot strapper from the user-supplied
        /// command line arguments
        /// </summary>
        /// <remarks>We don't want to forward these arguments to the pre- or post- processor</remarks>
        private static IList<string> RemoveBootstrapperArgs(string[] commandLineArgs)
        {
            if (commandLineArgs == null)
            {
                throw new ArgumentNullException("param");
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