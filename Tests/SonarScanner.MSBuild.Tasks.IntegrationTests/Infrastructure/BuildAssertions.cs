/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests
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

            if (!result.ResultsByTarget.TryGetValue(target, out TargetResult targetResult))
            {
                Assert.Inconclusive(@"Could not find result for target ""{0}""", target);
            }
            Assert.AreEqual<BuildResultCode>(resultCode, result.OverallResult, "Unexpected build result");
        }

        public static void AssertExpectedPropertyValue(ProjectInstance projectInstance, string propertyName, string expectedValue)
        {
            var propertyInstance = projectInstance.GetProperty(propertyName);
            if (expectedValue == null &&
                propertyInstance == null)
            {
                return;
            }

            Assert.IsNotNull(propertyInstance, "The expected property does not exist: {0}", propertyName);
            Assert.AreEqual(expectedValue, propertyInstance.EvaluatedValue, "Property '{0}' does not have the expected value", propertyName);
        }

        public static void AssertPropertyDoesNotExist(ProjectInstance projectInstance, string propertyName)
        {
            var propertyInstance = projectInstance.GetProperty(propertyName);

            var value = propertyInstance?.EvaluatedValue;

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

            var matches = actualResult.ProjectStateAfterBuild.GetItemsByItemTypeAndEvaluatedInclude(BuildTaskConstants.SettingItemName, settingName);

            Assert.AreNotEqual(0, matches.Count(), "Expected SonarQubeSetting with include value of '{0}' does not exist", settingName);
            Assert.AreEqual(1, matches.Count(), "Only expecting one SonarQubeSetting with include value of '{0}' to exist", settingName);

            var item = matches.Single();
            var value = item.GetMetadataValue(BuildTaskConstants.SettingValueMetadataName);

            Assert.AreEqual(expectedValue, value, "SonarQubeSetting with include value '{0}' does not have the expected value", settingName);
        }

        /// <summary>
        /// Checks that a SonarQubeSetting does not exist
        /// </summary>
        public static void AssertAnalysisSettingDoesNotExist(BuildResult actualResult, string settingName)
        {
            Assert.IsNotNull(actualResult.ProjectStateAfterBuild, "Test error: ProjectStateAfterBuild should not be null");

            var matches = actualResult.ProjectStateAfterBuild.GetItemsByItemTypeAndEvaluatedInclude(BuildTaskConstants.SettingItemName, settingName);

            Assert.AreEqual(0, matches.Count(), "Not expected SonarQubeSetting with include value of '{0}' to exist. Actual occurrences: {1}", settingName, matches.Count());
        }

        /// <summary>
        /// Checks that a single ItemGroup item with the specified value exists
        /// </summary>
        public static void AssertSingleItemExists(BuildResult actualResult, string itemType, string expectedValue)
        {
            /* The equivalent XML would look like this:
            <ItemGroup>
              <itemType Include="expectedValue">
              </itemType>
            </ItemGroup>
            */

            Assert.IsNotNull(actualResult.ProjectStateAfterBuild, "Test error: ProjectStateAfterBuild should not be null");

            var matches = actualResult.ProjectStateAfterBuild.GetItemsByItemTypeAndEvaluatedInclude(itemType, expectedValue);

            Assert.AreEqual(1, matches.Count(), "Expecting one item of type '{0}' with value '{1}' to exist", itemType, expectedValue);
        }

        /// <summary>
        /// Checks that an ItemGroup item does not exit
        /// </summary>
        public static void AssertItemDoesNotExist(BuildResult actualResult, string itemType, string itemValue)
        {
            /* The equivalent XML would look like this:
            <ItemGroup>
              <itemType Include="expectedValue">
              </itemType>
            </ItemGroup>
            */

            Assert.IsNotNull(actualResult.ProjectStateAfterBuild, "Test error: ProjectStateAfterBuild should not be null");

            var matches = actualResult.ProjectStateAfterBuild.GetItemsByItemTypeAndEvaluatedInclude(itemType, itemValue);

            Assert.AreEqual(0, matches.Where(i => itemValue.Equals(i.EvaluatedInclude)),
                "Not expecting any '{0}' items with value '{1}' to exist", itemType, itemValue);
        }

        /// <summary>
        /// Checks that the expected number of ItemType entries exist
        /// </summary>
        public static void AssertExpectedItemGroupCount(BuildResult actualResult, string itemType, int expectedCount)
        {
            Assert.IsNotNull(actualResult.ProjectStateAfterBuild, "Test error: ProjectStateAfterBuild should not be null");

            IEnumerable<ProjectItemInstance> matches = actualResult.ProjectStateAfterBuild.GetItems(itemType);

            BuildUtilities.LogMessage("<{0}> item values:", itemType);
            foreach(var item in matches)
            {
                BuildUtilities.LogMessage("\t{0}", item.EvaluatedInclude);
            }

            Assert.AreEqual(expectedCount, matches.Count(), "Unexpected number of '{0}' items", itemType);
        }

        /// <summary>
        /// Checks that no analysis warnings will be treated as errors nor will they be ignored
        /// </summary>
        public static void AssertWarningsAreNotTreatedAsErrorsNorIgnored(BuildResult actualResult)
        {
            AssertExpectedPropertyValue(actualResult.ProjectStateAfterBuild, TargetProperties.TreatWarningsAsErrors, "false");
            AssertExpectedPropertyValue(actualResult.ProjectStateAfterBuild, TargetProperties.WarningsAsErrors, "");
            AssertExpectedPropertyValue(actualResult.ProjectStateAfterBuild, TargetProperties.WarningLevel, "4");
        }

        #endregion Assertions

        #region Private methods

        /// <summary>
        /// Writes the build and target output to the output stream
        /// </summary>
        public static void DumpTargetResult(BuildResult result, string target)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            BuildUtilities.LogMessage("Overall build result: {0}", result.OverallResult.ToString());

            if (!result.ResultsByTarget.TryGetValue(target, out TargetResult targetResult))
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

        #endregion Private methods
    }
}
