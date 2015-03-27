//-----------------------------------------------------------------------
// <copyright file="SonarRunnerWrapper.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SonarRunner.Shim
{
    public class SonarRunnerWrapper : ISonarRunner
    {
        private const int SonarRunnerTimeoutInMs = 1000 * 60 * 30; // twenty minutes

        private const string ProjectPropertiesFileName = "sonar-project.properties";

        #region ISonarRunner interface

        public bool Execute(AnalysisConfig config, ILogger logger)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
        
            string propertiesFileName = GenerateProjectProperties(config, logger);
		
            if (propertiesFileName == null)
            {
                logger.LogError("Generation of the sonar-properties file failed. Unable to complete SonarQube analysis.");
                return false;
            }

            string exeFileName = FindRunnerExe(logger);

			bool ranToCompletion = ExecuteJavaRunner(config, logger, exeFileName, propertiesFileName);
            if (ranToCompletion)
            {
                // TODO: Report the results
            }
            return ranToCompletion;
        }

        #endregion

        #region Private methods

		private static string GenerateProjectProperties(AnalysisConfig config, ILogger logger)
        {
            string fullName = Path.Combine(config.SonarOutputDir, ProjectPropertiesFileName);
            logger.LogMessage(Resources.DIAG_GeneratingProjectProperties, fullName);
            
            var projects = ProjectLoader.LoadFrom(config.SonarOutputDir);
            var contents = PropertiesWriter.ToString(new ConsoleLogger(includeTimestamp: true), config.SonarProjectKey, config.SonarProjectName, config.SonarProjectVersion, config.SonarOutputDir, projects);

            File.WriteAllText(fullName, contents, Encoding.ASCII);
            return fullName;
        }

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
