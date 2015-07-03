//-----------------------------------------------------------------------
// <copyright file="MockRulesetGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockRulesetGenerator : IRulesetGenerator
    {
        private List<Tuple<SonarWebService, string, string, string, string, string>> actuals = new List<Tuple<SonarWebService, string, string, string, string, string>>();

        #region Assertions

        public void AssertGenerateCalled(int times)
        {
            Assert.AreEqual(times, actuals.Count, "Expecting Generate to have been called exactly " + times + " times instead of " + actuals.Count);
        }

        public void CheckGeneratorArguments(string expectedWsServer, string expectedPluginKey, string expectedLanguage, string expectedFxcopRepositoryKey, string expectedKey, string expectedPathEnding)
        {
            Assert.IsTrue(
                actuals.Any(
                actual =>
                    actual.Item1.Server == expectedWsServer &&
                    actual.Item2 == expectedPluginKey &&
                    actual.Item3 == expectedLanguage &&
                    actual.Item4 == expectedFxcopRepositoryKey &&
                    actual.Item5 == expectedKey &&
                    actual.Item6.EndsWith(expectedPathEnding)),
                "Could not find a matching actual Generate invocation");
        }

        #endregion

        #region IRulesetGenerator interface

        void IRulesetGenerator.Generate(SonarWebService ws, string requiredPluginKey, string language, string fxcopRepositoryKey, string sonarProjectKey, string outputFilePath)
        {
            Assert.IsNotNull(ws);
            Assert.IsFalse(string.IsNullOrWhiteSpace(requiredPluginKey), "Supplied requiredPluginKey should not be null or empty");
            Assert.IsFalse(string.IsNullOrWhiteSpace(language), "Supplied language should not be null or empty");
            Assert.IsFalse(string.IsNullOrWhiteSpace(fxcopRepositoryKey), "Supplied FxCop repository key should not be null or empty");
            Assert.IsFalse(string.IsNullOrWhiteSpace(sonarProjectKey), "Supplied project key should not be null or empty");
            Assert.IsFalse(string.IsNullOrWhiteSpace(outputFilePath), "Supplied output file path should not be null or empty");

            actuals.Add(Tuple.Create(ws, requiredPluginKey, language, fxcopRepositoryKey, sonarProjectKey, outputFilePath));
        }

        #endregion
    }
}
