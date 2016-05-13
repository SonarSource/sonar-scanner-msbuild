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
using System.Text;
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

        /// <summary>
        /// Strings that are used to indicate arguments that contain
        /// sensitive data that should not be logged
        /// </summary>
        public static readonly string[] SensitivePropertyKeys = new string[] {
            SonarProperties.SonarPassword,
            SonarProperties.SonarUserName,
            SonarProperties.DbPassword,
            SonarProperties.DbUserName
        };


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

        public string GetEscapedArguments()
        {
            if (this.CmdLineArgs == null) { return null; }

            return string.Join(" ", this.CmdLineArgs.Select(a => EscapeArgument(a)));
        }

        /// <summary>
        /// Returns the string that should be used when logging command line arguments
        /// (sensitive data will have been removed)
        /// </summary>
        public string AsLogText()
        {
            if (this.CmdLineArgs == null) { return null; }

            bool hasSensitiveData = false;

            StringBuilder sb = new StringBuilder();

            foreach (string arg in this.CmdLineArgs)
            {
                if (ContainsSensitiveData(arg))
                {
                    hasSensitiveData = true;
                }
                else
                {
                    sb.Append(arg);
                    sb.Append(" ");
                }
            }

            if (hasSensitiveData)
            {
                sb.Append(Resources.MSG_CmdLine_SensitiveCmdLineArgsAlternativeText);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Determines whether the text contains sensitive data that
        /// should not be logged/written to file
        /// </summary>
        public static bool ContainsSensitiveData(string text)
        {
            Debug.Assert(SensitivePropertyKeys != null, "SensitiveDataMarkers array should not be null");

            if (text == null)
            {
                return false;
            }

            return SensitivePropertyKeys.Any(marker => text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) > -1);
        }

        /// <summary>
        /// The CreateProcess Win32 API call only takes 1 string for all arguments.
        /// Ultimately, it is the responsibility of each program to decide how to split this string into multiple arguments.
        ///
        /// See:
        /// https://blogs.msdn.microsoft.com/oldnewthing/20100917-00/?p=12833/
        /// https://blogs.msdn.microsoft.com/twistylittlepassagesallalike/2011/04/23/everyone-quotes-command-line-arguments-the-wrong-way/
        /// http://www.daviddeley.com/autohotkey/parameters/parameters.htm
        /// </summary>
        private static string EscapeArgument(string arg)
        {
            Debug.Assert(arg != null, "Not expecting an argument to be null");

            var sb = new StringBuilder();

            sb.Append("\"");
            for (int i = 0; i < arg.Length; i++)
            {
                int numberOfBackslashes = 0;
                for (; i < arg.Length && arg[i] == '\\'; i++)
                {
                    numberOfBackslashes++;
                }

                if (i == arg.Length)
                {
                    //
                    // Escape all backslashes, but let the terminating
                    // double quotation mark we add below be interpreted
                    // as a metacharacter.
                    //
                    sb.Append('\\', numberOfBackslashes * 2);
                }
                else if (arg[i] == '"')
                {
                    //
                    // Escape all backslashes and the following
                    // double quotation mark.
                    //
                    sb.Append('\\', numberOfBackslashes * 2 + 1);
                    sb.Append(arg[i]);
                }
                else
                {
                    //
                    // Backslashes aren't special here.
                    //
                    sb.Append('\\', numberOfBackslashes);
                    sb.Append(arg[i]);
                }
            }
            sb.Append("\"");

            return sb.ToString();
        }

        #endregion

    }
}
