//-----------------------------------------------------------------------
// <copyright file="BootstrapperExeTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration.Interfaces;
using SonarQube.TeamBuild.PostProcessor.Interfaces;
using SonarQube.TeamBuild.PreProcessor;
using System.IO;
using TestUtilities;
using static SonarQube.Bootstrapper.Program;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class BootstrapperExeTests
    {
        private string RootDir;
        private string TempDir;
        private Mock<IProcessFactory> MockProcessorFactory;
        private Mock<ITeamBuildPreProcessor> MockPreProcessor;
        private Mock<IMSBuildPostProcessor> MockPostProcessor;
        private Mock<ITeamBuildSettings> MockTeamBuildSettings;

        public TestContext TestContext { get; set; }

        [TestInitialize()]
        public void MyTestInitialize() {
            RootDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            // this is the Temp folder used by Bootstrapper
            TempDir = Path.Combine(RootDir, ".sonarqube");
            string analysisConfigFile = Path.Combine(RootDir, "analysisConfig.xml");
            createAnalysisConfig(analysisConfigFile);

            mockProcessors(true, true);
            MockTeamBuildSettings = new Mock<ITeamBuildSettings>();
            MockTeamBuildSettings.SetupGet(x => x.AnalysisConfigFilePath).Returns(analysisConfigFile);
        }

        private void createAnalysisConfig(string filePath)
        {
            AnalysisConfig config = new AnalysisConfig();
            config.Save(filePath);
        }

        private void mockProcessors(bool preProcessorOutcome, bool postProcessorOutcome)
        {
            MockPreProcessor = new Mock<ITeamBuildPreProcessor>();
            MockPostProcessor = new Mock<IMSBuildPostProcessor>();
            MockPreProcessor.Setup(x => x.Execute(It.IsAny<string[]>())).Returns(preProcessorOutcome);
            MockPostProcessor.Setup(x => x.Execute(It.IsAny<string[]>(), It.IsAny<AnalysisConfig>(), It.IsAny<ITeamBuildSettings>()
                )).Returns(postProcessorOutcome);
            MockProcessorFactory = new Mock<IProcessFactory>();
            MockProcessorFactory.Setup(x => x.createPostProcessor()).Returns(MockPostProcessor.Object);
            MockProcessorFactory.Setup(x => x.createPreProcessor()).Returns(MockPreProcessor.Object);
        }

        #region Tests

        [TestMethod]
        public void Exe_ParsingFails()
        {
            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                // Act
                TestLogger logger = CheckExecutionFails("/d: badkey=123");

                // Assert
                logger.AssertErrorsLogged();
            }
        }

        [TestMethod]
        public void Exe_PreProc_URLIsRequired()
        {
            // Arrange
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();

            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                // Act
                TestLogger logger = CheckExecutionFails("begin");

                // Assert
                logger.AssertErrorLogged(SonarQube.Bootstrapper.Resources.ERROR_Args_UrlRequired);
                logger.AssertErrorsLogged(1);
            }
        }

        [TestMethod]
        public void Exe_PreProcFails()
        {
            // Arrange
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();

            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                mockProcessors(false, true);

                // Act
                TestLogger logger = CheckExecutionFails("begin",
                    "/install:true",  // this argument should just pass through
                    "/d:sonar.verbose=true",
                    "/d:sonar.host.url=http://host:9",
                    "/d:another.key=will be ignored");

                // Assert
                logger.AssertWarningsLogged(0);
                logger.AssertVerbosity(LoggerVerbosity.Debug); // sonar.verbose=true was specified

                AssertPreProcessorArgs("/install:true",
                    "/d:sonar.verbose=true",
                    "/d:sonar.host.url=http://host:9",
                    "/d:another.key=will be ignored");

                AssertPostProcessorNotCalled();
            }
        }

        [TestMethod]
        public void Exe_PreProcSucceeds()
        {
            // Arrange
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();

            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                // Act
                TestLogger logger = CheckExecutionSucceeds("/d:sonar.host.url=http://anotherHost", "begin");

                // Assert
                logger.AssertWarningsLogged(0);
                logger.AssertVerbosity(VerbosityCalculator.DefaultLoggingVerbosity);

                AssertPreProcessorArgs("/d:sonar.host.url=http://anotherHost");
            }
        }

        [TestMethod]
        public void Exe_PreProcCleansTemp()
        {
            // Arrange
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();

            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                // Create dummy file in Temp
                string filePath = Path.Combine(TempDir, "myfile");
                Directory.CreateDirectory(TempDir);
                FileStream stream = File.Create(filePath);
                stream.Close();
                Assert.IsTrue(File.Exists(filePath));

                // Act
                TestLogger logger = CheckExecutionSucceeds("/d:sonar.host.url=http://anotherHost", "begin");

                // Assert
                Assert.IsFalse(File.Exists(filePath));
            }
        }

        [TestMethod]
        public void Exe_PostProc_Fails()
        {
            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                mockProcessors(true, false);
                // this is usually created by the PreProcessor
                Directory.CreateDirectory(TempDir);

                // Act
                TestLogger logger = CheckExecutionFails("end");

                // Assert
                logger.AssertWarningsLogged(0);
                logger.AssertErrorsLogged(1);
                AssertPostProcessorArgs();
            }
        }

        [TestMethod]
        public void Exe_PostProc_Succeeds()
        {
            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                // this is usually created by the PreProcessor
                Directory.CreateDirectory(TempDir);

                // Act
                TestLogger logger = CheckExecutionSucceeds("end", "other params", "yet.more.params");

                // Assert
                logger.AssertWarningsLogged(0);

                // The bootstrapper pass through any parameters it doesn't recognise so the post-processor
                // can decide whether to handle them or not
                AssertPostProcessorArgs("other params",
                    "yet.more.params");
            }
        }

        [TestMethod]
        public void Exe_PropertiesFile()
        {
            // Default settings:
            // There must be a default settings file next to the bootstrapper exe to supply
            // the necessary settings, and the bootstrapper should pass this settings path
            // to the pre-processor 

            // Arrange
            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                // Create a default properties file next to the exe
                AnalysisProperties defaultProperties = new AnalysisProperties();
                defaultProperties.Add(new Property() { Id = SonarProperties.HostUrl, Value = "http://host" });
                string defaultPropertiesFilePath = CreateDefaultPropertiesFile(defaultProperties);

                // Act
                try
                {
                    // Call the pre-processor
                    TestLogger logger = CheckExecutionSucceeds("/v:version", "/n:name", "/k:key");
                    logger.AssertWarningsLogged(1); // Should be warned once about the missing "begin" / "end"
                    logger.AssertSingleWarningExists(ArgumentProcessor.BeginVerb, ArgumentProcessor.EndVerb);

                   AssertPreProcessorArgs("/v:version",
                        "/n:name",
                        "/k:key",
                        "/s:" + defaultPropertiesFilePath);

                    AssertPostProcessorNotCalled();

                    // Call the post-process (no arguments)
                    logger = CheckExecutionSucceeds();

                    AssertPostProcessorArgs();

                    logger.AssertWarningsLogged(1); // Should be warned once about the missing "begin" / "end"
                    logger.AssertSingleWarningExists(ArgumentProcessor.BeginVerb, ArgumentProcessor.EndVerb);
                }
                finally
                {
                    File.Delete(defaultPropertiesFilePath);
                }
            }
        }

        #endregion Tests

        #region Private methods

        private static EnvironmentVariableScope InitializeNonTeamBuildEnvironment(string workingDirectory)
        {
            Directory.SetCurrentDirectory(workingDirectory);
            EnvironmentVariableScope scope = new EnvironmentVariableScope();
            scope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, null);
            scope.SetVariable(BootstrapperSettings.BuildDirectory_TFS2015, null);
            return scope;
        }

        private static string CreateDefaultPropertiesFile(AnalysisProperties defaultProperties)
        {
            // NOTE: don't forget to delete this file when the test that uses it
            // completes, otherwise it may affect subsequent tests.
            string defaultPropertiesFilePath = BootstrapperTestUtils.GetDefaultPropertiesFilePath();
            defaultProperties.Save(defaultPropertiesFilePath);
            return defaultPropertiesFilePath;
        }

        #endregion Private methods

        #region Checks

        private TestLogger CheckExecutionFails(params string[] args)
        {
            TestLogger logger = new TestLogger();

            int exitCode = Bootstrapper.Program.Execute(args, MockProcessorFactory.Object, MockTeamBuildSettings.Object, logger);

            Assert.AreEqual(Bootstrapper.Program.ErrorCode, exitCode, "Bootstrapper did not return the expected exit code");
            logger.AssertErrorsLogged();

            return logger;
        }

        private TestLogger CheckExecutionSucceeds(params string[] args)
        {
            TestLogger logger = new TestLogger();

            int exitCode = Bootstrapper.Program.Execute(args, MockProcessorFactory.Object, MockTeamBuildSettings.Object, logger);

            Assert.AreEqual(0, exitCode, "Bootstrapper did not return the expected exit code");
            logger.AssertErrorsLogged(0);

            return logger;
        }

        private void AssertPostProcessorNotCalled()
        {
            MockPostProcessor.Verify(x => x.Execute(
                It.IsAny<string[]>(), It.IsAny<AnalysisConfig>(), It.IsAny<ITeamBuildSettings>()), 
                Times.Never());
        }

        private void AssertPreProcessorNotCalled()
        {
            MockPreProcessor.Verify(x => x.Execute(It.IsAny<string[]>()), Times.Never());
        }

        private void AssertPostProcessorArgs(params string[] expectedArgs)
        {
            MockPostProcessor.Verify(x => x.Execute(
                expectedArgs, It.IsAny<AnalysisConfig>(), It.IsAny<ITeamBuildSettings>()),
                Times.Once());
        }

        private void AssertPreProcessorArgs(params string[] expectedArgs)
        {
            MockPreProcessor.Verify(x => x.Execute(expectedArgs), Times.Once());
        }

        #endregion Checks
    }
}