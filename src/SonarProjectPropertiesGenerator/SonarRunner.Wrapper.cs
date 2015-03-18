//-----------------------------------------------------------------------
// <copyright file="SonarRunnerWrapper.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using SonarProjectPropertiesGenerator;
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

        private const string SonarRunnerFileName = "sonar-runner.bat";

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

			bool ranToCompletion = ExecuteJavaRunner(config, logger, propertiesFileName);
            if (ranToCompletion)
            {
                // TODO: Report the results
            }
            else
            {
                logger.LogError("Sonar runner did not run to completion");
            }
            return ranToCompletion;
        }

        #endregion

        #region Private methods

		private static string GenerateProjectProperties(AnalysisConfig config, ILogger logger)
        {
            var projects = ProjectLoader.LoadFrom(config.SonarOutputDir);
            var contents = PropertiesWriter.ToString(new ConsoleLogger(includeTimestamp: true), config.SonarProjectKey, config.SonarProjectName, config.SonarProjectVersion, config.SonarOutputDir, projects);

            string fullName = Path.Combine(config.SonarOutputDir, ProjectPropertiesFileName);
            File.WriteAllText(fullName, contents, Encoding.ASCII);
            return fullName;
        }

        private static bool ExecuteJavaRunner(AnalysisConfig config, ILogger logger, string propertiesFileName)
        {
            string args = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "-Dproject.settings=\"{0}\"", propertiesFileName);

            // TODO: need a reliable way to set the working directory
            string workingDir = Path.GetDirectoryName(config.SonarRunnerPropertiesPath);
            workingDir = Path.Combine(workingDir, "..\\bin");
            workingDir = Path.GetFullPath(workingDir);

            bool success = Execute(SonarRunnerFileName, args, workingDir, logger);
            return success;
        }

        private static bool Execute(string fileName, string args, string workingDirectory, ILogger logger)
        {
            Debug.Assert(File.Exists(fileName), "The specified file does not exist");

            logger.LogMessage("Shelling out to the sonar-runner");
            logger.LogMessage("Current directory: {0}", Directory.GetCurrentDirectory());

            ProcessRunner runner = new ProcessRunner();
            bool success = runner.Execute(fileName, args, workingDirectory, logger);
            success = success && !runner.ErrorsLogger;

            if (success)
            {
                logger.LogMessage("Sonar runner has finished");
            }
			else
            {
				// TODO: should be kill the process or leave it? Could we corrupt the data on the server if we kill the process?
                logger.LogMessage("Timed-out waiting for the sonar runner to complete");
            }
            return success;
        }

        #endregion

        private class ProcessRunner
        {
            private ILogger logger;

            public bool ErrorsLogger { get; private set; }

            public bool Execute(string fileName, string args, string workingDirectory, ILogger logger)
            {
                this.logger = logger;

                logger.LogMessage("  File name: {0}", fileName);
                logger.LogMessage("  Arguments: {0}", args);
                logger.LogMessage("  Working directory: {0}", workingDirectory);

                ProcessStartInfo psi = new ProcessStartInfo()
                {
                    FileName = fileName,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false, // required if we want to capture the error output
                    ErrorDialog = false,
                    Arguments = args,
                    WorkingDirectory = workingDirectory
                };

                Process p = new Process();
                p.StartInfo = psi;
                p.ErrorDataReceived += p_ErrorDataReceived;
                p.OutputDataReceived += p_OutputDataReceived;

                bool succeeded;
                try
                {
                    p.Start();
                    p.BeginErrorReadLine();
                    p.BeginOutputReadLine();
                    succeeded = p.WaitForExit(SonarRunnerTimeoutInMs);
                }
                finally
                {
                    p.ErrorDataReceived -= p_ErrorDataReceived;
                    p.OutputDataReceived -= p_OutputDataReceived;
                }
                return succeeded;
            }

            void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    this.logger.LogMessage(e.Data);
                }
            }

            void p_ErrorDataReceived(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    this.ErrorsLogger = true;
                    this.logger.LogError(e.Data);
                }
            }
        }
    }
}
