//-----------------------------------------------------------------------
// <copyright file="BuildAssertions.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.MSBuild.Tasks.IntegrationTests
{
    public static class BuildAssertions
    {
        #region Assertions

        /// <summary>
        /// Checks that building the specified target succeeded.
        /// </summary>
        public static void AssertTargetSucceeded(BuildResult result, string target)
        {
            AssertExpectedTargetOutput(result, target, BuildResultCode.Success);
        }

        /// <summary>
        /// Checks that building the specified target failed.
        /// </summary>
        public static void AssertTargetFailed(BuildResult result, string target)
        {
            AssertExpectedTargetOutput(result, target, BuildResultCode.Failure);
        }

        /// <summary>
        /// Checks that building the specified target produced the expected result.
        /// </summary>
        public static void AssertExpectedTargetOutput(BuildResult result, string target, BuildResultCode resultCode)
        {
            DumpTargetResult(result, target);

            TargetResult targetResult;
            if (!result.ResultsByTarget.TryGetValue(target, out targetResult))
            {
                Assert.Inconclusive(@"Could not find result for target ""{0}""", target);
            }
            Assert.AreEqual<BuildResultCode>(resultCode, result.OverallResult, "Unexpected build result");
        }

        public static void AssertExpectedPropertyValue(ProjectInstance projectInstance, string propertyName, string expectedValue)
        {
            ProjectPropertyInstance propertyInstance = AssertPropertyExists(projectInstance, propertyName);
            Assert.AreEqual(expectedValue, propertyInstance.EvaluatedValue, "Property '{0}' does not have the expected value", propertyName);
        }

        public static ProjectPropertyInstance AssertPropertyExists(ProjectInstance projectInstance, string propertyName)
        {
            ProjectPropertyInstance propertyInstance = projectInstance.GetProperty(propertyName);
            Assert.IsNotNull(propertyInstance, "The expected property does not exist: {0}", propertyName);
            return propertyInstance;
        }

        public static void AssertPropertyDoesNotExist(ProjectInstance projectInstance, string propertyName)
        {
            ProjectPropertyInstance propertyInstance = projectInstance.GetProperty(propertyName);

            string value = propertyInstance == null ? null : propertyInstance.EvaluatedValue;

            Assert.IsNull(propertyInstance, "Not expecting the property to exist. Property: {0}, Value: {1}", propertyName, value);
        }

        /// <summary>
        /// Checks whether there is a single "SonarQubeSetting" item with the expected name and setting value
        /// </summary>
        public static void AssertExpectedAnalysisSetting(BuildResult actualResult, string settingName, string expectedValue)
        {
            /* The equivalent XML would look like this:
            <ItemGroup>
              <SonarQubeSetting Include="settingName">
                <Value>expectedValue</Value
              </SonarQubeSetting>
            </ItemGroup>
            */

            Assert.IsNotNull(actualResult.ProjectStateAfterBuild, "Test error: ProjectStateAfterBuild should not be null");

            IEnumerable<ProjectItemInstance> matches = actualResult.ProjectStateAfterBuild.GetItemsByItemTypeAndEvaluatedInclude(BuildTaskConstants.SettingItemName, settingName);

            Assert.AreNotEqual(0, matches.Count(), "Expected SonarQubeSetting with include value of '{0}' does not exist", settingName);
            Assert.AreEqual(1, matches.Count(), "Only expecting one SonarQubeSetting with include value of '{0}' to exist", settingName);

            ProjectItemInstance item = matches.Single();
            string value = item.GetMetadataValue(BuildTaskConstants.SettingValueMetadataName);

            Assert.AreEqual(expectedValue, value, "SonarQubeSetting with include value '{0}' does not have the expected value", settingName);

        }

        public static void AssertAnalysisSettingDoesNotExist(BuildResult actualResult, string settingName)
        {
            Assert.IsNotNull(actualResult.ProjectStateAfterBuild, "Test error: ProjectStateAfterBuild should not be null");

            IEnumerable<ProjectItemInstance> matches = actualResult.ProjectStateAfterBuild.GetItemsByItemTypeAndEvaluatedInclude(BuildTaskConstants.SettingItemName, settingName);

            Assert.AreEqual(0, matches.Count(), "Not expected SonarQubeSetting with include value of '{0}' to exist. Actual occurences: {1}", settingName, matches.Count());
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Writes the build and target output to the output stream
        /// </summary>
        public static void DumpTargetResult(BuildResult result, string target)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            BuildUtilities.LogMessage("Overall build result: {0}", result.OverallResult.ToString());

            TargetResult targetResult;
            if (!result.ResultsByTarget.TryGetValue(target, out targetResult))
            {
                BuildUtilities.LogMessage(@"Could not find result for target ""{0}""", target);
            }
            else
            {
                BuildUtilities.LogMessage(@"Results for target ""{0}""", target);
                BuildUtilities.LogMessage("\tTarget exception: {0}", targetResult.Exception == null ? "{null}" : targetResult.Exception.Message);
                BuildUtilities.LogMessage("\tTarget result: {0}", targetResult.ResultCode.ToString());
            }
        }

        #endregion
    }
}
