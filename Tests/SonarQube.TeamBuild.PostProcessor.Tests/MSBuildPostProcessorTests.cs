/*
 * SonarQube Scanner for MSBuild
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarScanner.Shim;
using TestUtilities;

namespace SonarQube.TeamBuild.PostProcessor.Tests
{
    [TestClass]
    public class MSBuildPostProcessorTests
    {
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
            Assert.IsFalse(success, "Not expecting post-processor to have succeeded");

            context.CodeCoverage.AssertInitializedCalled();
            context.CodeCoverage.AssertExecuteCalled();
            context.Scanner.AssertNotExecuted();
            context.ReportBuilder.AssertNotExecuted();

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets(context.Logger));
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
            Assert.IsFalse(success, "Not expecting post-processor to have succeeded");

            context.CodeCoverage.AssertExecuteCalled();
            context.Scanner.AssertExecuted();
            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-scanner fails

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets(context.Logger));
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
            Assert.IsTrue(success, "Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertInitializedCalled();
            context.CodeCoverage.AssertExecuteCalled();
            context.Scanner.AssertExecuted();

            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-scanner fails

            CollectionAssert.AreEqual(new string[] { "-Dsonar.scanAllFiles=true" }, context.Scanner.SuppliedCommandLineArgs.ToArray(), "Unexpected command line args passed to the sonar-scanner");

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets(context.Logger));
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
            Assert.IsTrue(success, "Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertInitializedCalled();
            context.CodeCoverage.AssertExecuteCalled();
            context.Scanner.AssertExecuted();

            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-scanner fails

            CollectionAssert.AreEqual(new string[] { "-Dsonar.scanAllFiles=true" }, context.Scanner.SuppliedCommandLineArgs.ToArray(), "Unexpected command line args passed to the sonar-scanner");

            context.Logger.AssertErrorsLogged(1);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets(context.Logger));
        }

        [TestMethod]
        [Description("The coverage processing has 2 paths for fail - initialisation failures which are non-critical and processing errors that stop the post-processor workflow")]
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
            Assert.IsTrue(success, "Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertInitializedCalled();
            context.CodeCoverage.AssertExecuteNotCalled();
            context.Scanner.AssertExecuted();
            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-scanner fails

            CollectionAssert.AreEqual(new string[] {"-Dsonar.scanAllFiles=true" }, context.Scanner.SuppliedCommandLineArgs.ToArray(), "Unexpected command line args passed to the sonar-scanner");

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets(context.Logger));
        }

        [TestMethod]
        public void PostProc_FailsOnInvalidArgs()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);

            // Act
            var success = Execute(context, "/d:sonar.foo=bar");

            // Assert
            Assert.IsFalse(success, "Expecting post-processor to have failed");

            context.CodeCoverage.AssertInitialisedNotCalled();
            context.CodeCoverage.AssertExecuteNotCalled();
            context.Scanner.AssertNotExecuted();
            context.ReportBuilder.AssertNotExecuted();

            context.Logger.AssertErrorsLogged(1);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets(context.Logger));
        }

        [TestMethod]
        public void PostProc_ValidArgsPassedThrough()
        {
            // Arrange
            var context = new PostProcTestContext(TestContext);
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
            Assert.IsTrue(success, "Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertExecuteCalled();
            context.CodeCoverage.AssertInitializedCalled();
            context.Scanner.AssertExecuted();
            context.ReportBuilder.AssertExecuted();

            CollectionAssert.AreEqual(expectedArgs, context.Scanner.SuppliedCommandLineArgs.ToArray(), "Unexpected command line args passed to the sonar-runner");

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);

            // Verify that the method was called at least once
            context.TargetsUninstaller.Verify(m => m.UninstallTargets(context.Logger));
        }

        #endregion Tests

        /// <summary>
        /// Helper class that creates all of the necessary mocks
        /// </summary>
        private class PostProcTestContext
        {
            private readonly TestLogger logger;
            private readonly TeamBuildSettings settings;

            private readonly MockCodeCoverageProcessor codeCoverage;
            private readonly MockSonarScanner scanner;
            private readonly MockSummaryReportBuilder reportBuilder;

            public Mock<ITargetsUninstaller> TargetsUninstaller { get; }

            public PostProcTestContext(TestContext testContext)
            {
                Config = new AnalysisConfig();
                settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(testContext.DeploymentDirectory);

                logger = new TestLogger();
                codeCoverage = new MockCodeCoverageProcessor();
                scanner = new MockSonarScanner();
                reportBuilder = new MockSummaryReportBuilder();
                TargetsUninstaller = new Mock<ITargetsUninstaller>();
                var callCount = 0;
                TargetsUninstaller
                    .Setup(m => m.UninstallTargets(Logger))
                    .Callback(() =>
                    {
                        // Verify that the method was called maximum once
                        Assert.IsTrue(callCount == 0, "Method should be called exactly once");
                        callCount++;
                    });

                codeCoverage.InitialiseValueToReturn = true;
                codeCoverage.ProcessValueToReturn = true;
            }

            public AnalysisConfig Config { get; set; }
            public TeamBuildSettings Settings { get { return settings; } }
            public MockCodeCoverageProcessor CodeCoverage {  get { return codeCoverage; } }
            public MockSonarScanner Scanner { get { return scanner; } }
            public MockSummaryReportBuilder ReportBuilder { get { return reportBuilder; } }
            public TestLogger Logger { get { return logger; } }
        }

        #region Private methods

        private static bool Execute(PostProcTestContext context, params string[] args)
        {
            var proc = new MSBuildPostProcessor(context.CodeCoverage, context.Scanner, context.ReportBuilder, context.Logger, context.TargetsUninstaller.Object);
            var success = proc.Execute(args, context.Config, context.Settings);
            return success;
        }

        #endregion Private methods
    }
}
