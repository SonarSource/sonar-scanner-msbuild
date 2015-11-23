//-----------------------------------------------------------------------
// <copyright file="SonarRunnerWrapper.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace SonarRunner.Shim
{
    public class SonarRunnerWrapper : ISonarRunner
    {
        /// <summary>
        /// Env variable that controls the amount of memory the JVM can use for the sonar-runner. 
        /// </summary>
        /// <remarks>Large projects error out with OutOfMemoryException if not set</remarks>
        private const string SonarRunnerOptsVariableName = "SONAR_RUNNER_OPTS";

        /// <summary>
        /// Env variable that locates the sonar-runner
        /// </summary>
        /// <remarks>We asked users to set this in the 0.9 version so that the sonar-runner is discoverable but 
        /// in 1.0+ this is not needed and setting it can cause the sonar-runner to fail</remarks>
        public const string SonarRunnerHomeVariableName = "SONAR_RUNNER_HOME";

        /// <summary>
        /// Name of the command line argument used to specify the generated project settings file to use
        /// </summary>
        public const string ProjectSettingsFileArgName = "project.settings";

        /// <summary>
        /// Additional arguments that will always be passed to the runner
        /// </summary>
        public const string StandardAdditionalRunnerArguments = "-e"; // -e = produce execution errors to assist with troubleshooting

        /// <summary>
        /// Default value for the SONAR_RUNNER_OPTS
        /// </summary>
        /// <remarks>Reserving more than is available on the agent will cause the sonar-runner to fail</remarks>
        private const string SonarRunnerOptsDefaultValue = "-Xmx1024m";

        private const string CmdLineArgPrefix = "-D";

        #region ISonarRunner interface

        public ProjectInfoAnalysisResult Execute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (userCmdLineArguments == null)
            {
                throw new ArgumentNullException("cmdLineArguments");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);
            Debug.Assert(result != null, "Not expecting the file generator to return null");
            result.RanToCompletion = false;

            SonarProjectPropertiesValidator.Validate(
                config.SonarRunnerWorkingDirectory, result.Projects,
                onValid: () =>
                {
                    ProjectInfoReportBuilder.WriteSummaryReport(config, result, logger);

                    if (result.FullPropertiesFilePath == null)
                    {
                        // We expect a detailed error message to have been logged explaining
                        // why the properties file generation could not be performed
                        logger.LogInfo(Resources.MSG_PropertiesGenerationFailed);
                    }
                    else
                    {
                        string exeFileName = FindRunnerExe(config, logger);
                        if (exeFileName != null)
                        {
                            result.RanToCompletion = ExecuteJavaRunner(config, userCmdLineArguments, logger, exeFileName, result.FullPropertiesFilePath);
                        }
                    }
                },
                onInvalid: (invalidFolders) =>
                {
                    // LOG error message
                    logger.LogError(Resources.ERR_ConflictingSonarProjectProperties, string.Join(", ", invalidFolders));
                });

            return result;
        }

        #endregion ISonarRunner interface

        #region Private methods

        private static string FindRunnerExe(AnalysisConfig config, ILogger logger)
        {
            string fullPath = null;

            var binFolder = config.SonarBinDir;
            var sonarRunnerZip = Path.Combine(binFolder, "sonar-runner.zip");
            var sonarRunnerDestinationFolder = Path.Combine(binFolder, "sonar-runner");

            if (Utilities.TryEnsureEmptyDirectories(logger, sonarRunnerDestinationFolder))
            {
                ZipFile.ExtractToDirectory(sonarRunnerZip, sonarRunnerDestinationFolder);
                fullPath = Path.Combine(sonarRunnerDestinationFolder, @"bin\sonar-runner.bat");
            }

            return fullPath;
        }

        public /* for test purposes */ static bool ExecuteJavaRunner(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger, string exeFileName, string propertiesFileName)
        {
            Debug.Assert(File.Exists(exeFileName), "The specified exe file does not exist: " + exeFileName);
            Debug.Assert(File.Exists(propertiesFileName), "The specified properties file does not exist: " + propertiesFileName);

            IgnoreSonarRunnerHome(logger);

            IEnumerable<string> allCmdLineArgs = GetAllCmdLineArgs(propertiesFileName, userCmdLineArguments, config);

            IDictionary<string, string> envVarsDictionary = GetAdditionalEnvVariables(logger);
            Debug.Assert(envVarsDictionary != null);

            logger.LogInfo(Resources.MSG_CallingSonarRunner);

            Debug.Assert(!String.IsNullOrWhiteSpace(config.SonarRunnerWorkingDirectory), "The working dir should have been set in the analysis config");
            Debug.Assert(Directory.Exists(config.SonarRunnerWorkingDirectory), "The working dir should exist");

            ProcessRunnerArguments runnerArgs = new ProcessRunnerArguments(exeFileName, logger)
            {
                CmdLineArgs = allCmdLineArgs,
                WorkingDirectory = config.SonarRunnerWorkingDirectory, 
                EnvironmentVariables = envVarsDictionary
            };
            ProcessRunner runner = new ProcessRunner();

            bool success = runner.Execute(runnerArgs);

            success = success && !runner.ErrorsLogged;

            if (success)
            {
                logger.LogInfo(Resources.MSG_SonarRunnerCompleted);
            }
            else
            {
                logger.LogError(Resources.ERR_SonarRunnerExecutionFailed);
            }
            return success;
        }

        private static void IgnoreSonarRunnerHome(ILogger logger)
        {
            if (!String.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(SonarRunnerHomeVariableName)))
            {
                logger.LogInfo(Resources.MSG_SonarRunnerHomeIsSet);
                Environment.SetEnvironmentVariable(SonarRunnerHomeVariableName, String.Empty);
            }
        }

        /// <summary>
        /// Returns any additional environment variables that need to be passed to
        /// the sonar-runner
        /// </summary>
        private static IDictionary<string, string> GetAdditionalEnvVariables(ILogger logger)
        {
            IDictionary<string, string> envVarsDictionary = new Dictionary<string, string>();

            // Always set a value for SONAR_RUNNER_OPTS just in case it is set at process-level
            // which wouldn't be inherited by the child sonar-runner process.
            string sonarRunnerOptsValue = GetSonarRunnerOptsValue(logger);
            envVarsDictionary.Add(SonarRunnerOptsVariableName, sonarRunnerOptsValue);

            return envVarsDictionary;
        }

        /// <summary>
        /// Get the value of the SONAR_RUNNER_OPTS variable that controls the amount of memory available to the JDK so that the sonar-runner doesn't
        /// hit OutOfMemory exceptions. If no env variable with this name is defined then a default value is used.
        /// </summary>
        private static string GetSonarRunnerOptsValue(ILogger logger)
        {
            string existingValue = Environment.GetEnvironmentVariable(SonarRunnerOptsVariableName);

            if (!String.IsNullOrWhiteSpace(existingValue))
            {
                logger.LogInfo(Resources.MSG_SonarRunOptsAlreadySet, SonarRunnerOptsVariableName, existingValue);
                return existingValue;
            }
            else
            {
                logger.LogInfo(Resources.MSG_SonarRunnerOptsDefaultUsed, SonarRunnerOptsVariableName, SonarRunnerOptsDefaultValue);
                return SonarRunnerOptsDefaultValue;
            }
        }

        /// <summary>
        /// Returns all of the command line arguments to pass to sonar-runner
        /// </summary>
        private static IEnumerable<string> GetAllCmdLineArgs(string projectSettingsFilePath, IEnumerable<string> userCmdLineArguments, AnalysisConfig config)
        {
            // We don't know what all of the valid command line arguments are so we'll
            // just pass them on for the sonar-runner to validate.
            List<string> args = new List<string>(userCmdLineArguments);

            // Add any sensitive arguments supplied in the config should be passed on the command line
            args.AddRange(GetSensitiveFileSettings(config, userCmdLineArguments));

            // Add the project settings file and the standard options.
            // Experimentation suggests that the sonar-runner won't error if duplicate arguments
            // are supplied - it will just use the last argument.
            // So we'll set our additional properties last to make sure they take precedence.
            args.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}{1}={2}", CmdLineArgPrefix, ProjectSettingsFileArgName, projectSettingsFilePath));
            args.Add(StandardAdditionalRunnerArguments);

            return args;
        }

        private static IEnumerable<string> GetSensitiveFileSettings(AnalysisConfig config, IEnumerable<string> userCmdLineArguments)
        {
            IEnumerable<Property> allPropertiesFromConfig = config.GetAnalysisSettings(false).GetAllProperties();

            return allPropertiesFromConfig.Where(p => p.ContainsSensitiveData() && !UserSettingExists(p, userCmdLineArguments))
                .Select(p => p.AsSonarRunnerArg());
        }

        private static bool UserSettingExists(Property fileProperty, IEnumerable<string> userArgs)
        {
            return userArgs.Any(userArg => userArg.IndexOf(CmdLineArgPrefix + fileProperty.Id, StringComparison.Ordinal) == 0);
        }

        #endregion Private methods
    }
}