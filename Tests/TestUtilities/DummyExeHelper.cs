//-----------------------------------------------------------------------
// <copyright file="DummyExeHelper.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.CodeDom.Compiler;
using System.IO;

namespace TestUtilities
{
    /// <summary>
    /// Creates dummy executables that log the input parameters and return a specified
    /// exit code
    /// </summary>
    public static class DummyExeHelper
    {
        // FIX: should be using constants in the product code
        public const string PreProcessorExeName = "SonarQube.MSBuild.PreProcessor.exe";
        public const string PostProcessorExeName = "SonarQube.MSBuild.PostProcessor.exe";

        #region Public methods

        public static void CreateDummyPreProcessor(string dummyBinDir, int exitCode)
        {
            CreateDummyExe(dummyBinDir, PreProcessorExeName, exitCode);
        }

        public static void CreateDummyPostProcessor(string dummyBinDir, int exitCode)
        {
            CreateDummyExe(dummyBinDir, PostProcessorExeName, exitCode);
        }

        #endregion

        #region Checks

        public static string AssertDummyPreProcLogExists(string dummyBinDir, TestContext testContext)
        {
            return AssertLogFileExists(dummyBinDir, PreProcessorExeName, testContext);
        }

        public static string AssertDummyPostProcLogExists(string dummyBinDir, TestContext testContext)
        {
            return AssertLogFileExists(dummyBinDir, PostProcessorExeName, testContext);
        }

        public static string AssertDummyPreProcLogDoesNotExist(string dummyBinDir)
        {
            return AssertLogFileDoesNotExist(dummyBinDir, PreProcessorExeName);
        }

        public static string AssertDummyPostProcLogDoesNotExist(string dummyBinDir)
        {
            return AssertLogFileDoesNotExist(dummyBinDir, PostProcessorExeName);
        }

        public static void AssertExpectedLogContents(string logPath, string expected)
        {
            Assert.IsTrue(File.Exists(logPath), "Expected log file does not exist: {0}", logPath);

            string actual = File.ReadAllText(logPath);
            Assert.AreEqual(expected, actual, "Log file does not have the expected content");
        }

        private static string AssertLogFileExists(string dummyBinDir, string exeName, TestContext testContext)
        {
            string logFilePath = GetLogFilePath(dummyBinDir, exeName);

            Assert.IsTrue(File.Exists(logFilePath), "Expecting the dummy exe log to exist. File: {0}", logFilePath);
            testContext.AddResultFile(logFilePath);
            return logFilePath;
        }

        private static string AssertLogFileDoesNotExist(string dummyBinDir, string exeName)
        {
            string logFilePath = GetLogFilePath(dummyBinDir, exeName);

            Assert.IsFalse(File.Exists(logFilePath), "Not expecting the dummy exe log to exist. File: {0}", logFilePath);
            return logFilePath;
        }

        #endregion

        #region Private methods

        private static string CreateDummyExe(string outputDir, string exeName, int exitCode)
        {
            string code = GetDummyExeSource(exitCode);
            string asmPath = Path.Combine(outputDir, exeName);
            CompileAssembly(code, asmPath);
            return asmPath;
        }

        private static string GetDummyExeSource(int returnCode)
        {
            string code;
            string resourceName = "TestUtilities.EmbeddedCode.DummyExe.cs";

            using (Stream stream = typeof(DummyExeHelper).Assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                code = reader.ReadToEnd();
            }

            code = code.Replace("EXITCODE_PLACEHOLDER", returnCode.ToString());
            return code;
        }

        /// <summary>
        /// Compiles the supplied code into a new assembly
        /// </summary>
        private static void CompileAssembly(string code, string outputFilePath)
        {
            CSharpCodeProvider provider = new CSharpCodeProvider();

            CompilerParameters options = new CompilerParameters();
            options.OutputAssembly = outputFilePath;
            options.GenerateExecutable = true;
            options.GenerateInMemory = false;


            CompilerResults result = provider.CompileAssemblyFromSource(options, code);

            if (result.Errors.Count > 0)
            {
                Console.WriteLine(result.Output.ToString());
                Assert.Fail("Test setup error: failed to create dynamic assembly. See the test output for compiler output");
            }
        }

        private static string GetLogFilePath(string dummyBinDir, string exeName)
        {
            string logFilePath = Path.Combine(dummyBinDir, exeName);
            logFilePath = Path.ChangeExtension(logFilePath, ".log");
            return logFilePath;
        }

        #endregion

    }
}