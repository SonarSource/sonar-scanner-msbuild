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
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.TFS;
using SonarScanner.MSBuild.TFS.Interfaces;
using SonarScanner.MSBuild.Shim;
using TestUtilities;

namespace SonarScanner.MSBuild.PostProcessor.Tests
{
    [TestClass]
    public class MSBuildPostProcessorTests
    {
        private const string CredentialsErrorMessage = "Credentials must be passed in both begin and end steps or not at all";

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PostProc_ExecutionFailsIfCodeCoverageFails()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.CodeCoverage.InitialiseValueToReturn = true;
            context.CodeCoverage.ProcessValueToReturn = false;

            // Act
            var success = Execute(context);

            // Assert
            success.Should().BeFalse("Not expecting post-processor to have succeeded");

            context.CodeCoverage.AssertInitializedCalled();
            context.CodeCoverage.AssertExecuteCalled();
            context.Scanner.AssertNotExecuted();
            context.ReportBuilder.AssertNotExecuted();

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets());
        }

        [TestMethod]
        public void PostProc_ExecutionFailsIfSonarScannerFails()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Scanner.ValueToReturn = new ProjectInfoAnalysisResult
            {
                RanToCompletion = false
            };

            // Act
            var success = Execute(context);

            // Assert
            success.Should().BeFalse("Not expecting post-processor to have succeeded");

            context.CodeCoverage.AssertExecuteCalled();
            context.Scanner.AssertExecuted();
            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-scanner fails

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets());
        }

        [TestMethod]
        public void PostProc_ExecutionSucceeds()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Scanner.ValueToReturn = new ProjectInfoAnalysisResult
            {
                RanToCompletion = true
            };

            // Act
            var success = Execute(context);

            // Assert
            success.Should().BeTrue("Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertInitializedCalled();
            context.CodeCoverage.AssertExecuteCalled();
            context.Scanner.AssertExecuted();

            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-scanner fails

            context.Scanner.SuppliedCommandLineArgs.Should().Equal(
                new string[] { "-Dsonar.scanAllFiles=true" },
                "Unexpected command line args passed to the sonar-scanner");

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets());
        }

        [TestMethod]
        public void PostProc_ExecutionSucceedsWithErrorLogs()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Scanner.ValueToReturn = new ProjectInfoAnalysisResult
            {
                RanToCompletion = true
            };
            context.Scanner.ErrorToLog = "Errors";

            // Act
            var success = Execute(context);

            // Assert
            success.Should().BeTrue("Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertInitializedCalled();
            context.CodeCoverage.AssertExecuteCalled();
            context.Scanner.AssertExecuted();

            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-scanner fails

            context.Scanner.SuppliedCommandLineArgs.Should().Equal(
                new string[] { "-Dsonar.scanAllFiles=true" },
                "Unexpected command line args passed to the sonar-scanner");

            context.Logger.AssertErrorsLogged(1);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets());
        }

        [TestMethod]
        [Description("The coverage processing has 2 paths for fail - initialization failures which are non-critical and processing errors that stop the post-processor workflow")]
        public void PostProc_ExecutionSucceedsIfCoverageNotInitialised()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.CodeCoverage.InitialiseValueToReturn = false;
            context.Scanner.ValueToReturn = new ProjectInfoAnalysisResult
            {
                RanToCompletion = true
            };

            // Act
            var success = Execute(context);

            // Assert
            success.Should().BeTrue("Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertInitializedCalled();
            context.CodeCoverage.AssertExecuteNotCalled();
            context.Scanner.AssertExecuted();
            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-scanner fails

            context.Scanner.SuppliedCommandLineArgs.Should().Equal(
                new string[] { "-Dsonar.scanAllFiles=true" },
                "Unexpected command line args passed to the sonar-scanner");

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets());
        }

        [TestMethod]
        public void PostProc_FailsOnInvalidArgs()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);

            // Act
            var success = Execute(context, "/d:sonar.foo=bar");

            // Assert
            success.Should().BeFalse("Expecting post-processor to have failed");

            context.CodeCoverage.AssertInitialisedNotCalled();
            context.CodeCoverage.AssertExecuteNotCalled();
            context.Scanner.AssertNotExecuted();
            context.ReportBuilder.AssertNotExecuted();

            context.Logger.AssertErrorsLogged(1);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets());
        }

        [TestMethod]
        public void PostProc_ValidArgsPassedThrough()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Config.HasBeginStepCommandLineCredentials = true;
            context.Scanner.ValueToReturn = new ProjectInfoAnalysisResult
            {
                RanToCompletion = true
            };

            var suppliedArgs = new string[]
            {
                "/d:sonar.jdbc.password=dbpwd",
                "/d:sonar.jdbc.username=dbuser",
                "/d:sonar.password=\"my pwd\"",
                "/d:sonar.login=login"
            };

            var expectedArgs = new string[]
            {
                "-Dsonar.jdbc.password=dbpwd",
                "-Dsonar.jdbc.username=dbuser",
                "-Dsonar.password=\"my pwd\"",
                "-Dsonar.login=login",
                "-Dsonar.scanAllFiles=true"
            };

            // Act
            var success = Execute(context, suppliedArgs);

            // Assert
            success.Should().BeTrue("Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertExecuteCalled();
            context.CodeCoverage.AssertInitializedCalled();
            context.Scanner.AssertExecuted();
            context.ReportBuilder.AssertExecuted();


            context.Scanner.SuppliedCommandLineArgs.Should().Equal(
                expectedArgs,
                "Unexpected command line args passed to the sonar-scanner");

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets());
        }

        [TestMethod]
        public void PostProc_WhenSettingInFileButNoCommandLineArg_Fail()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Config.HasBeginStepCommandLineCredentials = true;

            // Act
            var success = Execute(context, args: new string[0]);

            // Assert
            success.Should().BeFalse();
            context.Logger.AssertErrorLogged(CredentialsErrorMessage);

            context.CodeCoverage.AssertInitialisedNotCalled();
            context.CodeCoverage.AssertExecuteNotCalled();
            context.Scanner.AssertNotExecuted();
            context.ReportBuilder.AssertNotExecuted();

            context.TargetsUninstaller.Verify(m => m.UninstallTargets());
        }

        [TestMethod]
        public void PostProc_WhenNoSettingInFileAndCommandLineArg_Fail()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Config.AdditionalConfig = new List<ConfigSetting>();
            context.Scanner.ValueToReturn = new ProjectInfoAnalysisResult
            {
                RanToCompletion = true
            };

            // Act
            var success = Execute(context, args: "/d:sonar.login=foo");

            // Assert
            success.Should().BeFalse();
            context.Logger.AssertErrorLogged(CredentialsErrorMessage);

            context.CodeCoverage.AssertInitialisedNotCalled();
            context.CodeCoverage.AssertExecuteNotCalled();
            context.Scanner.AssertNotExecuted();
            context.ReportBuilder.AssertNotExecuted();

            context.TargetsUninstaller.Verify(m => m.UninstallTargets());
        }

        [TestMethod]
        public void PostProc_WhenNoSettingInFileAndNoCommandLineArg_DoesNotFail()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Config.AdditionalConfig = new List<ConfigSetting>();
            context.Scanner.ValueToReturn = new ProjectInfoAnalysisResult
            {
                RanToCompletion = true
            };

            // Act
            var success = Execute(context, args: new string[0]);

            // Assert
            success.Should().BeTrue();
            context.Logger.AssertErrorDoesNotExist(CredentialsErrorMessage);
        }

        [TestMethod]
        public void PostProc_WhenSettingInFileAndCommandLineArg_DoesNotFail()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
            context.Config.HasBeginStepCommandLineCredentials = true;
            context.Scanner.ValueToReturn = new ProjectInfoAnalysisResult
            {
                RanToCompletion = true
            };

            // Act
            var success = Execute(context, args: "/d:sonar.login=foo");

            // Assert
            success.Should().BeTrue();
            context.Logger.AssertErrorDoesNotExist(CredentialsErrorMessage);
        }

        [TestMethod]
        public void Execute_NullArgs_Throws()
        {
            Action action = () => DummyPostProcessorExecute(null, new AnalysisConfig(), new MockTeamBuildSettings());
            action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("args");
        }

        [TestMethod]
        public void Execute_NullAnalysisConfig_Throws()
        {
            Action action = () => DummyPostProcessorExecute(new string[0], null, new MockTeamBuildSettings());
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
                Settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(TestUtils.CreateTestSpecificFolderWithSubPaths(testContext));

                Logger = new TestLogger();
                CodeCoverage = new MockCodeCoverageProcessor();
                Scanner = new MockSonarScanner(Logger);
                ReportBuilder = new MockSummaryReportBuilder();
                TargetsUninstaller = new Mock<ITargetsUninstaller>();
                var callCount = 0;
                TargetsUninstaller
                    .Setup(m => m.UninstallTargets())
                    .Callback(() =>
                    {
                        // Verify that the method was called maximum once
                        callCount.Should().Be(0, "Method should be called exactly once");
                        callCount++;
                    });

                CodeCoverage.InitialiseValueToReturn = true;
                CodeCoverage.ProcessValueToReturn = true;
            }

            public AnalysisConfig Config { get; set; }
            public TeamBuildSettings Settings { get; }
            public MockCodeCoverageProcessor CodeCoverage { get; }
            public MockSonarScanner Scanner { get; }
            public MockSummaryReportBuilder ReportBuilder { get; }
            public TestLogger Logger { get; }
        }

        #region Private methods

        private static bool Execute(PostProcTestContext context, params string[] args)
        {
            var proc = new MSBuildPostProcessor(context.CodeCoverage, context.Scanner, context.ReportBuilder, context.Logger, context.TargetsUninstaller.Object);
            var success = proc.Execute(args, context.Config, context.Settings);
            return success;
        }

        private void DummyPostProcessorExecute(string[] args, AnalysisConfig config, ITeamBuildSettings settings)
        {
            var context = new PostProcTestContext(TestContext);
            var proc = new MSBuildPostProcessor(context.CodeCoverage, context.Scanner, context.ReportBuilder, context.Logger, context.TargetsUninstaller.Object);
            proc.Execute(args, config, settings);
        }

        #endregion Private methods
    }
}
