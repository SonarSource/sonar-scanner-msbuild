//-----------------------------------------------------------------------
// <copyright file="CommandLineParser.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SonarQube.Common
{
    /// <summary>
    /// Process and validates the command line arguments and reports any errors
    /// </summary>
    /// <remarks>The command line parsing makes a number of simplying assumptions:
    /// * order is unimportant
    /// * all arguments have a recognisable prefix e.g. /key= 
    /// * prefixes are case-insensitive
    /// * unrecognised arguments are treated as errors
    /// * the command line arguments are those supplied in Main(args) i.e. they have been converted
    ///   from a string to an array by the runtime. This means that quoted arguments will already have
    ///   been partially processed so a command line of:
    ///        myApp.exe "quoted arg" /k="ab cd" ""
    ///   will be supplied as three args, [quoted arg] , [/k=ab cd] and String.Empty</remarks>
    public class CommandLineParser
    {
        /// <summary>
        /// List of definitions of valid arguments
        /// </summary>
        private readonly IEnumerable<ArgumentDescriptor> descriptors;

        public CommandLineParser(IEnumerable<ArgumentDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                throw new ArgumentNullException("descriptors");
            }

            Debug.Assert(descriptors.All(d => d.Prefixes != null && d.Prefixes.Any()), "All descriptors must provide at least one prefix");
            Debug.Assert(descriptors.Select(d => d.Id).Distinct(ArgumentDescriptor.IdComparer).Count() == descriptors.Count(), "All descriptors must have a unique id");

            this.descriptors = descriptors;
        }

        /// <summary>
        /// Parses the supplied arguments. Logs errors for unrecognised, duplicate or missing arguments.
        /// </summary>
        /// <param name="argumentInstances">A list of argument instances that have been recognised</param>
        public bool ParseArguments(string[] commandLineArgs, ILogger logger, out IEnumerable<ArgumentInstance> argumentInstances)
        {
            if (commandLineArgs == null)
            {
                throw new ArgumentNullException("commandLineArgs");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }


            bool parsedOk = true;
            
            // List of values that have been recognised
            IList<ArgumentInstance> arguments = new List<ArgumentInstance>();

            foreach (string arg in commandLineArgs)
            {
                string prefix;
                ArgumentDescriptor descriptor;

                if (TryGetMatchingDescriptor(arg, out descriptor, out prefix))
                {
                    string newId = descriptor.Id;

                    if (!descriptor.AllowMultiple && IdExists(newId, arguments))
                    {
                        string existingValue;
                        ArgumentInstance.TryGetArgumentValue(newId, arguments, out existingValue);
                        logger.LogError(Resources.ERROR_CmdLine_DuplicateArg, arg, existingValue);
                        parsedOk = false;
                    }
                    else
                    {
                        // Store the argument
                        string argValue = arg.Substring(prefix.Length);
                        arguments.Add(new ArgumentInstance(descriptor, argValue));
                    }
                }
                else
                {
                    logger.LogError(Resources.ERROR_CmdLine_UnrecognisedArg, arg);
                    parsedOk = false;
                }
            }

            argumentInstances = arguments;

            // We'll for missing arguments this even if the parsing failed so we output as much detail
            // as possible about the failures.
            parsedOk &= CheckRequiredArgumentsSupplied(argumentInstances, logger);

            return parsedOk;
        }

        /// <summary>
        /// Attempts to find a descriptor for the current argument
        /// </summary>
        /// <param name="argument">The argument passed on the command line</param>
        /// <param name="descriptor">The descriptor that matches the argument</param>
        /// <param name="prefix">The specific prefix that was matched</param>
        private bool TryGetMatchingDescriptor(string argument, out ArgumentDescriptor descriptor, out string prefix)
        {
            descriptor = null;
            prefix = null;

            bool found = false;

            foreach (ArgumentDescriptor item in this.descriptors)
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

        private static bool IdExists(string id, IEnumerable<ArgumentInstance> arguments)
        {
            ArgumentInstance existing;
            bool exists = ArgumentInstance.TryGetArgument(id, arguments, out existing);
            return exists;
        }

        /// <summary>
        /// Checks whether any required arguments are missing and logs error messages for them.
        /// </summary>
        private bool CheckRequiredArgumentsSupplied(IEnumerable<ArgumentInstance> arguments, ILogger logger)
        {
            bool allExist = true;
            foreach (ArgumentDescriptor desc in this.descriptors.Where(d => d.Required))
            {
                ArgumentInstance argument;
                ArgumentInstance.TryGetArgument(desc.Id, arguments, out argument);

                bool exists = argument != null && !string.IsNullOrWhiteSpace(argument.Value);
                if (!exists)
                {
                    logger.LogError(Resources.ERROR_CmdLine_MissingRequiredArgument, desc.Description);
                    allExist = false;
                }
            }
            return allExist;
        }
        
    }
}
