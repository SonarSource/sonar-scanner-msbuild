/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.PostProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor;
using TestUtilities;

namespace SonarScanner.MSBuild.Test
{
    [TestClass]
    public class BootstrapperClassTests
    {
        private const int ErrorCode = 1;
        private string rootDir;
        private string tempDir;
        private Mock<IProcessorFactory> mockProcessorFactory;
        private Mock<ITeamBuildPreProcessor> mockPreProcessor;
        private Mock<IMSBuildPostProcessor> mockPostProcessor;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void MyTestInitialize()
        {
            rootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            // this is the Temp folder used by Bootstrapper
            tempDir = Path.Combine(rootDir, ".sonarqube");
            // it will look in Directory.GetCurrentDir, which is RootDir.
            var analysisConfigFile = Path.Combine(tempDir, "conf", "SonarQubeAnalysisConfig.xml");
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
            mockPreProcessor = new Mock<ITeamBuildPreProcessor>();
            mockPostProcessor = new Mock<IMSBuildPostProcessor>();
            mockPreProcessor.Setup(x => x.Execute(It.IsAny<string[]>())).Returns(Task.FromResult(preProcessorOutcome));
            mockPostProcessor.Setup(x => x.Execute(It.IsAny<string[]>(), It.IsAny<AnalysisConfig>(), It.IsAny<ITeamBuildSettings>())).Returns(postProcessorOutcome);
            mockProcessorFactory = new Mock<IProcessorFactory>();
            mockProcessorFactory.Setup(x => x.CreatePostProcessor()).Returns(mockPostProcessor.Object);
            mockProcessorFactory.Setup(x => x.CreatePreProcessor()).Returns(mockPreProcessor.Object);
        }

        #region Tests

        [TestMethod]
        public void Exe_PreProcFails()
        {
            // Arrange
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();

            using (InitializeNonTeamBuildEnvironment(rootDir))
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

            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                // Sanity
                File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeFalse();
                File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeFalse();

                // Act
                CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, null, "/d:sonar.host.url=http://anotherHost");

                // Assert
                File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeTrue();
                File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeTrue();
            }
        }

        [TestMethod]
        public void CopyDlls_WhenFileExistButAreNotLocked_FilesAreCopied()
        {
            // Arrange
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();

            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                Directory.CreateDirectory(Path.Combine(tempDir, "bin"));
                File.Create(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Close();
                File.Create(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Close();

                // Act
                CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, null, "/d:sonar.host.url=http://anotherHost");

                // Assert
                File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeTrue();
                File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeTrue();
            }
        }

        [TestMethod]
        public void CopyDlls_WhenFileExistAndAreLockedButSameVersion_DoNothing()
        {
            // Arrange
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();

            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                Directory.CreateDirectory(Path.Combine(tempDir, "bin"));
                var file1 = File.Create(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll"));
                var file2 = File.Create(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll"));

                // Act
                CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, _ => new Version(), "/d:sonar.host.url=http://anotherHost");

                // Assert
                File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeTrue();
                File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeTrue();

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

            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                Directory.CreateDirectory(Path.Combine(tempDir, "bin"));
                var file1 = File.Create(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll"));
                var file2 = File.Create(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll"));

                var callCount = 0;
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
                File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeTrue();
                File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeTrue();

                logger.DebugMessages.Should().HaveCount(3);
                logger.DebugMessages[0].Should().Match(@"Cannot delete directory: '*\.sonarqube\bin' because The process cannot access the file 'SonarScanner.MSBuild.Common.dll' because it is being used by another process..");
                logger.DebugMessages[1].Should().Match(@"Cannot delete file: '*\.sonarqube\bin\SonarScanner.MSBuild.Common.dll' because The process cannot access the file '*SonarScanner.MSBuild.Common.dll' because it is being used by another process..");
                logger.DebugMessages[2].Should().Match(@"Cannot delete file: '*\.sonarqube\bin\SonarScanner.MSBuild.Tasks.dll' because The process cannot access the file '*SonarScanner.MSBuild.Tasks.dll' because it is being used by another process..");

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

            using (InitializeNonTeamBuildEnvironment(rootDir))
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

            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                // Create dummy file in Temp
                var filePath = Path.Combine(tempDir, "myfile");
                Directory.CreateDirectory(tempDir);
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
            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                MockProcessors(true, false);
                // this is usually created by the PreProcessor
                Directory.CreateDirectory(tempDir);

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
            Directory.Delete(tempDir, true);

            using (InitializeNonTeamBuildEnvironment(rootDir))
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
            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                // this is usually created by the PreProcessor
                Directory.CreateDirectory(tempDir);

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
            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                // this is usually created by the PreProcessor
                Directory.CreateDirectory(tempDir);

                var analysisConfigFile = Path.Combine(tempDir, "conf", "SonarQubeAnalysisConfig.xml");
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
                ? new BootstrapperClass(mockProcessorFactory.Object, settings, logger, getAssemblyVersion)
                : new BootstrapperClass(mockProcessorFactory.Object, settings, logger);
            var exitCode = bootstrapper.Execute().Result;

            exitCode.Should().Be(ErrorCode, "Bootstrapper did not return the expected exit code");
            logger.AssertErrorsLogged();

            return logger;
        }

        private TestLogger CheckExecutionSucceeds(AnalysisPhase phase, bool debug, Func<string, Version> getAssemblyVersion = null, params string[] args)
        {
            var logger = new TestLogger();
            var settings = MockBootstrapSettings(phase, debug, args);
            var bootstrapper = getAssemblyVersion != null
                ? new BootstrapperClass(mockProcessorFactory.Object, settings, logger, getAssemblyVersion)
                : new BootstrapperClass(mockProcessorFactory.Object, settings, logger);
            var exitCode = bootstrapper.Execute().Result;

            exitCode.Should().Be(0, "Bootstrapper did not return the expected exit code");
            logger.AssertErrorsLogged(0);

            return logger;
        }

        private IBootstrapperSettings MockBootstrapSettings(AnalysisPhase phase, bool debug, string[] args)
        {
            Mock<IBootstrapperSettings> mockBootstrapSettings;

            File.Create(Path.Combine(rootDir, "SonarScanner.MSBuild.Common.dll")).Close();
            File.Create(Path.Combine(rootDir, "SonarScanner.MSBuild.Tasks.dll")).Close();

            mockBootstrapSettings = new Mock<IBootstrapperSettings>();
            mockBootstrapSettings.SetupGet(x => x.ChildCmdLineArgs).Returns(args.ToArray);
            mockBootstrapSettings.SetupGet(x => x.TempDirectory).Returns(tempDir);
            mockBootstrapSettings.SetupGet(x => x.Phase).Returns(phase);
            mockBootstrapSettings.SetupGet(x => x.ScannerBinaryDirPath).Returns(rootDir);
            mockBootstrapSettings.SetupGet(x => x.LoggingVerbosity).Returns(debug ? LoggerVerbosity.Debug : LoggerVerbosity.Info);

            return mockBootstrapSettings.Object;
        }

        private void AssertPostProcessorNotCalled() =>
            mockPostProcessor.Verify(x => x.Execute(It.IsAny<string[]>(), It.IsAny<AnalysisConfig>(), It.IsAny<ITeamBuildSettings>()), Times.Never());

        private void AssertPostProcessorArgs(params string[] expectedArgs) =>
            mockPostProcessor.Verify(x => x.Execute(expectedArgs, It.IsAny<AnalysisConfig>(), It.IsAny<ITeamBuildSettings>()), Times.Once());

        private void AssertPreProcessorArgs(params string[] expectedArgs) =>
            mockPreProcessor.Verify(x => x.Execute(expectedArgs), Times.Once());

        #endregion Checks
    }
}
