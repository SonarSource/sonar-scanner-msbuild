//-----------------------------------------------------------------------
// <copyright file="MockSonarRunner.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarRunner.Shim;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.PostProcessor.Tests
{
    internal class MockSonarRunner : ISonarRunner
    {
        private bool methodCalled;

        #region Test Helpers

        public ProjectInfoAnalysisResult ValueToReturn { get; set; }

        public IEnumerable<string> SuppliedCommandLineArgs { get; set; }

        #endregion

        #region ISonarRunner interface

        public ProjectInfoAnalysisResult Execute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger)
        {
            Assert.IsFalse(this.methodCalled, "Runner should only be called once");
            this.methodCalled = true;
            this.SuppliedCommandLineArgs = userCmdLineArguments;

            return this.ValueToReturn;
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
