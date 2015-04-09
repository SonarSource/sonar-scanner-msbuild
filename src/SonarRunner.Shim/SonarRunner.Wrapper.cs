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
using System.Linq;
using System.Text;

namespace SonarRunner.Shim
{
    public class SonarRunnerWrapper : ISonarRunner
    {
        private const int SonarRunnerTimeoutInMs = System.Threading.Timeout.Infinite;

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
                string exeFileName = FindRunnerExe(logger);
                if (exeFileName != null)
                {
                    result.RanToCompletion = ExecuteJavaRunner(config, logger, exeFileName, result.FullPropertiesFilePath);
                }
            }

            return result;
        }

        #endregion

        #region Private methods

        private static string FindRunnerExe(ILogger logger)
        {
            string exeFileName = FileLocator.FindDefaultSonarRunnerExecutable();
            if (exeFileName == null)
            {
                logger.LogError(Resources.ERR_FailedToLocateSonarRunner, FileLocator.SonarRunnerFileName);
            }
            else
            {
                logger.LogMessage(Resources.DIAG_LocatedSonarRunner, exeFileName);
            }
            return exeFileName;
        }

        private static bool ExecuteJavaRunner(AnalysisConfig config, ILogger logger, string exeFileName, string propertiesFileName)
        {
            Debug.Assert(File.Exists(exeFileName), "The specified exe file does not exist: " + exeFileName);
            Debug.Assert(File.Exists(propertiesFileName), "The specified properties file does not exist: " + propertiesFileName);

            string args = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "-Dproject.settings=\"{0}\"", propertiesFileName);
            
            logger.LogMessage(Resources.DIAG_CallingSonarRunner);

            ProcessRunner runner = new ProcessRunner();
            bool success = runner.Execute(exeFileName, args, Path.GetDirectoryName(exeFileName), SonarRunnerTimeoutInMs, logger);
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

        #endregion
    }
}
