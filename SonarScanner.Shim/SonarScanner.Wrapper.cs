//-----------------------------------------------------------------------
// <copyright file="SonarScannerWrapper.cs" company="SonarSource SA and Microsoft Corporation">
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

namespace SonarScanner.Shim
{
    public class SonarScannerWrapper : ISonarScanner
    {
        /// <summary>
        /// Env variable that controls the amount of memory the JVM can use for the sonar-scanner.
        /// </summary>
        /// <remarks>Large projects error out with OutOfMemoryException if not set</remarks>
        private const string SonarScannerOptsVariableName = "SONAR_SCANNER_OPTS";

        /// <summary>
        /// Env variable that locates the sonar-scanner
        /// </summary>
        /// <remarks>Existing values set by the user might cause failures/remarks>
        public const string SonarScannerHomeVariableName = "SONAR_SCANNER_HOME";

        /// <summary>
        /// Name of the command line argument used to specify the generated project settings file to use
        /// </summary>
        public const string ProjectSettingsFileArgName = "project.settings";

        /// <summary>
        /// Additional arguments that will always be passed to the scanner
        /// </summary>
        public const string StandardAdditionalScannerArguments = "-e"; // -e = produce execution errors to assist with troubleshooting

        /// <summary>
        /// Default value for the SONAR_SCANNER_OPTS
        /// </summary>
        /// <remarks>Reserving more than is available on the agent will cause the sonar-scanner to fail</remarks>
        private const string SonarScannerOptsDefaultValue = "-Xmx1024m";

        private const string CmdLineArgPrefix = "-D";

        private const string SonarScannerVersion = "2.8";

        #region ISonarScanner interface

        public ProjectInfoAnalysisResult Execute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (userCmdLineArguments == null)
            {
                throw new ArgumentNullException("userCmdLineArguments");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);
            Debug.Assert(result != null, "Not expecting the file generator to return null");
            result.RanToCompletion = false;

            SonarProjectPropertiesValidator.Validate(
                config.SonarScannerWorkingDirectory,
                result.Projects,
                onValid: () =>
                {
                    InternalExecute(config, userCmdLineArguments, logger, result);
                },
                onInvalid: (invalidFolders) =>
                {
                    // LOG error message
                    logger.LogError(Resources.ERR_ConflictingSonarProjectProperties, string.Join(", ", invalidFolders));
                });

            return result;
        }

        #endregion ISonarScanner interface

        #region Private methods

        private static void InternalExecute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger, ProjectInfoAnalysisResult result)
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
                string exeFileName = FindScannerExe(config, logger);
                if (exeFileName != null)
                {
                    result.RanToCompletion = ExecuteJavaRunner(config, userCmdLineArguments, logger, exeFileName, result.FullPropertiesFilePath);
                }
            }
        }

        private static string FindScannerExe(AnalysisConfig config, ILogger logger)
        {
            string fullPath = null;

            var binFolder = config.SonarBinDir;
            var sonarScannerZip = Path.Combine(binFolder, "sonar-scanner-" + SonarScannerVersion + ".zip");
            var sonarScannerDestinationFolder = Path.Combine(binFolder, "sonar-scanner");

            if (Utilities.TryEnsureEmptyDirectories(logger, sonarScannerDestinationFolder))
            {
                ZipFile.ExtractToDirectory(sonarScannerZip, sonarScannerDestinationFolder);
                fullPath = Path.Combine(sonarScannerDestinationFolder, @"bin\sonar-scanner.bat");
            }

            return fullPath;
        }

        public /* for test purposes */ static bool ExecuteJavaRunner(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger, string exeFileName, string propertiesFileName)
        {
            Debug.Assert(File.Exists(exeFileName), "The specified exe file does not exist: " + exeFileName);
            Debug.Assert(File.Exists(propertiesFileName), "The specified properties file does not exist: " + propertiesFileName);

            IgnoreSonarScannerHome(logger);

            IEnumerable<string> allCmdLineArgs = GetAllCmdLineArgs(propertiesFileName, userCmdLineArguments, config);

            IDictionary<string, string> envVarsDictionary = GetAdditionalEnvVariables(logger);
            Debug.Assert(envVarsDictionary != null);

            logger.LogInfo(Resources.MSG_SonarScannerCalling);

            Debug.Assert(!String.IsNullOrWhiteSpace(config.SonarScannerWorkingDirectory), "The working dir should have been set in the analysis config");
            Debug.Assert(Directory.Exists(config.SonarScannerWorkingDirectory), "The working dir should exist");

            ProcessRunnerArguments scannerArgs = new ProcessRunnerArguments(exeFileName, true, logger)
            {
                CmdLineArgs = allCmdLineArgs,
                WorkingDirectory = config.SonarScannerWorkingDirectory,
                EnvironmentVariables = envVarsDictionary
            };

            ProcessRunner runner = new ProcessRunner();

            // SONARMSBRU-202 Note that the Sonar Scanner may write warnings to stderr so
            // we should only rely on the exit code when deciding if it ran successfully
            bool success = runner.Execute(scannerArgs);

            if (success)
            {
                logger.LogInfo(Resources.MSG_SonarScannerCompleted);
            }
            else
            {
                logger.LogError(Resources.ERR_SonarScannerExecutionFailed);
            }
            return success;
        }

        private static void IgnoreSonarScannerHome(ILogger logger)
        {
            if (!String.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(SonarScannerHomeVariableName)))
            {
                logger.LogInfo(Resources.MSG_SonarScannerHomeIsSet);
                Environment.SetEnvironmentVariable(SonarScannerHomeVariableName, String.Empty);
            }
        }

        /// <summary>
        /// Returns any additional environment variables that need to be passed to
        /// the sonar-scanner
        /// </summary>
        private static IDictionary<string, string> GetAdditionalEnvVariables(ILogger logger)
        {
            IDictionary<string, string> envVarsDictionary = new Dictionary<string, string>();

            // Always set a value for SONAR_SCANNER_OPTS just in case it is set at process-level
            // which wouldn't be inherited by the child sonar-scanner process.
            string sonarScannerOptsValue = GetSonarScannerOptsValue(logger);
            envVarsDictionary.Add(SonarScannerOptsVariableName, sonarScannerOptsValue);

            return envVarsDictionary;
        }

        /// <summary>
        /// Get the value of the SONAR_SCANNER_OPTS variable that controls the amount of memory available to the JDK so that the sonar-scanner doesn't
        /// hit OutOfMemory exceptions. If no env variable with this name is defined then a default value is used.
        /// </summary>
        private static string GetSonarScannerOptsValue(ILogger logger)
        {
            string existingValue = Environment.GetEnvironmentVariable(SonarScannerOptsVariableName);

            if (!String.IsNullOrWhiteSpace(existingValue))
            {
                logger.LogInfo(Resources.MSG_SonarScannerOptsAlreadySet, SonarScannerOptsVariableName, existingValue);
                return existingValue;
            }
            else
            {
                logger.LogInfo(Resources.MSG_SonarScannerOptsDefaultUsed, SonarScannerOptsVariableName, SonarScannerOptsDefaultValue);
                return SonarScannerOptsDefaultValue;
            }
        }

        /// <summary>
        /// Returns all of the command line arguments to pass to sonar-scanner
        /// </summary>
        private static IEnumerable<string> GetAllCmdLineArgs(string projectSettingsFilePath, IEnumerable<string> userCmdLineArguments, AnalysisConfig config)
        {
            // We don't know what all of the valid command line arguments are so we'll
            // just pass them on for the sonar-scanner to validate.
            List<string> args = new List<string>(userCmdLineArguments);

            // Add any sensitive arguments supplied in the config should be passed on the command line
            args.AddRange(GetSensitiveFileSettings(config, userCmdLineArguments));

            // Add the project settings file and the standard options.
            // Experimentation suggests that the sonar-scanner won't error if duplicate arguments
            // are supplied - it will just use the last argument.
            // So we'll set our additional properties last to make sure they take precedence.
            args.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}{1}={2}", CmdLineArgPrefix, ProjectSettingsFileArgName, projectSettingsFilePath));
            args.Add(StandardAdditionalScannerArguments);

            return args;
        }

        private static IEnumerable<string> GetSensitiveFileSettings(AnalysisConfig config, IEnumerable<string> userCmdLineArguments)
        {
            IEnumerable<Property> allPropertiesFromConfig = config.GetAnalysisSettings(false).GetAllProperties();

            return allPropertiesFromConfig.Where(p => p.ContainsSensitiveData() && !UserSettingExists(p, userCmdLineArguments))
                .Select(p => p.AsSonarScannerArg());
        }

        private static bool UserSettingExists(Property fileProperty, IEnumerable<string> userArgs)
        {
            return userArgs.Any(userArg => userArg.IndexOf(CmdLineArgPrefix + fileProperty.Id, StringComparison.Ordinal) == 0);
        }

        #endregion Private methods
    }
}