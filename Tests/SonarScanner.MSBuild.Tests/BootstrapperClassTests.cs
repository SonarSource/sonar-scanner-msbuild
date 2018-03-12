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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild;
using SonarScanner.MSBuild.TFS.Interfaces;
using SonarScanner.MSBuild.PostProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor;
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class BootstrapperClassTests
    {
        private string RootDir;
        private string TempDir;
        private Mock<IProcessorFactory> MockProcessorFactory;
        private Mock<ITeamBuildPreProcessor> MockPreProcessor;
        private Mock<IMSBuildPostProcessor> MockPostProcessor;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void MyTestInitialize()
        {
            RootDir = TestUtils.CreateTestSpecificFolder(TestContext);
            // this is the Temp folder used by Bootstrapper
            TempDir = Path.Combine(RootDir, ".sonarqube");
            // it will look in Directory.GetCurrentDir, which is RootDir.
            var analysisConfigFile = Path.Combine(TempDir, "conf", "SonarQubeAnalysisConfig.xml");
            CreateAnalysisConfig(analysisConfigFile);
            MockProcessors(true, true);
        }

        private void CreateAnalysisConfig(string filePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            var config = new AnalysisConfig();
            config.Save(filePath);
        }

        private void MockProcessors(bool preProcessorOutcome, bool postProcessorOutcome)
        {
            MockPreProcessor = new Mock<ITeamBuildPreProcessor>();
            MockPostProcessor = new Mock<IMSBuildPostProcessor>();
            MockPreProcessor.Setup(x => x.Execute(It.IsAny<string[]>())).Returns(preProcessorOutcome);
            MockPostProcessor.Setup(x => x.Execute(It.IsAny<string[]>(), It.IsAny<AnalysisConfig>(), It.IsAny<ITeamBuildSettings>()
                )).Returns(postProcessorOutcome);
            MockProcessorFactory = new Mock<IProcessorFactory>();
            MockProcessorFactory.Setup(x => x.CreatePostProcessor()).Returns(MockPostProcessor.Object);
            MockProcessorFactory.Setup(x => x.CreatePreProcessor()).Returns(MockPreProcessor.Object);
        }

        #region Tests

        [TestMethod]
        public void Exe_PreProcFails()
        {
            // Arrange
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();

            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                MockProcessors(false, true);

                // Act
                var logger = CheckExecutionFails(AnalysisPhase.PreProcessing, true,
                    "/install:true",  // this argument should just pass through
                    "/d:sonar.verbose=true",
                    "/d:sonar.host.url=http://host:9",
                    "/d:another.key=will be ignored");

                // Assert
                logger.AssertWarningsLogged(0);
                logger.AssertVerbosity(LoggerVerbosity.Debug);

                AssertPreProcessorArgs("/install:true",
                    "/d:sonar.verbose=true",
                    "/d:sonar.host.url=http://host:9",
                    "/d:another.key=will be ignored");

                AssertPostProcessorNotCalled();
            }
        }

        [TestMethod]
        public void Exe_CopyDLLs()
        {
            // Arrange
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();

            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                // Act
                var logger = CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, "/d:sonar.host.url=http://anotherHost");

                // Assert
                Assert.IsTrue(File.Exists(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Common.dll")));
                Assert.IsTrue(File.Exists(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")));
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
                var logger = CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, "/d:sonar.host.url=http://anotherHost");

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
                var filePath = Path.Combine(TempDir, "myfile");
                Directory.CreateDirectory(TempDir);
                var stream = File.Create(filePath);
                stream.Close();
                Assert.IsTrue(File.Exists(filePath));

                // Act
                var logger = CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, "/d:sonar.host.url=http://anotherHost");

                // Assert
                Assert.IsFalse(File.Exists(filePath));
            }
        }

        [TestMethod]
        public void Exe_PostProc_Fails()
        {
            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                MockProcessors(true, false);
                // this is usually created by the PreProcessor
                Directory.CreateDirectory(TempDir);

                // Act
                var logger = CheckExecutionFails(AnalysisPhase.PostProcessing, false);

                // Assert
                logger.AssertWarningsLogged(0);
                logger.AssertErrorsLogged(1);
                AssertPostProcessorArgs();
            }
        }

        [TestMethod]
        public void Exe_PostProc_Fails_On_Missing_TempFolder()
        {
            Directory.Delete(TempDir, true);

            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                // Act
                var logger = CheckExecutionFails(AnalysisPhase.PostProcessing, false);

                // Assert
                logger.AssertErrorsLogged(2);
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
                var logger = CheckExecutionSucceeds(AnalysisPhase.PostProcessing, false, "other params", "yet.more.params");

                // Assert
                logger.AssertWarningsLogged(0);

                // The bootstrapper pass through any parameters it doesn't recognize so the post-processor
                // can decide whether to handle them or not
                AssertPostProcessorArgs("other params", "yet.more.params");
            }
        }

        [TestMethod]
        public void Exe_PostProc_NoAnalysisConfig()
        {
            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                // this is usually created by the PreProcessor
                Directory.CreateDirectory(TempDir);

                var analysisConfigFile = Path.Combine(TempDir, "conf", "SonarQubeAnalysisConfig.xml");
                File.Delete(analysisConfigFile);

                // Act
                var logger = CheckExecutionFails(AnalysisPhase.PostProcessing, false, "other params", "yet.more.params");

                // Assert
                logger.AssertWarningsLogged(0);
                logger.AssertErrorsLogged(2);
                AssertPostProcessorNotCalled();
            }
        }

        #endregion Tests

        #region Private methods

        private static EnvironmentVariableScope InitializeNonTeamBuildEnvironment(string workingDirectory)
        {
            Directory.SetCurrentDirectory(workingDirectory);
            var scope = new EnvironmentVariableScope();
            scope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, null);
            scope.SetVariable(BootstrapperSettings.BuildDirectory_TFS2015, null);
            return scope;
        }

        #endregion Private methods

        #region Checks

        private TestLogger CheckExecutionFails(AnalysisPhase phase, bool debug, params string[] args)
        {
            var logger = new TestLogger();
            var settings = MockBootstrapSettings(phase, debug, args);
            var bootstrapper = new BootstrapperClass(MockProcessorFactory.Object, settings, logger);
            var exitCode = bootstrapper.Execute();

            Assert.AreEqual(SonarScanner.MSBuild.Program.ErrorCode, exitCode, "Bootstrapper did not return the expected exit code");
            logger.AssertErrorsLogged();

            return logger;
        }

        private TestLogger CheckExecutionSucceeds(AnalysisPhase phase, bool debug, params string[] args)
        {
            var logger = new TestLogger();
            var settings = MockBootstrapSettings(phase, debug, args);
            var bootstrapper = new BootstrapperClass(MockProcessorFactory.Object, settings, logger);
            var exitCode = bootstrapper.Execute();

            Assert.AreEqual(0, exitCode, "Bootstrapper did not return the expected exit code");
            logger.AssertErrorsLogged(0);

            return logger;
        }

        private IBootstrapperSettings MockBootstrapSettings(AnalysisPhase phase, bool debug, string[] args)
        {
            Mock<IBootstrapperSettings> mockBootstrapSettings;

            File.Create(Path.Combine(RootDir, "SonarScanner.MSBuild.Common.dll")).Close();
            File.Create(Path.Combine(RootDir, "SonarScanner.MSBuild.Tasks.dll")).Close();

            mockBootstrapSettings = new Mock<IBootstrapperSettings>();
            mockBootstrapSettings.SetupGet(x => x.ChildCmdLineArgs).Returns(args.ToArray);
            mockBootstrapSettings.SetupGet(x => x.TempDirectory).Returns(TempDir);
            mockBootstrapSettings.SetupGet(x => x.Phase).Returns(phase);
            mockBootstrapSettings.SetupGet(x => x.ScannerBinaryDirPath).Returns(RootDir);
            mockBootstrapSettings.SetupGet(x => x.LoggingVerbosity).Returns(debug ? LoggerVerbosity.Debug : LoggerVerbosity.Info);

            return mockBootstrapSettings.Object;
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
