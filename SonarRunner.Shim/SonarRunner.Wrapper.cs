//-----------------------------------------------------------------------
// <copyright file="SonarRunnerWrapper.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace SonarRunner.Shim
{
    public class SonarRunnerWrapper : ISonarRunner
    {
        private const string SonarRunnerOptsVariableName = "SONAR_RUNNER_OPTS";

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

        #endregion

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

        private static bool ExecuteJavaRunner(ILogger logger, string exeFileName, string propertiesFileName)
        {
            Debug.Assert(File.Exists(exeFileName), "The specified exe file does not exist: " + exeFileName);
            Debug.Assert(File.Exists(propertiesFileName), "The specified properties file does not exist: " + propertiesFileName);

            string args = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "-Dproject.settings=\"{0}\"", propertiesFileName);

            logger.LogMessage(Resources.DIAG_CallingSonarRunner);

            string sonarRunnerOptsValue = GetSonarRunnerOptsValue(logger);

            ProcessRunner runner = new ProcessRunner();
            bool success = runner.Execute(
                exeFileName,
                args,
                Path.GetDirectoryName(exeFileName),
                Timeout.Infinite,
                !String.IsNullOrEmpty(sonarRunnerOptsValue) ? new Dictionary<string, string>() { { SonarRunnerOptsVariableName, sonarRunnerOptsValue } } : null,
                logger);

            success = success && !runner.ErrorsLogged;

            if (success)
            {
                logger.LogMessage(Resources.DIAG_SonarRunnerCompleted);
            }
            else
            {
                // TODO: should be kill the process or leave it? Could we corrupt the data on the server if we kill the process?
                logger.LogError(Resources.ERR_SonarRunnerExecutionFailed);
            }
            return success;
        }

        /// <summary>
        /// Get the value of the SONAR_RUNNER_OPTS variable that controls the amount of memory available to the JDK so that the sonar-runner doesn't 
        /// hit OutOfMemory exceptions. If no env variable with this name is defined at machine or user level then use the process one is used. 
        /// Bar that, a default value is used. 
        /// </summary>
        /// <returns></returns>
        private static string GetSonarRunnerOptsValue(ILogger logger)
        {
            string existingValue = Environment.GetEnvironmentVariable(SonarRunnerOptsVariableName);

            if (!String.IsNullOrWhiteSpace(existingValue))
            {
                return existingValue;
            }
            else
            {
                logger.LogMessage(Resources.INFO_SonarRunnerOptsDefaultUsed, SonarRunnerOptsVariableName, SonarRunnerOptsDefaultValue);
                return SonarRunnerOptsDefaultValue;
            }

        }

        #endregion
    }
}
