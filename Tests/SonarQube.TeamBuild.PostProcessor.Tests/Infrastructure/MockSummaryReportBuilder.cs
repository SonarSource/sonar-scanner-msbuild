//-----------------------------------------------------------------------
// <copyright file="MockSummaryReportBuilder.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarScanner.Shim;

namespace SonarQube.TeamBuild.PostProcessor.Tests
{
    internal class MockSummaryReportBuilder : ISummaryReportBuilder
    {
        private bool methodCalled;

        #region ISummaryReportBuilder interface

        public void GenerateReports(TeamBuildSettings settings, AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
        {
            Assert.IsFalse(methodCalled, "Generate reports has already been called");

            this.methodCalled = true;
        }

        #endregion

        #region Checks

        public void AssertExecuted()
        {
            Assert.IsTrue(this.methodCalled, "Expecting ISummaryReportBuilder.GenerateReports to have been called");
        }

        public void AssertNotExecuted()
        {
            Assert.IsFalse(this.methodCalled, "Not expecting ISummaryReportBuilder.GenerateReports to have been called");
        }

        #endregion
    }
}
