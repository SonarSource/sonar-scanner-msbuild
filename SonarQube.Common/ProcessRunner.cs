//-----------------------------------------------------------------------
// <copyright file="ProcessRunner.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace SonarQube.Common
{
    /// <summary>
    /// Helper class to run an executable and capture the output
    /// </summary>
    public sealed class ProcessRunner
    {
        private ILogger outputLogger;
        
        #region Public methods

        public bool ErrorsLogged { get; private set; }

        public int ExitCode { get; private set; }

        /// <summary>
        /// Runs the specified executable with timeout
        /// </summary>
        /// <returns>True if the process exited successfully, otherwise false</returns>
        public bool Execute(string exeName, string args, string workingDirectory, int timeoutInMilliseconds, ILogger logger)
        {
            return Execute(exeName, args, workingDirectory, Timeout.Infinite, null, logger);
        }

        /// <summary>
        /// Runs the specified executable without timeout 
        /// </summary>
        /// <returns>True if the process exited successfully, otherwise false</returns>
        public bool Execute(string exeName, string args, string workingDirectory, ILogger logger)
        {
            return Execute(exeName, args, workingDirectory, Timeout.Infinite, null, logger);
        }

        /// <summary>
        /// Runs the specified executable and passes per-process env variables
        /// </summary>
        /// <param name="envVariables">Names and values of process env variables to be passed to the new process</param>
        /// <returns>True if the process exited successfully, otherwise false</returns>
        public bool Execute(string exeName, string args, string workingDirectory, IDictionary<string, string> envVariables, ILogger logger)
        {
            return Execute(exeName, args, workingDirectory, Timeout.Infinite, envVariables, logger);
        }

        /// <summary>
        /// Runs the specified executable and returns a boolean indicating success or failure
        /// </summary>
        /// <param name="exeName">Name of the file to execute. This can be a full name or just the file name (if the file is on the %PATH%).</param>
        /// <param name="envVariables">Names and values of process env variables to be passed to the new process</param>
        /// <remarks>The standard and error output will be streamed to the logger</remarks>
        public bool Execute(string exeName, string args, string workingDirectory, int timeoutInMilliseconds, IDictionary<string, string> envVariables, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(exeName))
            {
                throw new ArgumentNullException("exeName");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            
            this.outputLogger = logger;

            ProcessStartInfo psi = new ProcessStartInfo()
            {
                FileName = exeName,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false, // required if we want to capture the error output
                ErrorDialog = false,
                CreateNoWindow = true,
                Arguments = args,
                WorkingDirectory = workingDirectory
            };

            if (envVariables != null)
            {
                foreach (KeyValuePair<string, string> envVariable in envVariables)
                {
                    if (!String.IsNullOrEmpty(envVariable.Value))
                    {
                        psi.EnvironmentVariables.Add(envVariable.Key, envVariable.Value);
                    }
                }
            }

            bool succeeded;
            Process process = null;
            try
            {
                process = new Process();

                process.StartInfo = psi;
                process.ErrorDataReceived += OnErrorDataReceived;
                process.OutputDataReceived += OnOutputDataReceived;

                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                logger.LogMessage(Resources.DIAG_ExecutingFile, exeName, args, workingDirectory, timeoutInMilliseconds, process.Id);

                succeeded = process.WaitForExit(timeoutInMilliseconds);
                if (succeeded)
                {
                    process.WaitForExit(); // Give any asynchronous events the chance to complete
                }

                // false means we asked the process to stop but it didn't.
                // true: we might still have timed out, but the process ended when we asked it to
                if (succeeded) 
                {
                    logger.LogMessage(Resources.DIAG_ExecutionExitCode, process.ExitCode);
                    this.ExitCode = process.ExitCode;
                }
                else
                {
                    logger.LogWarning(Resources.DIAG_ExecutionTimedOut, timeoutInMilliseconds, exeName);
                }

                succeeded = succeeded && (process.ExitCode == 0);
            }
            finally
            {
                process.ErrorDataReceived -= OnErrorDataReceived;
                process.OutputDataReceived -= OnOutputDataReceived;
                
                process.Dispose();
            }
            return succeeded;
        }

        #endregion

        #region Private methods

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                this.outputLogger.LogMessage(e.Data);
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                this.ErrorsLogged = true;
                this.outputLogger.LogError(e.Data);
            }
        }

        #endregion
    }
}
