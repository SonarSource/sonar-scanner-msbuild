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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class PreProcessorExeTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PreProc_ErrorCodeReturnedForInvalidCommandLine()
        {
            string errorOutput = CheckExecutionFails("invalid_command_line aaa 123");

            Assert.IsTrue(errorOutput.Contains("invalid_command_line"), "Error output did not contain the expected data. Check that the exe failed for the expected reason");
        }

        #endregion

        #region Checks

        private static string CheckExecutionFails(params string[] args)
        {
            Process p = Execute(args);

            Assert.AreNotEqual(0, p.ExitCode, "Expecting a non-zero exit code");
            string errorOutput = p.StandardError.ReadToEnd();
            Console.WriteLine("Error output: {0}", errorOutput);

            Assert.IsFalse(string.IsNullOrWhiteSpace(errorOutput), "Expecting error output if the process fails");
            return errorOutput;
        }

        private static Process Execute(string[] args)
        {
            ProcessStartInfo psi = new ProcessStartInfo()
            {
                FileName = typeof(SonarQube.TeamBuild.PreProcessor.Program).Assembly.Location,
                RedirectStandardError = true,
                UseShellExecute = false, // required if we want to capture the error output
                ErrorDialog = false,
                CreateNoWindow = true,
                Arguments = string.Join(" ", args)
            };

            Process p = Process.Start(psi);
            p.WaitForExit(5000); // if the process times out then it's possible that an unhandled exception is being thrown
            Assert.IsTrue(p.HasExited, "Timed out waiting for the process to exit");
            return p;
        }

        #endregion
    }
}
