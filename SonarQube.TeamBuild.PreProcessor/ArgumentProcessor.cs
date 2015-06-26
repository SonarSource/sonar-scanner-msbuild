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
using System.Reflection;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Process and validates the pre-processor command line arguments and reports any errors
    /// </summary>
    public static class ArgumentProcessor // was internal
    {
        #region Argument definitions

        /// <summary>
        /// Ids for supported arguments
        /// </summary>
        private static class KeywordIds
        {
            public const string ProjectKey = "1";
            public const string ProjectName = "2";
            public const string ProjectVersion = "3";
            public const string RunnerPropertiesPath = "4";
            public const string DynamicSetting = "5";
        }

        private static IList<ArgumentDescriptor> Descriptors;

        static ArgumentProcessor()
        {
            // Initialise the set of valid descriptors.
            // To add a new argument, just add it to the list.
            Descriptors = new List<ArgumentDescriptor>();

            Descriptors.Add(new ArgumentDescriptor(
                id: KeywordIds.ProjectKey, prefixes: new string[] { "/key:", "/k:" }, required: true, allowMultiple:false, description:Resources.CmdLine_ArgDescription_ProjectKey));

            Descriptors.Add(new ArgumentDescriptor(
                id: KeywordIds.ProjectName, prefixes: new string[] { "/name:", "/n:" }, required: true, allowMultiple: false, description: Resources.CmdLine_ArgDescription_ProjectName));
            
            Descriptors.Add(new ArgumentDescriptor(
                id: KeywordIds.ProjectVersion, prefixes: new string[] { "/version:", "/v:" }, required: true, allowMultiple: false, description: Resources.CmdLine_ArgDescription_ProjectVersion));
            
            Descriptors.Add(AnalysisPropertyFileProvider.Descriptor);
            Descriptors.Add(CmdLineArgPropertyProvider.Descriptor);

            Debug.Assert(Descriptors.All(d => d.Prefixes != null && d.Prefixes.Any()), "All descriptors must provide at least one prefix");
            Debug.Assert(Descriptors.Select(d => d.Id).Distinct().Count() == Descriptors.Count, "All descriptors must have a unique id");
        }

        #endregion

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
                throw new ArgumentNullException("logger");
            }

            ProcessedArgs processed = null;
            IEnumerable<ArgumentInstance> arguments;

            // This call will fail if there are duplicate, missing, or unrecognized arguments
            CommandLineParser parser = new CommandLineParser(Descriptors, false /* don't allow unrecognized */);
            bool parsedOk = parser.ParseArguments(commandLineArgs, logger, out arguments);

            // Handler for command line analysis properties
            IAnalysisPropertyProvider cmdLineProperties;
            parsedOk &= CmdLineArgPropertyProvider.TryCreateProvider(arguments, logger, out cmdLineProperties);

            // Handler for property file
            IAnalysisPropertyProvider globalFileProperties;
            string asmPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            parsedOk &= AnalysisPropertyFileProvider.TryCreateProvider(arguments, asmPath, logger, out globalFileProperties);

            if (parsedOk)
            {
                Debug.Assert(cmdLineProperties != null);
                Debug.Assert(globalFileProperties != null);

                processed = new ProcessedArgs(
                    TryGetArgumentValue(KeywordIds.ProjectKey, arguments),
                    TryGetArgumentValue(KeywordIds.ProjectName, arguments),
                    TryGetArgumentValue(KeywordIds.ProjectVersion, arguments),
                    cmdLineProperties,
                    globalFileProperties);
            }

            return processed;
        }

        #endregion

        #region Private methods
        
        private static string TryGetArgumentValue(string id, IEnumerable<ArgumentInstance> arguments)
        {
            ArgumentInstance argument = arguments.SingleOrDefault(a => a.Descriptor.Id == id);
            return argument == null ? null : argument.Value;
        }
        
        #endregion
    }
}