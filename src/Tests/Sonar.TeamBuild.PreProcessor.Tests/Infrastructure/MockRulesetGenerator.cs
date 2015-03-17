//-----------------------------------------------------------------------
// <copyright file="MockRulesetGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sonar.FxCopRuleset;
using System.IO;

namespace Sonar.TeamBuild.PreProcessor.Tests
{
    internal class MockRulesetGenerator : IRulesetGenerator
    {
        private bool generateCalled;

        private string actualKey, actualFilePath, actualUrl, actualUserName, actualPassword;

        #region Assertions

        public void AssertGenerateCalled()
        {
            Assert.IsTrue(this.generateCalled, "Expecting Generate to have been called");
        }

        public void AssertGenerateNotCalled()
        {
            Assert.IsFalse(this.generateCalled, "Not expecting Generate to have been called");
        }

        public void CheckGeneratorArguments(string expectedKey, string expectedUrl, string expectedUserName, string expectedPassword)
        {
            Assert.AreEqual(expectedKey, this.actualKey);
            Assert.AreEqual(expectedUrl, this.actualUrl);
            Assert.AreEqual(expectedUserName, this.actualUserName);
            Assert.AreEqual(expectedPassword, this.actualPassword);

            // The path should be a valid path to an existing file
            Assert.IsNotNull(this.actualFilePath, "Supplied file path should not be null");
        }

        #endregion

        #region IRulesetGenerator interface

        void IRulesetGenerator.Generate(string sonarProjectKey, string outputFilePath, string sonarUrl, string userName, string password)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(sonarProjectKey), "Supplied project key should not be null");
            Assert.IsFalse(string.IsNullOrWhiteSpace(outputFilePath), "Supplied output file path should not be null");
            Assert.IsFalse(string.IsNullOrWhiteSpace(sonarUrl), "Supplied sonar url should not be null");

            this.generateCalled = true;

            this.actualKey = sonarProjectKey;
            this.actualFilePath = outputFilePath;
            this.actualUrl = sonarUrl;
            this.actualUserName = userName;
            this.actualPassword = password;
        }

        #endregion
    }
}
