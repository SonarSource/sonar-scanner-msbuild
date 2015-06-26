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
using System.Reflection;

namespace SonarQube.Bootstrapper
{
    /// <summary>
    /// Processes the command line arguments.
    /// Supports the standard property-related arguments automatically (i.e. /p: and /s:).
    /// The appropriate "additionalDescriptors" should be supplied to provide support for other command line arguments.
    /// </summary>
    public static class ArgumentProcessor
    {
        // FIX: this code is very similar to that in the pre-processor. Consider refactoring to avoid duplication
        // once the other argument and properties-writing tickets have been completed.

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
            CommandLineParser parser = new CommandLineParser(new ArgumentDescriptor[] { CmdLineArgPropertyProvider.Descriptor, AnalysisPropertyFileProvider.Descriptor },
                true /* allow unrecognized arguments*/);
            bool parsedOk = parser.ParseArguments(commandLineArgs, logger, out arguments);

            // Handler for command line analysis properties
            IAnalysisPropertyProvider cmdLineProperties;
            parsedOk &= CmdLineArgPropertyProvider.TryCreateProvider(arguments, logger, out cmdLineProperties);

            // Handler for property file
            IAnalysisPropertyProvider globalFileProperties;
            string asmPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            parsedOk &= AnalysisPropertyFileProvider.TryCreateProvider(arguments, asmPath, logger, out globalFileProperties);

            Debug.Assert(!parsedOk || cmdLineProperties != null);
            Debug.Assert(!parsedOk || globalFileProperties != null);

            if (parsedOk)
            {
                Debug.Assert(cmdLineProperties != null);
                Debug.Assert(globalFileProperties != null);
                IAnalysisPropertyProvider properties = new AggregatePropertiesProvider(cmdLineProperties, globalFileProperties);

                string url;
                if (TryGetUrl(properties, logger, out url))
                {
                    Debug.Assert(!string.IsNullOrWhiteSpace(url));
                    settings = new BootstrapperSettings(url, logger);
                }
                else
                {
                    parsedOk = false;
                }
            }

            return parsedOk;
        }

        private static bool TryGetUrl(IAnalysisPropertyProvider properties, ILogger logger, out string url)
        {
            if (properties.TryGetValue(SonarProperties.HostUrl, out url))
            {
                return true;
            }
            else
            {
                logger.LogError(Resources.ERROR_CmdLine_UrlRequired);
                return false;
            }
        }
    }
}
