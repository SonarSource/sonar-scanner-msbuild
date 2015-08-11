//-----------------------------------------------------------------------
// <copyright file="ProcessRunnerArguments.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class containing parameters required to execute a new process
    /// </summary>
    public class ProcessRunnerArguments
    {
        private readonly string exeName;
        private readonly ILogger logger;

        public ProcessRunnerArguments(string exeName, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(exeName))
            {
                throw new ArgumentNullException("exeName");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            this.exeName = exeName;
            this.logger = logger;

            this.TimeoutInMilliseconds = Timeout.Infinite;
        }

        #region Public properties

        public string ExeName { get { return this.exeName; } }

        /// <summary>
        /// Non-sensitive command line arguments (i.e. ones that can safely be logged). Optional.
        /// </summary>
        public IEnumerable<string> CmdLineArgs { get; set; }

        public string WorkingDirectory { get; set; }

        public int TimeoutInMilliseconds { get; set; }

        /// <summary>
        /// Additional environments variables that should be set/overridden for the process. Can be null.
        /// </summary>
        public IDictionary<string, string> EnvironmentVariables { get; set; }

        public ILogger Logger { get { return this.logger; } }

        public string GetQuotedCommandLineArgs()
        {
            if (this.CmdLineArgs != null)
            {
                return string.Join(" ", this.CmdLineArgs.Select(a => GetQuotedArg(a)));
            }
            return null;
        }

        #endregion

        #region Private methods

        private static string GetQuotedArg(string arg)
        {
            Debug.Assert(arg != null, "Not expecting an argument to be null");

            string quotedArg = arg;

            // If an argument contains a quote then we assume it has been correctly quoted.
            // Otherwise, quote strings that contain spaces.
            if (quotedArg != null && arg.Contains(' ') && !arg.Contains('"'))
            {
                quotedArg = "\"" + arg + "\"";
            }

            return quotedArg;
        }

        #endregion

    }
}
