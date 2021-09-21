/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2021 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
        private const string PostProcessorExeName = "MSBuild.SonarQube.Internal.PostProcess.exe";

        public static string CreateDummyPostProcessor(string dummyBinDir)
        {
            var code = GetDummyExeSource();
            var asmPath = Path.Combine(dummyBinDir, PostProcessorExeName);
            CompileAssembly(code, asmPath);
            return asmPath;
        }

        public static string AssertDummyPostProcLogExists(string dummyBinDir, TestContext testContext)
        {
            var logFilePath = Path.Combine(dummyBinDir, PostProcessorExeName);
            logFilePath = Path.ChangeExtension(logFilePath, ".log");
            File.Exists(logFilePath).Should().BeTrue("Expecting the dummy exe log to exist. File: {0}", logFilePath);
            testContext.AddResultFile(logFilePath);
            return logFilePath;
        }

        public static void AssertExpectedLogContents(string logPath, params string[] expected)
        {
            File.Exists(logPath).Should().BeTrue("Expected log file does not exist: {0}", logPath);
            var actualLines = File.ReadAllLines(logPath);
            (expected ?? new string[] { }).Should().BeEquivalentTo(actualLines, "Log file does not have the expected content");
        }

        private static string GetDummyExeSource()
        {
            var resourceName = "TestUtilities.EmbeddedCode.DummyExe.cs";
            using (var stream = typeof(DummyExeHelper).Assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
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
    }
}
