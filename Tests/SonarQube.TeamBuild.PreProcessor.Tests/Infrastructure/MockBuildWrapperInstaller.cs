//-----------------------------------------------------------------------
// <copyright file="MockBuildWrapperInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    public class MockBuildWrapperInstaller : IBuildWrapperInstaller
    {

        private int callCount;

        #region Test helpers

        public void AssertExpectedCallCount(int expected)
        {
            Assert.AreEqual(expected, this.callCount, "BuildWrapperInstaller was not called the expected number of times");
        }

        #endregion

        #region IBuildWrapperInstaller methods

        AnalysisProperties IBuildWrapperInstaller.InstallBuildWrapper(ISonarQubeServer server, string binDirectory, string outputDirectory)
        {
            Assert.IsNotNull(server, "Supplied server should not be null");
            Assert.IsFalse(string.IsNullOrWhiteSpace(binDirectory), "Supplied bin directory should not be null or empty");
            Assert.IsFalse(string.IsNullOrWhiteSpace(outputDirectory), "Supplied output directory should not be null or empty");

            this.callCount++;

            return new AnalysisProperties();
        }

        #endregion
    }
}
