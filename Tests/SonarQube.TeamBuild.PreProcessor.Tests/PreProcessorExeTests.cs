/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
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
