//-----------------------------------------------------------------------
// <copyright file="BootstrapperExeTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.IO;
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class BootstrapperExeTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void Exe_ParsingFails()
        {
            // Arrange
            string rootDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                string binDir = CalculateBinDir(rootDir);

                MockBuildAgentUpdater mockUpdater = new MockBuildAgentUpdater();

                // Act
                TestLogger logger = CheckExecutionFails(mockUpdater, "/d: badkey=123");

                // Assert
                mockUpdater.AssertUpdateNotAttempted();
                mockUpdater.AssertVersionNotChecked();

                AssertDirectoryDoesNotExist(binDir);

                logger.AssertErrorsLogged();
            }
        }

        [TestMethod]
        public void Exe_PreProc_UpdateFails()
        {
            // Arrange
            string rootDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            using (InitializeNonTeamBuildEnvironment(rootDir))
            {

                string binDir = CalculateBinDir(rootDir);
                MockBuildAgentUpdater mockUpdater = CreateValidUpdater(binDir, "http://host:9000");
                mockUpdater.TryUpdateReturnValue = false;

                // Act
                TestLogger logger = CheckExecutionFails(mockUpdater, "/d:sonar.host.url=http://host:9000", "begin");

                // Assert
                mockUpdater.AssertUpdateAttempted();
                mockUpdater.AssertVersionNotChecked();

                logger.AssertWarningsLogged(0);

                AssertDirectoryExists(binDir);
                logger.AssertErrorsLogged();
            }
        }

        [TestMethod]
        public void Exe_PreProc_VersionCheckFails()
        {
            // Arrange
            string rootDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                string binDir = CalculateBinDir(rootDir);
                MockBuildAgentUpdater mockUpdater = CreateValidUpdater(binDir, "http://ahost");
                mockUpdater.VersionCheckReturnValue = false;

                // Act
                TestLogger logger = CheckExecutionFails(mockUpdater, "/d:sonar.host.url=http://ahost", "begin");

                // Assert
                mockUpdater.AssertUpdateAttempted();
                mockUpdater.AssertVersionChecked();

                AssertDirectoryExists(binDir);
                DummyExeHelper.AssertDummyPreProcLogDoesNotExist(binDir);
                DummyExeHelper.AssertDummyPostProcLogDoesNotExist(binDir);
                logger.AssertErrorsLogged();
                logger.AssertWarningsLogged(0);
            }
        }

        [TestMethod]
        public void Exe_PreProc_VersionCheckSucceeds_PreProcFails()
        {
            // Arrange
            File.Delete(CreateDefaultPropertiesFile(new AnalysisProperties()));

            string rootDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                string binDir = CalculateBinDir(rootDir);
                MockBuildAgentUpdater mockUpdater = CreateValidUpdater(binDir, "http://host:9");

                mockUpdater.Updating += (sender, args) =>
                {
                    AssertDirectoryExists(args.TargetDir);
                    DummyExeHelper.CreateDummyPreProcessor(args.TargetDir, 1 /* pre-proc fails */);
                };

                // Act
                TestLogger logger = CheckExecutionFails(mockUpdater,
                    "begin",
                    "/d:sonar.host.url=http://host:9",
                    "/d:another.key=will be ignored");

                // Assert
                mockUpdater.AssertUpdateAttempted();
                mockUpdater.AssertVersionChecked();

                logger.AssertWarningsLogged(0);

                string logPath = DummyExeHelper.AssertDummyPreProcLogExists(binDir, this.TestContext);
                DummyExeHelper.AssertExpectedLogContents(logPath, "/d:sonar.host.url=http://host:9\r\n/d:another.key=will be ignored\r\n");
            }
        }

        [TestMethod]
        public void Exe_PreProc_VersionCheckSucceeds_PreProcSucceeds()
        {
            // Arrange
            File.Delete(CreateDefaultPropertiesFile(new AnalysisProperties()));

            string rootDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                string binDir = CalculateBinDir(rootDir);

                MockBuildAgentUpdater mockUpdater = CreateValidUpdater(binDir, "http://anotherHost");

                mockUpdater.Updating += (sender, args) =>
                {
                    AssertDirectoryExists(args.TargetDir);
                    DummyExeHelper.CreateDummyPreProcessor(args.TargetDir, 0 /* pre-proc succeeds */);
                };

                // Act
                TestLogger logger = CheckExecutionSucceeds(mockUpdater,
                    "/d:sonar.host.url=http://anotherHost", "begin");

                // Assert
                mockUpdater.AssertUpdateAttempted();
                mockUpdater.AssertVersionChecked();
                logger.AssertWarningsLogged(0);

                string logPath = DummyExeHelper.AssertDummyPreProcLogExists(binDir, this.TestContext);
                DummyExeHelper.AssertExpectedLogContents(logPath, "/d:sonar.host.url=http://anotherHost\r\n");
            }
        }

        [TestMethod]
        public void Exe_PostProc_Fails()
        {
            // Arrange
            string rootDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                string binDir = CalculateBinDir(rootDir);
                Directory.CreateDirectory(binDir);
                DummyExeHelper.CreateDummyPostProcessor(binDir, 1 /* post-proc fails */);

                MockBuildAgentUpdater mockUpdater = CreateValidUpdater(binDir, "http://h:9000");

                // Act
                TestLogger logger = CheckExecutionFails(mockUpdater, "end");

                // Assert
                mockUpdater.AssertUpdateNotAttempted();
                mockUpdater.AssertVersionNotChecked();
                logger.AssertWarningsLogged(0);

                string logPath = DummyExeHelper.AssertDummyPostProcLogExists(binDir, this.TestContext);
                DummyExeHelper.AssertExpectedLogContents(logPath, string.Empty);
            }
        }

        [TestMethod]
        public void Exe_PostProc_Succeeds()
        {
            // Arrange
            string rootDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                string binDir = CalculateBinDir(rootDir);
                Directory.CreateDirectory(binDir);
                DummyExeHelper.CreateDummyPostProcessor(binDir, 0 /* success exit code */);

                MockBuildAgentUpdater mockUpdater = CreateValidUpdater(binDir, "http://h:9000");

                // Act
                TestLogger logger = CheckExecutionSucceeds(mockUpdater, "end", "other params", "yet.more.params");

                // Assert
                mockUpdater.AssertUpdateNotAttempted();
                mockUpdater.AssertVersionNotChecked();
                logger.AssertWarningsLogged(0);

                // The bootstrapper pass through any parameters it doesn't recognise so the post-processor
                // can decide whether to handle them or not
                string logPath = DummyExeHelper.AssertDummyPostProcLogExists(binDir, this.TestContext);
                DummyExeHelper.AssertExpectedLogContents(logPath, "other params\r\nyet.more.params\r\n");
            }
        }

        [TestMethod]
        public void Exe__Version0_9Compatibility()
        {
            // Tests compatibility with the bootstrapper API used in v0.9
            // The pre-processor should be called if any arguments are passed.
            // The post-processor should be called if no arguments are passed.

            // Default settings:
            // There must be a default settings file next to the bootstrapper exe to supply 
            // the necessary settings, and the bootstrapper should pass this settings path 
            // to the pre-processor (since the pre-process is downloaded to a different
            // directory).

            // Arrange
            string rootDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                string binDir = CalculateBinDir(rootDir);
                MockBuildAgentUpdater mockUpdater = CreateValidUpdater(binDir, "http://host");

                mockUpdater.Updating += (sender, args) =>
                {
                    Assert.IsTrue(Directory.Exists(args.TargetDir), "Expecting the target directory to have been created");
                    DummyExeHelper.CreateDummyPreProcessor(args.TargetDir, 0 /* post-proc succeeds */);
                    DummyExeHelper.CreateDummyPostProcessor(args.TargetDir, 0 /* post-proc succeeds */);
                };

                // Create a default properties file next to the exe
                AnalysisProperties defaultProperties = new AnalysisProperties();
                defaultProperties.Add(new Property() { Id = SonarProperties.HostUrl, Value = "http://host" });
                string defaultPropertiesFilePath = CreateDefaultPropertiesFile(defaultProperties);

                // Act
                try
                {
                    // Call the pre-processor
                    TestLogger logger = CheckExecutionSucceeds(mockUpdater, "/v:version", "/n:name", "/k:key");
                    logger.AssertWarningsLogged(1); // Should be warned once about the missing "begin" / "end"
                    logger.AssertSingleWarningExists(ArgumentProcessor.BeginVerb, ArgumentProcessor.EndVerb);

                    mockUpdater.AssertUpdateAttempted();
                    mockUpdater.AssertVersionChecked();

                    string logPath = DummyExeHelper.AssertDummyPreProcLogExists(binDir, this.TestContext);
                    DummyExeHelper.AssertExpectedLogContents(logPath, "/v:version\r\n/n:name\r\n/k:key\r\n/s:" + defaultPropertiesFilePath + "\r\n");

                    DummyExeHelper.AssertDummyPostProcLogDoesNotExist(binDir);


                    // Call the post-process (no arguments)
                    logger = CheckExecutionSucceeds(mockUpdater);

                    logPath = DummyExeHelper.AssertDummyPostProcLogExists(binDir, this.TestContext);
                    DummyExeHelper.AssertExpectedLogContents(logPath, string.Empty);

                    logger.AssertWarningsLogged(1); // Should be warned once about the missing "begin" / "end"
                    logger.AssertSingleWarningExists(ArgumentProcessor.BeginVerb, ArgumentProcessor.EndVerb);
                }
                finally
                {
                    File.Delete(defaultPropertiesFilePath);
                }
            }
        }

        #endregion

        #region Private methods

        private static EnvironmentVariableScope InitializeNonTeamBuildEnvironment(string workingDirectory)
        {
            Directory.SetCurrentDirectory(workingDirectory);
            EnvironmentVariableScope scope = new EnvironmentVariableScope();
            scope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, null);
            scope.SetVariable(BootstrapperSettings.BuildDirectory_TFS2015, null);
            return scope;
        }

        /// <summary>
        /// Creates and returns a mock updater that execute successfully
        /// </summary>
        private static MockBuildAgentUpdater CreateValidUpdater(string binDir, string hostUrl)
        {
            string versionFile = Path.Combine(binDir, BootstrapperSettings.SupportedVersionsFilename);

            MockBuildAgentUpdater mockUpdater = new MockBuildAgentUpdater();
            mockUpdater.ExpectedHostUrl = hostUrl;
            mockUpdater.ExpectedTargetDir = binDir;
            mockUpdater.TryUpdateReturnValue = true;
            mockUpdater.ExpectedVersionPath = versionFile;
            mockUpdater.ExpectedVersion = new System.Version(BootstrapperSettings.LogicalVersionString);
            mockUpdater.VersionCheckReturnValue = true;

            return mockUpdater;
        }

        private static string CalculateBinDir(string rootDir)
        {
            return Path.Combine(rootDir, ".sonarqube", "bin");
        }

        private static string CreateDefaultPropertiesFile(AnalysisProperties defaultProperties)
        {
            // NOTE: don't forget to delete this file when the test that uses it
            // completes, otherwise it may affect subsequent tests.
            string defaultPropertiesFilePath = Path.Combine(Path.GetDirectoryName(typeof(Bootstrapper.Program).Assembly.Location), FilePropertyProvider.DefaultFileName);
            defaultProperties.Save(defaultPropertiesFilePath);
            return defaultPropertiesFilePath;
        }

        #endregion

        #region Checks

        private static TestLogger CheckExecutionFails(IBuildAgentUpdater updater, params string[] args)
        {
            TestLogger logger = new TestLogger();

            int exitCode = Bootstrapper.Program.Execute(args, updater, logger);

            Assert.AreEqual(Bootstrapper.Program.ErrorCode, exitCode, "Bootstrapper did not return the expected exit code");
            logger.AssertErrorsLogged();

            return logger;
        }

        private static TestLogger CheckExecutionSucceeds(IBuildAgentUpdater updater, params string[] args)
        {
            TestLogger logger = new TestLogger();

            int exitCode = Bootstrapper.Program.Execute(args, updater, logger);

            Assert.AreEqual(0, exitCode, "Bootstrapper did not return the expected exit code");
            logger.AssertErrorsLogged(0);

            return logger;
        }

        private static void AssertDirectoryExists(string binDir)
        {
            Assert.IsTrue(Directory.Exists(binDir), "Expecting the directory to exist. Directory: {0}", binDir);
        }

        private static void AssertDirectoryDoesNotExist(string binDir)
        {
            Assert.IsFalse(Directory.Exists(binDir), "Not expecting directory to exist. Directory: {0}", binDir);
        }

        #endregion
    }
}
