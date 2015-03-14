//-----------------------------------------------------------------------
// <copyright file="BuildAssertions.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace SonarMSBuild.Tasks.IntegrationTests
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
            BuildUtilities.DumpTargetResult(result, target);

            TargetResult targetResult;
            if (!result.ResultsByTarget.TryGetValue(target, out targetResult))
            {
                Assert.Inconclusive(@"Could not find result for target ""{0}""", target);
            }
            Assert.AreEqual<BuildResultCode>(resultCode, result.OverallResult, "Unexpected build result");
        }


        /// <summary>
        /// Checks that the specified item group is empty
        /// </summary>
        public static void AssertItemGroupIsEmpty(ProjectInstance project, string itemType)
        {
            Assert.IsTrue(project.GetItems(itemType).Count() == 0, "Item group '{0}' should be empty", itemType);

        }

        /// <summary>
        /// Checks that the specified item group is not empty
        /// </summary>
        public static void AssertItemGroupIsNotEmpty(ProjectInstance project, string itemType)
        {
            Assert.IsTrue(project.GetItems(itemType).Count() > 0, "Item group '{0}' should be not empty", itemType);
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

        public static bool AssertBooleanPropertyExists(ProjectInstance projectInstance, string propertyName)
        {
            ProjectPropertyInstance propertyInstance = BuildAssertions.AssertPropertyExists(projectInstance, TargetProperties.SonarTestProject);

            bool result;
            bool parsedOk = bool.TryParse(propertyInstance.EvaluatedValue, out result);
            Assert.IsTrue(parsedOk, "Failed to convert the property value to a boolean. Property: {0}, Evaluated value: {1}", propertyInstance.Name, propertyInstance.EvaluatedValue);

            return result;
        }

        #endregion
    }
}
