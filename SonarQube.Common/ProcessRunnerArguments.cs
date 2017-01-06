/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

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


        public ProcessRunnerArguments(string exeName, bool isBatchScript, ILogger logger)
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
            this.IsBatchScript = isBatchScript;

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

        private bool IsBatchScript { get; set; }

        /// <summary>
        /// Additional environments variables that should be set/overridden for the process. Can be null.
        /// </summary>
        public IDictionary<string, string> EnvironmentVariables { get; set; }

        public ILogger Logger { get { return this.logger; } }

        public string GetEscapedArguments()
        {
            if (this.CmdLineArgs == null) { return null; }

            var result = string.Join(" ", this.CmdLineArgs.Select(a => EscapeArgument(a)));

            if (IsBatchScript)
            {
                result = ShellEscape(result);
            }

            return result;
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

        /// <summary>
        /// Batch scripts are evil.
        /// The escape character in batch is '^'.
        ///
        /// Example:
        /// script.bat : echo %*
        /// cmd.exe: script.bat foo^>out.txt
        ///
        /// This passes the argument "foo >out.txt" to script.bat.
        /// Variable expansion happen before execution (i.e. it is preprocessing), so the script becomes:
        ///
        /// echo foo>out.txt
        ///
        /// which will write "foo" into the file "out.txt"
        ///
        /// To avoid this, one must call:
        /// cmd.exe: script.bat foo^^^>out.txt
        ///
        /// which gets rewritten into: echo foo^>out.txt
        /// and then executed.
        ///
        /// Note: Delayed expansion is not available for %*, %1
        /// set foo=%* and set foo="%*" with echo !foo!
        /// will only move the command injection away from the "echo" to the "set" itself.
        /// </summary>
        private static string ShellEscape(string argLine)
        {
            var sb = new StringBuilder();
            foreach (var c in argLine)
            {
                // This escape is required after %* is expanded to prevent command injections
                sb.Append('^');
                sb.Append('^');

                // This escape is required only to pass the argument line to the batch script
                sb.Append('^');
                sb.Append(c);
            }
            return sb.ToString();
        }

        #endregion

    }
}
