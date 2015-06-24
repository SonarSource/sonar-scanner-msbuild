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
            
            Descriptors.Add(new ArgumentDescriptor(
                id: KeywordIds.RunnerPropertiesPath, prefixes: new string[] { "/runnerProperties:", "/r:" }, required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_PropertiesPath));

            Descriptors.Add(new ArgumentDescriptor(
                id: KeywordIds.DynamicSetting, prefixes: new string[] { "/p:" }, required: false, allowMultiple: true, description: Resources.CmdLine_ArgDescription_DynamicSetting));

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

            // This call will fail if there are duplicate, missing, or unrecognised arguments
            CommandLineParser parser = new CommandLineParser(Descriptors);
            bool parsedOk = parser.ParseArguments(commandLineArgs, logger, out arguments);
            
            // The /p:[key]=[value] settings required further processing
            IEnumerable<AnalysisSetting> settings;
            bool analysisSettingsOk = TryGetAnalysisSettings(arguments, logger, out settings);

            if (parsedOk && analysisSettingsOk)
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

        private static ArgumentInstance TryGetArgument(string id, IEnumerable<ArgumentInstance> arguments)
        {
            return arguments.FirstOrDefault(a => a.Descriptor.Id == id);
        }

        private static string TryGetArgumentValue(string id, IEnumerable<ArgumentInstance> arguments)
        {
            ArgumentInstance argument = arguments.SingleOrDefault(a => a.Descriptor.Id == id);
            return argument == null ? null : argument.Value;
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
                        logger.LogError(Resources.ERROR_CmdLine_DuplicateSetting, argument.Value, existing.Value);
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