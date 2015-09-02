//-----------------------------------------------------------------------
// <copyright file="BuildAssertions.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

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
