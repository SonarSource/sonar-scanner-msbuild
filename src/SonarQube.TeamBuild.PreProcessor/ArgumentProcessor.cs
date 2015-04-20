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

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Process and validates the command line arguments and reports any errors
    /// </summary>
    /// <remarks>The command line parsing makes a number of simplying assumptions:
    /// * order is unimportant
    /// * all arguments have a recognisable prefix e.g. /key= 
    /// * prefixes are case-insensitive
    /// * duplicates are not allowed
    /// * unrecognised arguments are treated as errors
    /// * the command line arguments are those supplied in Main(args) i.e. they have been converted
    ///   from a string to an array by the runtime. This means that quoted arguments will already have
    ///   been partially processed so a command line of:
    ///        myApp.exe "quoted arg" /k="ab cd" ""
    ///   will be supplied as three args, [quoted arg] , [/k=ab cd] and String.Empty</remarks>
    internal static class ArgumentProcessor
    {
        /// <summary>
        /// Ids for supported arguments
        /// </summary>
        private enum KeywordIds
        {
            ProjectKey = 1,
            ProjectName,
            ProjectVersion,
            RunnerPropertiesPath
        }

        #region Argument definitions

        private static IList<ArgumentDescriptor> Descriptors;

        private class ArgumentDescriptor
        {
            /// <summary>
            /// The unique (internal) identifier for the argument
            /// </summary>
            public KeywordIds Id { get; set; }

            /// <summary>
            /// Any prefixes supported for the argument. This should include all of the characters that
            /// are not to be treated as part of the value e.g. /key=
            /// </summary>
            public string[] Prefixes { get; set; }

            /// <summary>
            /// Whether the argument is mandatory or not
            /// </summary>
            public bool Required { get; set; }

            /// <summary>
            /// A short description of the argument that will be displayed to the user
            /// e.g. /key= [SonarQube project key]
            /// </summary>
            public string Description { get; set; }
        }

        static ArgumentProcessor()
        {
            // Initialise the set of valid descriptors.
            // To add a new argument, just add it to the list.
            Descriptors = new List<ArgumentDescriptor>();

            Descriptors.Add(new ArgumentDescriptor()
                { Id = KeywordIds.ProjectKey, Prefixes = new string[] { "/key:", "/k:" }, Required = true, Description = Resources.CmdLine_ArgDescription_ProjectKey });
            
            Descriptors.Add(new ArgumentDescriptor()
                { Id = KeywordIds.ProjectName, Prefixes = new string[] { "/name:", "/n:" }, Required = true, Description = Resources.CmdLine_ArgDescription_ProjectName });
            
            Descriptors.Add(new ArgumentDescriptor()
                { Id = KeywordIds.ProjectVersion, Prefixes = new string[] { "/version:", "/v:" }, Required = true, Description = Resources.CmdLine_ArgDescription_ProjectVersion });
            
            Descriptors.Add(new ArgumentDescriptor()
                { Id = KeywordIds.RunnerPropertiesPath, Prefixes = new string[] { "/runnerProperties:", "/r:" }, Required = false, Description = Resources.CmdLine_ArgDescription_PropertiesPath });


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
            IDictionary<KeywordIds, string> idToValueMap;
            
            // This call will fail if there are duplicate or unrecognised arguments...
            bool parsedOk = ParseArguments(commandLineArgs, logger, out idToValueMap);
            
            // ... and this one will fail if any required arguments are missing.
            // We'll check this even if the parsing failed so we output as much detail
            // as possible about the failures.
            bool allRequiredExist = CheckRequiredArgumentsSupplied(idToValueMap, logger);

            if(parsedOk && allRequiredExist)
            {
                string propertiesPath;
                idToValueMap.TryGetValue(KeywordIds.RunnerPropertiesPath, out propertiesPath);
                propertiesPath = TryResolveRunnerPropertiesPath(propertiesPath, logger);

                if (!string.IsNullOrEmpty(propertiesPath))
                {
                    processed = new ProcessedArgs(
                        idToValueMap[KeywordIds.ProjectKey],
                        idToValueMap[KeywordIds.ProjectName],
                        idToValueMap[KeywordIds.ProjectVersion],
                        propertiesPath);
                }
            }
            return processed;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Parses the supplied arguments. Logs errors for unrecognised or duplicate arguments.
        /// </summary>
        /// <param name="idToValueMap">A map of (argument id -> supplied value)</param>
        /// <returns></returns>
        private static bool ParseArguments(string[] commandLineArgs, ILogger logger, out IDictionary<KeywordIds, string> idToValueMap)
        {
            bool parsedOk = true;
            // Property bag of the values that have been recognised
            idToValueMap = new Dictionary<KeywordIds, string>();

            foreach (string arg in commandLineArgs)
            {
                string prefix;
                ArgumentDescriptor descriptor;
                
                if(TryGetMatchingDescriptor(arg, out descriptor, out prefix))
                {
                    KeywordIds newId = descriptor.Id;
                    if (idToValueMap.ContainsKey(newId))
                    {
                        logger.LogError(Resources.ERROR_CmdLine_DuplicateArg, arg, idToValueMap[newId]);
                        parsedOk = false;
                    }
                    else
                    {
                        // Store the argument
                        string argValue = arg.Substring(prefix.Length);
                        idToValueMap[newId] = argValue;
                    }
                }
                else
                {
                    logger.LogError(Resources.ERROR_CmdLine_UnrecognisedArg, arg);
                    parsedOk = false;
                }
            }

            return parsedOk;
        }

        private static bool TryGetMatchingDescriptor(string argument, out ArgumentDescriptor descriptor, out string prefix)
        {
            descriptor = null;
            prefix = null;

            bool found = false;

            foreach(ArgumentDescriptor item in Descriptors)
            {
                string match = TryGetMatchingPrefix(item, argument);
                if (match != null)
                {
                    descriptor = item;
                    prefix = match;
                    found = true;
                    break;
                }
            }
            return found;
        }

        private static string TryGetMatchingPrefix(ArgumentDescriptor descriptor, string argument)
        {
            Debug.Assert(descriptor.Prefixes.Where(p => argument.StartsWith(p, StringComparison.OrdinalIgnoreCase)).Count() < 2,
                "Not expecting the argument to match multiple prefixes");

            string match = descriptor.Prefixes.FirstOrDefault(p => argument.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            return match;
        }

        private static bool CheckRequiredArgumentsSupplied(IDictionary<KeywordIds, string> idToValueMap, ILogger logger)
        {
            bool allExist = true;
            foreach(ArgumentDescriptor desc in Descriptors.Where(d => d.Required))
            {
                bool exists = idToValueMap.ContainsKey(desc.Id) && !string.IsNullOrWhiteSpace(idToValueMap[desc.Id]);
                if (!exists)
                {
                    logger.LogError(Resources.ERROR_CmdLine_MissingRequiredArgument, desc.Description);
                    allExist = false;
                }
            }
            return allExist;
        }

        private static string TryResolveRunnerPropertiesPath(string propertiesPath, ILogger logger)
        {
            string resolvedPath;

            if (string.IsNullOrEmpty(propertiesPath))
            {
                logger.LogMessage(Resources.DIAG_LocatingRunnerProperties);
                resolvedPath = FileLocator.FindDefaultSonarRunnerProperties();

                if (string.IsNullOrWhiteSpace(resolvedPath))
                {
                    logger.LogError(Resources.ERROR_FailedToLocateRunnerProperties);
                }
                else
                {
                    logger.LogMessage(Resources.DIAG_LocatedRunnerProperties, resolvedPath);
                }
            }
            else
            {
                if (File.Exists(propertiesPath))
                {
                    resolvedPath = propertiesPath;
                }
                else
                {
                    logger.LogError(Resources.ERROR_InvalidRunnerPropertiesLocationSupplied, propertiesPath);
                    resolvedPath = null;
                }
            }

            return resolvedPath;
        }

        #endregion

    }
}
