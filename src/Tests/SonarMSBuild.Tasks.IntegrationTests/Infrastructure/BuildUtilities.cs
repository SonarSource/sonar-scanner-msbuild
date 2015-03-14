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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarMSBuild.Tasks.IntegrationTests
{
    internal static class BuildUtilities
    {
        // TODO: work out some way to automatically set the tools version depending on the version of VS being used
        public const string MSBuildToolsVersionForTestProjects = "12.0"; // use this line for VS2013
        //public const string MSBuildToolsVersionForTestProjects = "14.0"; // use this line for VS2013

        #region Public methods

        /// <summary>
        /// Creates and returns a valid, initialized MSBuild ProjectRootElement for a new project in the
        /// specified parent folder
        /// </summary>
        /// <param name="projectDirectory">The folder in which the project should be created</param>
        /// <param name="preImportProperties">Any MSBuild properties that should be set before any targets are imported</param>
        /// <returns></returns>
        public static ProjectRootElement CreateValidProjectRoot(TestContext testContext, string projectDirectory, IDictionary<string, string> preImportProperties)
        {
            ProjectDescriptor descriptor = CreateValidProjectDescriptor(projectDirectory);
            ProjectRootElement projectRoot = CreateInitializedProjectRoot(testContext, descriptor, preImportProperties);
            return projectRoot;
        }

        /// <summary>
        /// Creates and returns a valid, initialized MSBuild ProjectRootElement for a new project in the
        /// specified parent folder with the specified project file name
        /// </summary>
        /// <param name="projectDirectory">The folder in which the project should be created</param>
        /// <param name="projectFileName">The name of the project file</param>
        /// <param name="preImportProperties">Any MSBuild properties that should be set before any targets are imported</param>
        /// <returns></returns>
        public static ProjectRootElement CreateValidNamedProjectRoot(TestContext testContext, string projectFileName, string projectDirectory, IDictionary<string, string> preImportProperties)
        {
            ProjectDescriptor descriptor = CreateValidNamedProjectDescriptor(projectDirectory, projectFileName);
            ProjectRootElement projectRoot = CreateInitializedProjectRoot(testContext, descriptor, preImportProperties);
            return projectRoot;
        }

        /// <summary>
        /// Creates and returns a valid project descriptor for a project in the supplied folders
        /// </summary>
        public static ProjectDescriptor CreateValidProjectDescriptor(string parentDirectory)
        {
            return CreateValidNamedProjectDescriptor(parentDirectory, "MyProject.xproj");
        }

        /// <summary>
        /// Creates and returns a valid project descriptor for a project in the supplied folders
        /// </summary>
        public static ProjectDescriptor CreateValidNamedProjectDescriptor(string parentDirectory, string projectFileName)
        {
            ProjectDescriptor descriptor = new ProjectDescriptor()
            {
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = false,
                ParentDirectoryPath = parentDirectory,
                ProjectFolderName = "MyProjectDir",
                ProjectFileName = projectFileName
            };
            return descriptor;
        }


        /// <summary>
        /// Creates a project file on disk from the specified descriptor.
        /// Sets the Sonar output folder property, if specified.
        /// </summary>
        public static ProjectRootElement CreateInitializedProjectRoot(TestContext testContext, ProjectDescriptor descriptor, IDictionary<string, string> preImportProperties)
        {
            ProjectRootElement projectRoot = BuildUtilities.CreateSonarAnalysisProject(testContext, descriptor, preImportProperties);

            projectRoot.ToolsVersion = MSBuildToolsVersionForTestProjects;

            projectRoot.Save(descriptor.FullFilePath);

            testContext.AddResultFile(descriptor.FullFilePath);
            return projectRoot;
        }

        /// <summary>
        /// Creates and returns an empty MSBuild project using the data in the supplied descriptor.
        /// The project will import the Sonar analysis targets file and the standard C# targets file.
        /// The project name and GUID will be set if the values are supplied in the descriptor.
        /// </summary>
        public static ProjectRootElement CreateSonarAnalysisProject(TestContext testContext, ProjectDescriptor descriptor, IDictionary<string, string> preImportProperties)
        {
            string sonarTargetFile = Path.Combine(testContext.DeploymentDirectory, TargetConstants.AnalysisTargetFile);
            Assert.IsTrue(File.Exists(sonarTargetFile), "Test error: the Sonar analysis targets file could not be found. Full path: {0}", sonarTargetFile);

            ProjectRootElement root = CreateMinimalBuildableProject(preImportProperties, sonarTargetFile);

            // Set the location of the task assembly
            root.AddProperty(TargetProperties.SonarBuildTasksAssemblyFile, typeof(WriteProjectInfoFile).Assembly.Location);

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

            if (descriptor.ContentFiles != null)
            {
                foreach(string contentFile in descriptor.ContentFiles)
                {
                    root.AddItem("Content", contentFile);
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
        /// Creates and returns a minimal C# project file that can be built.
        /// The project imports the C# targets and any other optional targets that are specified.
        /// The project is NOT saved.
        /// </summary>
        /// <param name="preImportProperties">Any properties that need to be set before the C# targets are imported. Can be null.</param>
        /// <param name="importsBeforeTargets">Any targets that should be imported before the C# targets are imported. Optional.</param>
        public static ProjectRootElement CreateMinimalBuildableProject(IDictionary<string, string> preImportProperties, params string[] importsBeforeTargets)
        {
            ProjectRootElement root = ProjectRootElement.Create();
            
            //root.AddImport(@"$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props");

            foreach(string importTarget in importsBeforeTargets)
            {
                Assert.IsTrue(File.Exists(importTarget), "Test error: the specified target file does not exist. Path: {0}", importTarget);
                root.AddImport(importTarget);
            }

            if (preImportProperties != null)
            {
                foreach(KeyValuePair<string, string> kvp in preImportProperties)
                {
                    root.AddProperty(kvp.Key, kvp.Value);
                }
            }

            // Import the standard Microsoft targets
            root.AddImport("$(MSBuildToolsPath)\\Microsoft.CSharp.targets");
            root.AddProperty("OutputType", "library"); // build a library so we don't need a Main method

            return root;
        }

        /// <summary>
        /// Builds the specified target and returns the build result.
        /// </summary>
        /// <param name="project">The project to build</param>
        /// <param name="logger">The build logger to use. If null then a default logger will be used that dumps the build output to the console.</param>
        /// <param name="targets">Optional list of targets to execute</param>
        /// <returns></returns>
        public static BuildResult BuildTargets(ProjectRootElement projectRoot, ILogger logger, params string[] targets)
        {
            ProjectInstance projectInstance = new ProjectInstance(projectRoot);

            BuildParameters parameters = new BuildParameters();
            parameters.Loggers = new ILogger[] { logger ?? new BuildLogger() };
            parameters.UseSynchronousLogging = true; 
            parameters.ShutdownInProcNodeOnBuildFinish = true; // required, other we can get an "Attempted to access an unloaded AppDomain" exception when the test finishes.
            
            BuildRequestData requestData = new BuildRequestData(projectInstance, targets);

            BuildResult result = null;
            BuildManager mgr = new BuildManager();
            try
            {
                result = mgr.Build(parameters, requestData);

                BuildUtilities.DumpProjectProperties(projectInstance, "Project properties post-build");
            }
            finally
            {
                mgr.ShutdownAllNodes();
                mgr.ResetCaches();
                mgr.Dispose();
            }

            return result;
        }

        /// <summary>
        /// Dumps the project properties to the console
        /// </summary>
        /// <param name="projectInstance">The owning project</param>
        /// <param name="title">Optional title to be written to the console</param>
        public static void DumpProjectProperties(ProjectInstance projectInstance, string title)
        {
            if (projectInstance == null)
            {
                throw new ArgumentNullException("projectInstance");
            }
            
            Console.WriteLine();
            Console.WriteLine("******************************************************");
            Console.WriteLine(title ?? "Project properties");
            foreach (ProjectPropertyInstance property in projectInstance.Properties ?? Enumerable.Empty<ProjectPropertyInstance>())
            {
                Console.WriteLine("{0} = {1}{2}",
                    property.Name,
                    property.EvaluatedValue,
                    property.IsImmutable ? ", IMMUTABLE" : null);
            }
            Console.WriteLine("******************************************************");
            Console.WriteLine();
        }

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
