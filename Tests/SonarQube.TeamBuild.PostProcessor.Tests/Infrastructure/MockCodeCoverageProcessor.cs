//-----------------------------------------------------------------------
// <copyright file="MockCodeCoverageProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using SonarQube.TeamBuild.Integration;
using SonarQube.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarQube.TeamBuild.PostProcessor.Tests
{
    internal class MockCodeCoverageProcessor : ICoverageReportProcessor
    {
        private bool methodCalled;

        #region Test helpers

        public bool ValueToReturn { get; set; }

        #endregion

        #region ICoverageReportProcessor interface

        public bool ProcessCoverageReports(AnalysisConfig context, TeamBuildSettings settings, ILogger logger)
        {
            Assert.IsFalse(this.methodCalled, "Expecting ProcessCoverageReports to be called only once");
            this.methodCalled = true;
            return ValueToReturn;
        }

        #endregion

        #region Checks

        public void AssertExecuted()
        {
            Assert.IsTrue(this.methodCalled, "Expecting the sonar-runner to have been called");
        }

        public void AssertNotExecuted()
        {
            Assert.IsFalse(this.methodCalled, "Not expecting the sonar-runner to have been called");
        }

        #endregion

    }
}
