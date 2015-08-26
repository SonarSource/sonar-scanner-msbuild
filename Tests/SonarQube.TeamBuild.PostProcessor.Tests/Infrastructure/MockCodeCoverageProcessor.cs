//-----------------------------------------------------------------------
// <copyright file="MockCodeCoverageProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using SonarQube.TeamBuild.Integration;
using SonarQube.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace SonarQube.TeamBuild.PostProcessor.Tests
{
    internal class MockCodeCoverageProcessor : ICoverageReportProcessor
    {
        private bool processCoverageMethodCalled;
        private bool initalisedCalled;

        #region Test helpers

        public bool ProcessValueToReturn { get; set; }
        public bool InitialiseValueToReturn { get; set; }

        #endregion

        #region ICoverageReportProcessor interface

        public bool Initialise(AnalysisConfig context, TeamBuildSettings settings, ILogger logger)
        {
            Assert.IsFalse(this.initalisedCalled, "Expecting Initialise to be called only once");
            this.initalisedCalled = true;
            return InitialiseValueToReturn;
        }

        public bool ProcessCoverageReports()
        {
            Assert.IsFalse(this.processCoverageMethodCalled, "Expecting ProcessCoverageReports to be called only once");
            Assert.IsTrue(this.initalisedCalled, "Expecting Initialise to be called first");
            this.processCoverageMethodCalled = true;
            return ProcessValueToReturn;
        }

        #endregion

        #region Checks

        public void AssertExecuteCalled()
        {
            Assert.IsTrue(this.processCoverageMethodCalled, "Expecting the sonar-runner to have been called");
        }

        public void AssertExecuteNotCalled()
        {
            Assert.IsFalse(this.processCoverageMethodCalled, "Not expecting the sonar-runner to have been called");
        }

        public void AssertInitializedCalled()
        {
            Assert.IsTrue(this.initalisedCalled, "Expecting the sonar-runner to have been called");
        }

        public void AssertInitialisedNotCalled()
        {
            Assert.IsFalse(this.initalisedCalled, "Not expecting the sonar-runner to have been called");
        }


        #endregion

    }
}
