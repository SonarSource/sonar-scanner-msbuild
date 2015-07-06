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
using System.Threading;

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
        /// Default value for the SONAR_RUNNER_OPTS
        /// </summary>
        /// <remarks>Reserving more than is available on the agent will cause the sonar-runner to fail</remarks>
        private const string SonarRunnerOptsDefaultValue = "-Xmx1024m";

        #region ISonarRunner interface

        public ProjectInfoAnalysisResult Execute(AnalysisConfig config, ILogger logger)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            ProjectInfoAnalysisResult result = PropertiesFileGenerator.GenerateFile(config, logger);
            Debug.Assert(result != null, "Not expecting the file generator to return null");

            ProjectInfoReportBuilder.WriteSummaryReport(config, result, logger);

            result.RanToCompletion = false;

            if (result.FullPropertiesFilePath == null)
            {
                // We expect a detailed error message to have been logged explaining
                // why the properties file generation could not be performed
                logger.LogMessage(Resources.DIAG_PropertiesGenerationFailed);
            }
            else
            {
                string exeFileName = FindRunnerExe(config, logger);
                if (exeFileName != null)
                {
                    result.RanToCompletion = ExecuteJavaRunner(logger, exeFileName, result.FullPropertiesFilePath);
                }
            }

            return result;
        }

        #endregion ISonarRunner interface

        #region Private methods

        private static string FindRunnerExe(AnalysisConfig config, ILogger logger)
        {
            var binFolder = config.SonarBinDir;

            var sonarRunnerZip = Path.Combine(binFolder, "sonar-runner.zip");
            var sonarRunnerDestinationFolder = Path.Combine(binFolder, "sonar-runner");
            Utilities.EnsureEmptyDirectory(sonarRunnerDestinationFolder, logger);
            ZipFile.ExtractToDirectory(sonarRunnerZip, sonarRunnerDestinationFolder);

            return Path.Combine(sonarRunnerDestinationFolder, @"bin\sonar-runner.bat");
        }

        public /* for test purposes */ static bool ExecuteJavaRunner(ILogger logger, string exeFileName, string propertiesFileName)
        {
            Debug.Assert(File.Exists(exeFileName), "The specified exe file does not exist: " + exeFileName);
            Debug.Assert(File.Exists(propertiesFileName), "The specified properties file does not exist: " + propertiesFileName);

            WarnAgainstSettingSonarRunnerHome(logger);

            string args = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "-Dproject.settings=\"{0}\"", propertiesFileName);

            logger.LogMessage(Resources.DIAG_CallingSonarRunner);

            IDictionary<string, string> envVarsDictionary = GetAdditionalEnvVariables(logger);
            Debug.Assert(envVarsDictionary != null);

            ProcessRunner runner = new ProcessRunner();
            bool success = runner.Execute(
                exeFileName,
                args,
                Path.GetDirectoryName(exeFileName),
                Timeout.Infinite,
                envVarsDictionary,
                logger);

            success = success && !runner.ErrorsLogged;

            if (success)
            {
                logger.LogMessage(Resources.DIAG_SonarRunnerCompleted);
            }
            else
            {
                logger.LogError(Resources.ERR_SonarRunnerExecutionFailed);
            }
            return success;
        }

        private static void WarnAgainstSettingSonarRunnerHome(ILogger logger)
        {
            if (!String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SonarRunnerHomeVariableName)))
            {
                logger.LogWarning(Resources.WARN_SonarRunnerHomeIsSet);
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
                logger.LogMessage(Resources.INFO_SonarRunOptsAlreadySet, SonarRunnerOptsVariableName, existingValue);
                return existingValue;
            }
            else
            {
                logger.LogMessage(Resources.INFO_SonarRunnerOptsDefaultUsed, SonarRunnerOptsVariableName, SonarRunnerOptsDefaultValue);
                return SonarRunnerOptsDefaultValue;
            }
        }

        #endregion Private methods
    }
}