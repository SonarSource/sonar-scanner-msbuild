/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PostProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor;
using SonarScanner.MSBuild.TFS.Interfaces;
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
            RootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
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
            MockPreProcessor.Setup(x => x.Execute(It.IsAny<string[]>())).Returns(Task.FromResult(preProcessorOutcome));
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
                    null,
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
        public void CopyDlls_WhenFileDoNotExist_FilesAreCopied()
        {
            // Arrange
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();

            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                // Sanity
                File.Exists(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeFalse();
                File.Exists(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeFalse();

                // Act
                CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, null, "/d:sonar.host.url=http://anotherHost");

                // Assert
                File.Exists(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeTrue();
                File.Exists(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeTrue();
            }
        }

        [TestMethod]
        public void CopyDlls_WhenFileExistButAreNotLocked_FilesAreCopied()
        {
            // Arrange
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();

            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                Directory.CreateDirectory(Path.Combine(TempDir, "bin"));
                File.Create(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Close();
                File.Create(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Close();

                // Act
                CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, null, "/d:sonar.host.url=http://anotherHost");

                // Assert
                File.Exists(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeTrue();
                File.Exists(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeTrue();
            }
        }

        [TestMethod]
        public void CopyDlls_WhenFileExistAndAreLockedButSameVersion_DoNothing()
        {
            // Arrange
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();

            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                Directory.CreateDirectory(Path.Combine(TempDir, "bin"));
                var file1 = File.Create(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Common.dll"));
                var file2 = File.Create(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Tasks.dll"));

                // Act
                CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, _ => new Version(), "/d:sonar.host.url=http://anotherHost");

                // Assert
                File.Exists(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeTrue();
                File.Exists(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeTrue();

                // Do not close before to ensure the file is locked
                file1.Close();
                file2.Close();
            }
        }

        [TestMethod]
        public void CopyDlls_WhenFileExistAndAreLockedButDifferentVersion_Fails()
        {
            // Arrange
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();

            using (InitializeNonTeamBuildEnvironment(RootDir))
            {
                Directory.CreateDirectory(Path.Combine(TempDir, "bin"));
                var file1 = File.Create(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Common.dll"));
                var file2 = File.Create(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Tasks.dll"));

                int callCount = 0;
                Func<string, Version> getAssemblyVersion = _ =>
                {
                    if (callCount == 0)
                    {
                        callCount++;
                        return new Version("1.0");
                    }

                    return new Version("2.0");
                };

                // Act
                var logger = CheckExecutionFails(AnalysisPhase.PreProcessing, false, getAssemblyVersion, "/d:sonar.host.url=http://anotherHost");

                // Assert
                File.Exists(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeTrue();
                File.Exists(Path.Combine(TempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeTrue();

                logger.DebugMessages.Should().HaveCount(3);
                logger.DebugMessages[0].Should().Match("Cannot delete directory: '*\\.sonarqube\\bin' because The process cannot access the file 'SonarScanner.MSBuild.Common.dll' because it is being used by another process..");
                logger.DebugMessages[1].Should().Match("Cannot delete file: '*\\.sonarqube\\bin\\SonarScanner.MSBuild.Common.dll' because The process cannot access the file 'SonarScanner.MSBuild.Common.dll' because it is being used by another process..");
                logger.DebugMessages[2].Should().Match("Cannot delete file: '*\\.sonarqube\\bin\\SonarScanner.MSBuild.Tasks.dll' because The process cannot access the file 'SonarScanner.MSBuild.Tasks.dll' because it is being used by another process..");

                logger.AssertErrorLogged(@"Cannot copy a different version of the SonarScanner for MSBuild assemblies because they are used by a running MSBuild/.Net Core process. To resolve this problem try one of the following:
- Analyze this project using the same version of SonarScanner for MSBuild
- Build your project with the '/nr:false' switch");

                // Do not close before to ensure the file is locked
                file1.Close();
                file2.Close();
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
                var logger = CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, null, "/d:sonar.host.url=http://anotherHost");

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
                File.Exists(filePath).Should().BeTrue();

                // Act
                CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, null, "/d:sonar.host.url=http://anotherHost");

                // Assert
                File.Exists(filePath).Should().BeFalse();
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
                var logger = CheckExecutionSucceeds(AnalysisPhase.PostProcessing, false, null, "other params", "yet.more.params");

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
                var logger = CheckExecutionFails(AnalysisPhase.PostProcessing, false, null, "other params", "yet.more.params");

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

        private TestLogger CheckExecutionFails(AnalysisPhase phase, bool debug, Func<string, Version> getAssemblyVersion = null, params string[] args)
        {
            var logger = new TestLogger();
            var settings = MockBootstrapSettings(phase, debug, args);
            var bootstrapper = getAssemblyVersion != null
                ? new BootstrapperClass(MockProcessorFactory.Object, settings, logger, getAssemblyVersion)
                : new BootstrapperClass(MockProcessorFactory.Object, settings, logger);
            var exitCode = bootstrapper.Execute().Result;

            exitCode.Should().Be(Program.ErrorCode, "Bootstrapper did not return the expected exit code");
            logger.AssertErrorsLogged();

            return logger;
        }

        private TestLogger CheckExecutionSucceeds(AnalysisPhase phase, bool debug, Func<string, Version> getAssemblyVersion = null, params string[] args)
        {
            var logger = new TestLogger();
            var settings = MockBootstrapSettings(phase, debug, args);
            var bootstrapper = getAssemblyVersion != null
                ? new BootstrapperClass(MockProcessorFactory.Object, settings, logger, getAssemblyVersion)
                : new BootstrapperClass(MockProcessorFactory.Object, settings, logger);
            var exitCode = bootstrapper.Execute().Result;

            exitCode.Should().Be(0, "Bootstrapper did not return the expected exit code");
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
