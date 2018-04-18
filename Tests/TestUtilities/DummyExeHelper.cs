/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System;
using System.CodeDom.Compiler;
using System.IO;
using FluentAssertions;
using Microsoft.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities
{
    /// <summary>
    /// Creates dummy executables that log the input parameters and return a specified
    /// exit code
    /// </summary>
    public static class DummyExeHelper
    {
        // FIX: should be using constants in the product code
        public const string PreProcessorExeName = "MSBuild.SonarQube.Internal.PreProcess.exe";

        public const string PostProcessorExeName = "MSBuild.SonarQube.Internal.PostProcess.exe";

        #region Public methods

        public static string CreateDummyPreProcessor(string dummyBinDir, int exitCode)
        {
            return CreateDummyExe(dummyBinDir, PreProcessorExeName, exitCode);
        }

        public static string CreateDummyPostProcessor(string dummyBinDir, int exitCode)
        {
            return CreateDummyExe(dummyBinDir, PostProcessorExeName, exitCode);
        }

        public static string CreateDummyExe(string outputDir, string exeName, int exitCode)
        {
            return CreateDummyExe(outputDir, exeName, exitCode, null);
        }

        public static string CreateDummyExe(string outputDir, string exeName, int exitCode, string additionalCode)
        {
            var code = GetDummyExeSource(exitCode, additionalCode);
            var asmPath = Path.Combine(outputDir, exeName);
            CompileAssembly(code, asmPath);
            return asmPath;
        }

        #endregion Public methods

        #region Checks

        public static string AssertDummyPreProcLogExists(string dummyBinDir, TestContext testContext)
        {
            var logFilePath = GetLogFilePath(dummyBinDir, PreProcessorExeName);
            return AssertLogFileExists(logFilePath, testContext);
        }

        public static string AssertDummyPostProcLogExists(string dummyBinDir, TestContext testContext)
        {
            var logFilePath = GetLogFilePath(dummyBinDir, PostProcessorExeName);
            return AssertLogFileExists(logFilePath, testContext);
        }

        public static string AssertDummyPreProcLogDoesNotExist(string dummyBinDir)
        {
            return AssertLogFileDoesNotExist(dummyBinDir, PreProcessorExeName);
        }

        public static string AssertDummyPostProcLogDoesNotExist(string dummyBinDir)
        {
            return AssertLogFileDoesNotExist(dummyBinDir, PostProcessorExeName);
        }

        public static string GetLogFilePath(string dummyBinDir, string exeName)
        {
            var logFilePath = Path.Combine(dummyBinDir, exeName);
            logFilePath = Path.ChangeExtension(logFilePath, ".log");
            return logFilePath;
        }

        public static void AssertExpectedLogContents(string logPath, params string[] expected)
        {
            File.Exists(logPath).Should().BeTrue("Expected log file does not exist: {0}", logPath);

            var actualLines = File.ReadAllLines(logPath);

            (expected ?? new string[] { }).Should().BeEquivalentTo(actualLines, "Log file does not have the expected content");
        }

        public static string AssertLogFileExists(string logFilePath, TestContext testContext)
        {
            File.Exists(logFilePath).Should().BeTrue("Expecting the dummy exe log to exist. File: {0}", logFilePath);
            testContext.AddResultFile(logFilePath);
            return logFilePath;
        }

        public static string AssertLogFileDoesNotExist(string dummyBinDir, string exeName)
        {
            var logFilePath = GetLogFilePath(dummyBinDir, exeName);

            File.Exists(logFilePath).Should().BeFalse("Not expecting the dummy exe log to exist. File: {0}", logFilePath);
            return logFilePath;
        }

        #endregion Checks

        #region Private methods

        private static string GetDummyExeSource(int returnCode, string additionalCode)
        {
            string code;
            var resourceName = "TestUtilities.EmbeddedCode.DummyExe.cs";

            using (var stream = typeof(DummyExeHelper).Assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                code = reader.ReadToEnd();
            }

            code = code.Replace("EXITCODE_PLACEHOLDER", returnCode.ToString());
            code = code.Replace("ADDITIONALCODE_PLACEHOLDER", additionalCode);
            return code;
        }

        /// <summary>
        /// Compiles the supplied code into a new assembly
        /// </summary>
        private static void CompileAssembly(string code, string outputFilePath)
        {
            var provider = new CSharpCodeProvider();

            var options = new CompilerParameters
            {
                OutputAssembly = outputFilePath,
                GenerateExecutable = true,
                GenerateInMemory = false
            };

            var result = provider.CompileAssemblyFromSource(options, code);

            if (result.Errors.Count > 0)
            {
                foreach(var item in result.Output)
                {
                    Console.WriteLine(item);
                }

                Assert.Fail("Test setup error: failed to create dynamic assembly. See the test output for compiler output");
            }
        }

        #endregion Private methods
    }
}
