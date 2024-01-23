/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
using TestUtilities;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.Shim;
using SonarScanner.MSBuild.Shim.Interfaces;
using System.IO;
using System.Linq;

namespace SonarScanner.MSBuild.PostProcessor.Test
{
    [TestClass]
    public class PostProcessorTests
    {
        private const string CredentialsErrorMessage = "Credentials must be passed in both begin and end steps or not at all";

        public TestContext TestContext { get; set; }

        #region Tests


        [TestMethod]
        public void PostProc_NoProjectsToAnalyze_NoExecutionTriggered()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Config.SonarOutputDir = Environment.CurrentDirectory;
            context.Config.SonarConfigDir = Environment.CurrentDirectory;
            context.Config.SonarQubeHostUrl = "http://sonarqube.com";
            context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
            context.Scanner.ValueToReturn = true;
            context.TfsProcessor.ValueToReturn = true;

            // Act
            var success = Execute_WithNoProject(context, true);

            // Assert
            success.Should().BeFalse("Expecting post-processor to have failed");
            context.TfsProcessor.AssertNotExecuted();
            context.Scanner.AssertNotExecuted();
            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);
            context.VerifyTargetsUninstaller();
        }

        [TestMethod]
        public void PostProc_ExecutionSucceedsWithErrorLogs()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Config.SonarOutputDir = Environment.CurrentDirectory;
            context.Config.SonarConfigDir = Environment.CurrentDirectory;
            context.Config.SonarQubeHostUrl = "http://sonarqube.com";
            context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
            context.Scanner.ValueToReturn = true;
            context.TfsProcessor.ValueToReturn = true;
            context.Scanner.ErrorToLog = "Errors";

            // Act
            var success = Execute(context, true);

            // Assert
            success.Should().BeTrue("Expecting post-processor to have succeeded");

            context.Scanner.AssertExecuted();
            context.Scanner.SuppliedCommandLineArgs.Should().Equal(new string[] { "-Dsonar.scanAllFiles=true" }, "Unexpected command line args passed to the sonar-scanner");
            context.Logger.AssertErrorsLogged(1);
            context.Logger.AssertWarningsLogged(0);
            context.VerifyTargetsUninstaller();
        }

        [TestMethod]
        public void PostProc_FailsOnInvalidArgs()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Config.SonarOutputDir = Environment.CurrentDirectory;
            context.TfsProcessor.ValueToReturn = false;
            context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;

            // Act
            var success = Execute(context, true, "/d:sonar.foo=bar");

            // Assert
            success.Should().BeFalse("Expecting post-processor to have failed");

            context.TfsProcessor.AssertNotExecuted();
            context.Scanner.AssertNotExecuted();
            context.Logger.AssertErrorsLogged(1);
            context.Logger.AssertWarningsLogged(0);
            context.VerifyTargetsUninstaller();
        }

        [TestMethod]
        public void PostProc_ValidArgsPassedThrough()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Config.HasBeginStepCommandLineCredentials = true;
            context.Config.SonarOutputDir = Environment.CurrentDirectory;
            context.Config.SonarConfigDir = Environment.CurrentDirectory;
            context.Config.SonarQubeHostUrl = "http://sonarqube.com";
            context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
            context.Scanner.ValueToReturn = true;
            context.TfsProcessor.ValueToReturn = true;

            var suppliedArgs = new[]
            {
                "/d:sonar.password=\"my pwd\"",
                "/d:sonar.login=login",
                "/d:sonar.token=token",
            };

            var expectedArgs = new[]
            {
                "-Dsonar.password=\"my pwd\"",
                "-Dsonar.login=login",
                "-Dsonar.token=token",
                "-Dsonar.scanAllFiles=true"
            };

            // Act
            var success = Execute(context, true, suppliedArgs);

            // Assert
            success.Should().BeTrue("Expecting post-processor to have succeeded");

            context.Scanner.AssertExecuted();
            context.Scanner.SuppliedCommandLineArgs.Should().Equal(expectedArgs, "Unexpected command line args passed to the sonar-scanner");
            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);
            context.VerifyTargetsUninstaller();
        }

        [TestMethod]
        public void PostProc_WhenSettingInFileButNoCommandLineArg_Fail()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Config.HasBeginStepCommandLineCredentials = true;
            context.Config.SonarQubeHostUrl = "http://sonarqube.com";
            context.Config.SonarQubeHostUrl = "http://sonarqube.com";
            context.TfsProcessor.ValueToReturn = false;

            // Act
            var success = Execute(context, true, args: new string[0]);

            // Assert
            success.Should().BeFalse();
            context.Logger.AssertErrorLogged(CredentialsErrorMessage);
            context.TfsProcessor.AssertNotExecuted();
            context.Scanner.AssertNotExecuted();
            context.VerifyTargetsUninstaller();
        }

        [TestMethod]
        public void PostProc_WhenNoSettingInFileAndCommandLineArg_Fail()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Config.SonarOutputDir = Environment.CurrentDirectory;
            context.Config.SonarQubeHostUrl = "http://sonarqube.com";
            context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
            context.Config.AdditionalConfig = new List<ConfigSetting>();
            context.Scanner.ValueToReturn = true;
            context.TfsProcessor.ValueToReturn = false;

            // Act
            var success = Execute(context, true, args: "/d:sonar.token=foo");

            // Assert
            success.Should().BeFalse();
            context.Logger.AssertErrorLogged(CredentialsErrorMessage);
            context.TfsProcessor.AssertNotExecuted();
            context.Scanner.AssertNotExecuted();
            context.VerifyTargetsUninstaller();
        }

        [TestMethod]
        public void PostProc_WhenNoSettingInFileAndNoCommandLineArg_DoesNotFail()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Config.SonarOutputDir = Environment.CurrentDirectory;
            context.Config.SonarConfigDir = Environment.CurrentDirectory;
            context.Config.SonarQubeHostUrl = "http://sonarqube.com";
            context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
            context.Config.AdditionalConfig = new List<ConfigSetting>();
            context.Scanner.ValueToReturn = true;

            // Act
            var success = Execute(context, true, args: new string[0]);

            // Assert
            success.Should().BeTrue();
            context.Logger.AssertNoErrorsLogged(CredentialsErrorMessage);
        }

        [TestMethod]
        public void PostProc_WhenSettingInFileAndCommandLineArg_DoesNotFail()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Config.HasBeginStepCommandLineCredentials = true;
            context.Config.SonarConfigDir = Environment.CurrentDirectory;
            context.Config.SonarQubeHostUrl = "http://sonarqube.com";
            context.Config.SonarOutputDir = Environment.CurrentDirectory;
            context.Config.SonarScannerWorkingDirectory = Environment.CurrentDirectory;
            context.Scanner.ValueToReturn = true;

            // Act
            var success = Execute(context, true, args: "/d:sonar.token=foo");

            // Assert
            success.Should().BeTrue();
            context.Logger.AssertNoErrorsLogged(CredentialsErrorMessage);
        }

        [TestMethod]
        public void Execute_NullArgs_Throws()
        {
            Action action = () => DummyPostProcessorExecute(null, new AnalysisConfig(), new MockBuildSettings());
            action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("args");
        }

        [TestMethod]
        public void Execute_NullAnalysisConfig_Throws()
        {
            Action action = () => DummyPostProcessorExecute(new string[0], null, new MockBuildSettings());
            action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
        }

        [TestMethod]
        public void Execute_NullTeamBuildSettings_Throws()
        {
            Action action = () => DummyPostProcessorExecute(new string[0], new AnalysisConfig(), null);
            action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("settings");
        }

        #endregion Tests

        /// <summary>
        /// Helper class that creates all of the necessary mocks
        /// </summary>
        private class PostProcTestContext
        {
            public Mock<ITargetsUninstaller> TargetsUninstaller { get; }

            public PostProcTestContext(TestContext testContext)
            {
                Config = new AnalysisConfig();
                Settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(TestUtils.CreateTestSpecificFolderWithSubPaths(testContext));
                Logger = new TestLogger();
                TfsProcessor = new MockTfsProcessor(Logger);
                Scanner = new MockSonarScanner(Logger);
                TargetsUninstaller = new Mock<ITargetsUninstaller>();
            }

            public AnalysisConfig Config { get; set; }
            public BuildSettings Settings { get; }
            public MockSonarScanner Scanner { get; }
            public TestLogger Logger { get; }
            public MockTfsProcessor TfsProcessor { get; }

            public void VerifyTargetsUninstaller() =>
                TargetsUninstaller.Verify(x => x.UninstallTargets(It.IsAny<string>()), Times.Once());
        }

        #region Private methods

        private bool Execute_WithNoProject(PostProcTestContext context, bool propertyWriteSucceeded, params string[] args)
        {
            var sonarProjectPropertiesValidator = new Mock<ISonarProjectPropertiesValidator>();

            IEnumerable<string> expectedValue;

            sonarProjectPropertiesValidator
                .Setup(propValidator => propValidator.AreExistingSonarPropertiesFilesPresent(It.IsAny<string>(), It.IsAny<ICollection<ProjectData>>(), out expectedValue)).Returns(false);

            var proc = new PostProcessor(context.Scanner, context.Logger, context.TargetsUninstaller.Object, context.TfsProcessor, sonarProjectPropertiesValidator.Object);

            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var projectInfo = TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);

            List<ProjectData> listOfProjects = new List<ProjectData>();
            listOfProjects.Add(new ProjectData(ProjectInfo.Load(projectInfo)));

            IEnumerable<ProjectData> expectedListOfProjects = Enumerable.Empty<ProjectData>();

            var propertiesFileGenerator = Mock.Of<IPropertiesFileGenerator>();
            Mock.Get(propertiesFileGenerator).Setup(m => m.TryWriteProperties(It.IsAny<PropertiesWriter>(), out expectedListOfProjects)).Returns(propertyWriteSucceeded);

            var projectInfoAnalysisResult = new ProjectInfoAnalysisResult();
            projectInfoAnalysisResult.Projects.AddRange(listOfProjects);
            projectInfoAnalysisResult.RanToCompletion = true;
            projectInfoAnalysisResult.FullPropertiesFilePath = null;

            Mock.Get(propertiesFileGenerator).Setup(m => m.GenerateFile()).Returns(projectInfoAnalysisResult);
            proc.SetPropertiesFileGenerator(propertiesFileGenerator);
            var success = proc.Execute(args, context.Config, context.Settings);
            return success;
        }

        private bool Execute(PostProcTestContext context, bool propertyWriteSucceeded, params string[] args)
        {
            var sonarProjectPropertiesValidator = new Mock<ISonarProjectPropertiesValidator>();

            IEnumerable<string> expectedValue;

            sonarProjectPropertiesValidator
                .Setup(propValidator => propValidator.AreExistingSonarPropertiesFilesPresent(It.IsAny<string>(), It.IsAny<ICollection<ProjectData>>(), out expectedValue)).Returns(false);

            var proc = new PostProcessor(context.Scanner, context.Logger, context.TargetsUninstaller.Object, context.TfsProcessor, sonarProjectPropertiesValidator.Object);

            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, Guid.NewGuid().ToString());

            var projectInfo = TestUtils.CreateProjectWithFiles(TestContext, "withFiles1", testDir);

            List<ProjectData> listOfProjects = new List<ProjectData>();
            listOfProjects.Add(new ProjectData(ProjectInfo.Load(projectInfo)));

            IEnumerable<ProjectData> expectedListOfProjects = listOfProjects;

            var propertiesFileGenerator = Mock.Of<IPropertiesFileGenerator>();
            Mock.Get(propertiesFileGenerator).Setup(m => m.TryWriteProperties(It.IsAny<PropertiesWriter>(), out expectedListOfProjects)).Returns(propertyWriteSucceeded);

            var projectInfoAnalysisResult = new ProjectInfoAnalysisResult();
            projectInfoAnalysisResult.Projects.AddRange(listOfProjects);
            projectInfoAnalysisResult.RanToCompletion = true;
            projectInfoAnalysisResult.FullPropertiesFilePath = Path.Combine(testDir, "sonar-project.properties");

            Mock.Get(propertiesFileGenerator).Setup(m => m.GenerateFile()).Returns(projectInfoAnalysisResult);
            proc.SetPropertiesFileGenerator(propertiesFileGenerator);
            var success = proc.Execute(args, context.Config, context.Settings);
            return success;
        }

        private void DummyPostProcessorExecute(string[] args, AnalysisConfig config, IBuildSettings settings)
        {
            var context = new PostProcTestContext(TestContext);
            var sonarProjectPropertiesValidator = new Mock<ISonarProjectPropertiesValidator>();

            var proc = new PostProcessor(context.Scanner, context.Logger, context.TargetsUninstaller.Object, context.TfsProcessor, sonarProjectPropertiesValidator.Object);
            proc.Execute(args, config, settings);
        }

        #endregion Private methods
    }
}
