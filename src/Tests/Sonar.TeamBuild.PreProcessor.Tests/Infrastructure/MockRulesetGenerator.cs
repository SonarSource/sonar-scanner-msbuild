//-----------------------------------------------------------------------
// <copyright file="MockRulesetGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockRulesetGenerator : IRulesetGenerator
    {
        private bool generateCalled;

        private string actualKey, actualFilePath;
        private SonarWebService actualWs;

        #region Assertions

        public void AssertGenerateCalled()
        {
            Assert.IsTrue(this.generateCalled, "Expecting Generate to have been called");
        }

        public void CheckGeneratorArguments(string expectedWsServer, string expectedKey)
        {
            Assert.AreEqual(expectedKey, this.actualKey);
            Assert.AreEqual(expectedWsServer, this.actualWs.Server);

            // The path should be a valid path to an existing file
            Assert.IsNotNull(this.actualFilePath, "Supplied file path should not be null");
        }

        #endregion

        #region IRulesetGenerator interface

        void IRulesetGenerator.Generate(SonarWebService ws, string sonarProjectKey, string outputFilePath)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(sonarProjectKey), "Supplied project key should not be null");
            Assert.IsFalse(string.IsNullOrWhiteSpace(outputFilePath), "Supplied output file path should not be null");

            this.generateCalled = true;

            this.actualKey = sonarProjectKey;
            this.actualFilePath = outputFilePath;
            this.actualWs = ws;
        }

        #endregion
    }
}
