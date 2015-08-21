//-----------------------------------------------------------------------
// <copyright file="MSBuildPostProcessorTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarRunner.Shim;
using TestUtilities;
using System.Linq;

namespace SonarQube.TeamBuild.PostProcessor.Tests
{
    [TestClass]
    public class MSBuildPostProcessorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PostProc_CannotFindConfig()
        {
            // Arrange
            PostProcTestContext context = new PostProcTestContext(this.TestContext);
            context.Config = null;

            // Act
            bool success = Execute(context);

            // Assert
            Assert.IsFalse(success, "Not expecting post-processor to have succeeded");

            context.CodeCoverage.AssertNotExecuted();
            context.Runner.AssertNotExecuted();
            context.ReportBuilder.AssertNotExecuted();

            context.Logger.AssertErrorsLogged(1);
        }

        [TestMethod]
        public void PostProc_ExecutionFailsIfCodeCoverageFails()
        {
            // Arrange
            PostProcTestContext context = new PostProcTestContext(this.TestContext);
            context.CodeCoverage.ValueToReturn = false;

            // Act
            bool success = Execute(context);

            // Assert
            Assert.IsFalse(success, "Not expecting post-processor to have succeeded");

            context.CodeCoverage.AssertExecuted();
            context.Runner.AssertNotExecuted();
            context.ReportBuilder.AssertNotExecuted();

            context.Logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        public void PostProc_ExecutionFailsIfSonarRunnerFails()
        {
            // Arrange
            PostProcTestContext context = new PostProcTestContext(this.TestContext);
            context.CodeCoverage.ValueToReturn = true;
            context.Runner.ValueToReturn = new ProjectInfoAnalysisResult();
            context.Runner.ValueToReturn.RanToCompletion = false;

            // Act
            bool success = Execute(context);

            // Assert
            Assert.IsFalse(success, "Not expecting post-processor to have succeeded");

            context.CodeCoverage.AssertExecuted();
            context.Runner.AssertExecuted();
            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-runner fails

            context.Logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        public void PostProc_ExecutionSucceeds()
        {
            // Arrange
            PostProcTestContext context = new PostProcTestContext(this.TestContext);
            context.CodeCoverage.ValueToReturn = true;
            context.Runner.ValueToReturn = new ProjectInfoAnalysisResult();
            context.Runner.ValueToReturn.RanToCompletion = true;

            // Act
            bool success = Execute(context);

            // Assert
            Assert.IsTrue(success, "Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertExecuted();
            context.Runner.AssertExecuted();
            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-runner fails

            CollectionAssert.AreEqual(new string[] { }, context.Runner.SuppliedCommandLineArgs.ToArray(), "Unexpected command line args passed to the sonar-runner");

            context.Logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        public void PostProc_FailsOnInvalidArgs()
        {
            // Arrange
            PostProcTestContext context = new PostProcTestContext(this.TestContext);

            // Act
            bool success = Execute(context, "/d:sonar.foo=bar");

            // Assert
            Assert.IsFalse(success, "Expecting post-processor to have failed");

            context.CodeCoverage.AssertNotExecuted();
            context.Runner.AssertNotExecuted();
            context.ReportBuilder.AssertNotExecuted();

            context.Logger.AssertErrorsLogged(1);
        }

        [TestMethod]
        public void PostProc_ValidArgsPassedThrough()
        {
            // Arrange
            PostProcTestContext context = new PostProcTestContext(this.TestContext);
            context.CodeCoverage.ValueToReturn = true;
            context.Runner.ValueToReturn = new ProjectInfoAnalysisResult();
            context.Runner.ValueToReturn.RanToCompletion = true;

            string[] suppliedArgs = new string[]
            {
                "/d:sonar.jdbc.password=dbpwd",
                "/d:sonar.jdbc.username=dbuser",
                "/d:sonar.password=pwd",
                "/d:sonar.login=login"
            };

            // Act
            bool success = Execute(context, suppliedArgs);

            // Assert
            Assert.IsTrue(success, "Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertExecuted();
            context.Runner.AssertExecuted();
            context.ReportBuilder.AssertExecuted();

            CollectionAssert.AreEqual(suppliedArgs, context.Runner.SuppliedCommandLineArgs.ToArray(), "Unexpected command line args passed to the sonar-runner");

            context.Logger.AssertErrorsLogged(0);
        }

        #endregion

        /// <summary>
        /// Helper class that creates all of the necessary mocks
        /// </summary>
        private class PostProcTestContext
        {
            private readonly TestLogger logger;
            private readonly TeamBuildSettings settings;

            private readonly MockCodeCoverageProcessor codeCoverage;
            private readonly MockSonarRunner runner;
            private readonly MockSummaryReportBuilder reportBuilder;

            public PostProcTestContext(TestContext testContext)
            {
                this.Config = new AnalysisConfig();
                this.settings = TeamBuildSettings.CreateNonTeamBuildSettings(testContext.DeploymentDirectory);

                this.logger = new TestLogger();
                this.codeCoverage = new MockCodeCoverageProcessor();
                this.runner = new MockSonarRunner();
                this.reportBuilder = new MockSummaryReportBuilder();
            }

            public AnalysisConfig Config { get; set; }
            public TeamBuildSettings Settings { get { return this.settings; } }
            public MockCodeCoverageProcessor CodeCoverage {  get { return this.codeCoverage; } }
            public MockSonarRunner Runner { get { return this.runner; } }
            public MockSummaryReportBuilder ReportBuilder { get { return this.reportBuilder; } }
            public TestLogger Logger { get { return this.logger; } }
        }

        #region Private methods

        private static bool Execute(PostProcTestContext context, params string[] args)
        {
            MSBuildPostProcessor proc = new MSBuildPostProcessor(context.CodeCoverage, context.Runner, context.ReportBuilder);
            bool success = proc.Execute(args, context.Config, context.Settings, context.Logger);
            return success;
        }

        #endregion
    }
}
