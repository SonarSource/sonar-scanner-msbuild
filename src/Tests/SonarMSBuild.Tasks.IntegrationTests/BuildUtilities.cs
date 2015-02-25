//-----------------------------------------------------------------------
// <copyright file="BuildUtilities.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarMSBuild.Tasks.IntegrationTests
{
    internal static class BuildUtilities
    {
        #region Public methods

        /// <summary>
        /// Creates and returns an empty MSBuild project using the data in the supplied descriptor.
        /// The project will import the analysis targets file and the standard C# targets file.
        /// The project name and GUID will be set if the values are supplied in the descriptor.
        /// </summary>
        public static ProjectRootElement CreateMsBuildProject(TestContext testContext, ProjectDescriptor descriptor)
        {
            ProjectRootElement root = CreateMinimalBuildableProject(testContext.TestDeploymentDir);

            if (!string.IsNullOrEmpty(descriptor.ProjectName))
            {
                root.AddProperty(TargetProperties.ProjectName, descriptor.ProjectName);
            }

            if (descriptor.ProjectGuid != Guid.Empty)
            {
                root.AddProperty(TargetProperties.ProjectGuid, descriptor.ProjectGuid.ToString("D"));
            }

            if (descriptor.ManagedSourceFiles != null)
            {
                foreach (string managedInput in descriptor.ManagedSourceFiles)
                {
                    root.AddItem("Compile", managedInput);
                }
            }

            if (descriptor.IsTestProject)
            {
                //TODO
            }
            else
            {
                //TODO
            }

            return root;
        }

        /// <summary>
        /// Creates and returns a minimal project file that can be built.
        /// The project imports the C# targets and the Sonar analysis targets.
        /// </summary>
        public static ProjectRootElement CreateMinimalBuildableProject(string importedTargetsDir)
        {
            Assert.IsTrue(Directory.Exists(importedTargetsDir), "Test error: the specified directory does not exist. Path: {0}", importedTargetsDir);

            ProjectRootElement root = ProjectRootElement.Create();

            // Import the MsBuild Sonar targets file
            string fullAnalysisTargetPath = Path.Combine(importedTargetsDir, TargetConstants.AnalysisTargetFileName);
            Assert.IsTrue(File.Exists(fullAnalysisTargetPath), "Test error: the analysis target file does not exist. Path: {0}", fullAnalysisTargetPath);
            root.AddImport(fullAnalysisTargetPath);

            // Set the location of the task assembly
            root.AddProperty(TargetProperties.SonarBuildTasksAssemblyFile, typeof(WriteProjectInfoFile).Assembly.Location);

            // Import the standard Microsoft targets
            root.AddImport("$(MSBuildToolsPath)\\Microsoft.CSharp.targets");
            root.AddProperty("OutputType", "library"); // build a library so we don't need a Main method

            return root;
        }

        /// <summary>
        /// Builds the specified target and returns the build result.
        /// </summary>
        public static BuildResult BuildTarget(ProjectInstance project, params string[] targets)
        {
            BuildParameters parameters = new BuildParameters();
            parameters.Loggers = new ILogger[] { new BuildLogger() };

            BuildRequestData requestData = new BuildRequestData(project, targets);

            BuildManager mgr = new BuildManager("testHost");
            BuildResult result = mgr.Build(parameters, requestData);

            return result;
        }

        #endregion

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

        #endregion

        #region Private methods

        /// <summary>
        /// Writes the build and target output to the output stream
        /// </summary>
        public static void DumpTargetResult(BuildResult result, string target)
        {
            Console.WriteLine("Overall build result: {0}", result.OverallResult.ToString());

            TargetResult targetResult;
            if (!result.ResultsByTarget.TryGetValue(target, out targetResult))
            {
                Console.WriteLine(@"Could not find result for target ""{0}""", target);
            }
            else
            {
                Console.WriteLine(@"Results for target ""{0}""", target);
                Console.WriteLine("\tTarget exception: {0}", targetResult.Exception == null ? "{null}" : targetResult.Exception.Message);
                Console.WriteLine("\tTarget result: {0}", targetResult.ResultCode.ToString());
            }
        }

        #endregion

    }
}
