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
    /// * unrecognised arguments are treated as errors
    /// * the command line arguments are those supplied in Main(args) i.e. they have been converted
    ///   from a string to an array by the runtime. This means that quoted arguments will already have
    ///   been partially processed so a command line of:
    ///        myApp.exe "quoted arg" /k="ab cd" ""
    ///   will be supplied as three args, [quoted arg] , [/k=ab cd] and String.Empty</remarks>
    public static class ArgumentProcessor // was internal
    {
        /// <summary>
        /// Ids for supported arguments
        /// </summary>
        private enum KeywordIds
        {
            ProjectKey = 1,
            ProjectName,
            ProjectVersion,
            RunnerPropertiesPath,
            DynamicSetting
        }

        #region Argument definitions

        private static IList<ArgumentDescriptor> Descriptors;

        /// <summary>
        /// Describes a single valid argument type - id, prefixes, multiplicity etc
        /// </summary>
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

            /// <summary>
            /// True if the argument can be specified multiple times,
            /// false if it can be specified at most once
            /// </summary>
            public bool AllowMultiple { get; set; }
        }

        /// <summary>
        /// Data-class for an instance of an argument
        /// </summary>
        private class ArgumentInstance
        {
            private readonly ArgumentDescriptor descriptor;
            private readonly string value;

            public ArgumentInstance(ArgumentDescriptor descriptor, string value)
            {
                if (descriptor == null)
                {
                    throw new ArgumentNullException("descriptor");
                }
                this.descriptor = descriptor;
                this.value = value;
            }

            public ArgumentDescriptor Descriptor { get { return this.descriptor; } }
            public string Value { get { return this.value; } }
        }

        static ArgumentProcessor()
        {
            // Initialise the set of valid descriptors.
            // To add a new argument, just add it to the list.
            Descriptors = new List<ArgumentDescriptor>();

            Descriptors.Add(new ArgumentDescriptor()
                { Id = KeywordIds.ProjectKey, Prefixes = new string[] { "/key:", "/k:" }, Required = true, AllowMultiple = false, Description = Resources.CmdLine_ArgDescription_ProjectKey });
            
            Descriptors.Add(new ArgumentDescriptor()
                { Id = KeywordIds.ProjectName, Prefixes = new string[] { "/name:", "/n:" }, Required = true, AllowMultiple = false, Description = Resources.CmdLine_ArgDescription_ProjectName });
            
            Descriptors.Add(new ArgumentDescriptor()
                { Id = KeywordIds.ProjectVersion, Prefixes = new string[] { "/version:", "/v:" }, Required = true, AllowMultiple = false, Description = Resources.CmdLine_ArgDescription_ProjectVersion });
            
            Descriptors.Add(new ArgumentDescriptor()
                { Id = KeywordIds.RunnerPropertiesPath, Prefixes = new string[] { "/runnerProperties:", "/r:" }, Required = false, AllowMultiple = false, Description = Resources.CmdLine_ArgDescription_PropertiesPath });

            Descriptors.Add(new ArgumentDescriptor()
                { Id = KeywordIds.DynamicSetting, Prefixes = new string[] { "/p:" }, Required = false, AllowMultiple = true, Description = Resources.CmdLine_ArgDescription_DynamicSetting });

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
            
            // This call will fail if there are duplicate or unrecognised arguments...
            bool parsedOk = ParseArguments(commandLineArgs, logger, out arguments);
            
            // ... and this one will fail if any required arguments are missing.
            // We'll check this even if the parsing failed so we output as much detail
            // as possible about the failures.
            bool allRequiredExist = CheckRequiredArgumentsSupplied(arguments, logger);

            // The /p:[key]=[value] settings required further processing
            IEnumerable<AnalysisSetting> settings;
            bool analysisSettingsOk = TryGetAnalysisSettings(arguments, logger, out settings);

            if (parsedOk && allRequiredExist && analysisSettingsOk)
            {
                {
                    ArgumentInstance propertyPathArgument = TryGetArgument(KeywordIds.RunnerPropertiesPath, arguments);
                    string propertiesPath = propertyPathArgument == null ? null : propertyPathArgument.Value;
                    propertiesPath = TryResolveRunnerPropertiesPath(propertiesPath, logger);

                    if (!string.IsNullOrEmpty(propertiesPath))
                    {
                        processed = new ProcessedArgs(
                            TryGetArgumentValue(KeywordIds.ProjectKey, arguments),
                            TryGetArgumentValue(KeywordIds.ProjectName, arguments),
                            TryGetArgumentValue(KeywordIds.ProjectVersion, arguments),
                            propertiesPath,
                            settings);
                    }

                }

            }
            return processed;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Parses the supplied arguments. Logs errors for unrecognised or duplicate arguments.
        /// </summary>
        /// <param name="argumentInstances">A list of argument instances that have been recognised</param>
        private static bool ParseArguments(string[] commandLineArgs, ILogger logger, out IEnumerable<ArgumentInstance> argumentInstances)
        {
            bool parsedOk = true;
            // List the values that have been recognised
            IList<ArgumentInstance> arguments = new List<ArgumentInstance>();

            foreach (string arg in commandLineArgs)
            {
                string prefix;
                ArgumentDescriptor descriptor;
                
                if(TryGetMatchingDescriptor(arg, out descriptor, out prefix))
                {
                    KeywordIds newId = descriptor.Id;

                    if (!descriptor.AllowMultiple && IdExists(newId, arguments))
                    {
                        string existingValue = TryGetArgumentValue(newId, arguments);
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

        private static bool IdExists(KeywordIds id, IEnumerable<ArgumentInstance> arguments)
        {
            bool exists = arguments.Any(a => a.Descriptor.Id == id);
            return exists;
        }

        private static ArgumentInstance TryGetArgument(KeywordIds id, IEnumerable<ArgumentInstance> arguments)
        {
            return arguments.FirstOrDefault(a => a.Descriptor.Id == id);
        }

        private static string TryGetArgumentValue(KeywordIds id, IEnumerable<ArgumentInstance> arguments)
        {
            ArgumentInstance argument = arguments.SingleOrDefault(a => a.Descriptor.Id == id);
            return argument == null ? null : argument.Value;
        }

        private static bool CheckRequiredArgumentsSupplied(IEnumerable<ArgumentInstance> arguments, ILogger logger)
        {
            bool allExist = true;
            foreach(ArgumentDescriptor desc in Descriptors.Where(d => d.Required))
            {
                ArgumentInstance argument = TryGetArgument(desc.Id, arguments);

                bool exists = argument != null && !string.IsNullOrWhiteSpace(argument.Value);
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

        #region Analysis settings handling
        /*
            Analysis settings (/p:[key]=[value] arguments) need further processing.
            We need to extract the key-value pairs and check for duplicate keys
        */

        private static bool TryGetAnalysisSettings(IEnumerable<ArgumentInstance> arguments, ILogger logger, out IEnumerable<AnalysisSetting> analysisSettings)
        {
            bool success = true;

            List<AnalysisSetting> settings = new List<AnalysisSetting>();

            foreach (ArgumentInstance argument in arguments.Where(a => a.Descriptor.Id == KeywordIds.DynamicSetting))
            {
                AnalysisSetting setting;
                if (AnalysisSetting.TryParse(argument.Value, out setting))
                {
                    AnalysisSetting existing = TryGetAnalysisSetting(setting.Id, settings);
                    if (existing != null)
                    {
                        logger.LogError(Resources.ERROR_CmdLine_DuplicateArg, argument.Value, existing.Value);
                        success = false;
                    }
                    else
                    {
                        settings.Add(setting);
                    }
                }
                else
                {
                    logger.LogError(Resources.ERROR_CmdLine_InvalidDynamicSetting, argument.Value);
                    success = false;
                }
            }

            // Check for named parameters that can't be set by dynamic properties
            success = success & !ContainsNamedParameter(SonarProperties.ProjectKey, settings, logger, Resources.ERROR_MustUseProjectKey);
            success = success & !ContainsNamedParameter(SonarProperties.ProjectName, settings, logger, Resources.ERROR_MustUseProjectName);
            success = success & !ContainsNamedParameter(SonarProperties.ProjectVersion, settings, logger, Resources.ERROR_MustUseProjectVersion);

            // Check for others settings that can't be set
            success = success & !ContainsUnsettableParameter(SonarProperties.ProjectBaseDir, settings, logger);
            success = success & !ContainsUnsettableParameter(SonarProperties.WorkingDirectory, settings, logger);

            analysisSettings = settings;
            return success;
        }

        private static AnalysisSetting TryGetAnalysisSetting(string id, IEnumerable<AnalysisSetting> settings)
        {
            return settings.FirstOrDefault(s => AnalysisSetting.SettingKeyComparer.Equals(id, s.Id));
        }

        private static bool ContainsNamedParameter(string settingName, IEnumerable<AnalysisSetting> settings, ILogger logger, string errorMessage)
        {
            if (settings.Any(s => AnalysisSetting.SettingKeyComparer.Equals(settingName, s.Id)))
            {
                logger.LogError(errorMessage);
                return true;
            }
            return false;
        }

        private static bool ContainsUnsettableParameter(string settingName, IEnumerable<AnalysisSetting> settings, ILogger logger)
        {
            if (settings.Any(s => AnalysisSetting.SettingKeyComparer.Equals(settingName, s.Id)))
            {
                logger.LogError(Resources.ERROR_CannotSetPropertyOnCommandLine, settingName);
                return true;
            }
            return false;
        }

        #endregion

    }
}
