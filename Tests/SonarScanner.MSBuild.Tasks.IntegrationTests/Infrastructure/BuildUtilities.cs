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
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests
{
    internal static class BuildUtilities
    {
        // TODO: work out some way to automatically set the tools version depending on the version of VS being used
        //public const string MSBuildToolsVersionForTestProjects = "14.0"; // use this line for VS2015
        public const string MSBuildToolsVersionForTestProjects = "15.0"; // use this line for VS2017

        private const string StandardImportBeforePropertyName = "ImportByWildcardBeforeMicrosoftCommonTargets";
        private const string StandardImportAfterPropertyName = "ImportByWildcardAfterMicrosoftCommonTargets";
        private const string UserImportBeforePropertyName = "ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets";
        private const string UserImportAfterPropertyName = "ImportUserLocationsByWildcardAfterMicrosoftCommonTargets";

        #region Project creation helpers

        /// <summary>
        /// Creates and returns a valid project descriptor for a project in the supplied folders
        /// </summary>
        public static ProjectDescriptor CreateValidProjectDescriptor(string parentDirectory, string projectFileName = "MyProject.xproj.txt", bool isVBProject = false)
        {
            var descriptor = new ProjectDescriptor()
            {
                ProjectLanguage = isVBProject ? SonarScanner.MSBuild.Common.ProjectLanguages.VisualBasic : SonarScanner.MSBuild.Common.ProjectLanguages.CSharp,
                ProjectGuid = Guid.NewGuid(),
                IsTestProject = false,
                ParentDirectoryPath = parentDirectory,
                ProjectFolderName = "MyProjectDir",
                ProjectFileName = projectFileName
            };
            return descriptor;
        }

        /// <summary>
        /// Creates and returns a valid, initialized MSBuild ProjectRootElement for a new project in the
        /// specified parent folder
        /// </summary>
        /// <param name="projectDirectory">The folder in which the project should be created</param>
        /// <param name="preImportProperties">Any MSBuild properties that should be set before any targets are imported</param>
        public static ProjectRootElement CreateValidProjectRoot(TestContext testContext, string projectDirectory, IDictionary<string, string> preImportProperties, bool isVBProject = false)
        {
            var descriptor = CreateValidProjectDescriptor(projectDirectory, isVBProject: isVBProject);
            var projectRoot = CreateInitializedProjectRoot(testContext, descriptor, preImportProperties);
            return projectRoot;
        }

        /// <summary>
        /// Creates a project file on disk from the specified descriptor.
        /// Sets the SonarQube output folder property, if specified.
        /// </summary>
        public static ProjectRootElement CreateInitializedProjectRoot(TestContext testContext, ProjectDescriptor descriptor, IDictionary<string, string> preImportProperties)
        {
            if (testContext == null)
            {
                throw new ArgumentNullException(nameof(testContext));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var projectRoot = BuildUtilities.CreateAnalysisProject(testContext, descriptor, preImportProperties);

            projectRoot.ToolsVersion = MSBuildToolsVersionForTestProjects;

            projectRoot.Save(descriptor.FullFilePath);

            testContext.AddResultFile(descriptor.FullFilePath);
            return projectRoot;
        }

        /// <summary>
        /// Creates and returns a minimal C# or VB project file that can be built.
        /// The project imports the C#/VB targets and any other optional targets that are specified.
        /// The project is NOT saved.
        /// </summary>
        /// <param name="preImportProperties">Any properties that need to be set before the C# targets are imported. Can be null.</param>
        /// <param name="importsBeforeTargets">Any targets that should be imported before the C# targets are imported. Optional.</param>
        public static ProjectRootElement CreateMinimalBuildableProject(IDictionary<string, string> preImportProperties, bool isVBProject, params string[] importsBeforeTargets)
        {
            var root = ProjectRootElement.Create();

            foreach(var importTarget in importsBeforeTargets)
            {
                Assert.IsTrue(File.Exists(importTarget), "Test error: the specified target file does not exist. Path: {0}", importTarget);
                root.AddImport(importTarget);
            }

            if (preImportProperties != null)
            {
                foreach(var kvp in preImportProperties)
                {
                    root.AddProperty(kvp.Key, kvp.Value);
                }
            }

            // Ensure the output path is set
            if (preImportProperties == null || !preImportProperties.ContainsKey("OutputPath"))
            {
                root.AddProperty("OutputPath", @"bin\");
            }

            // Ensure the language is set
            if (preImportProperties == null || !preImportProperties.ContainsKey("Language"))
            {
                root.AddProperty("Language", isVBProject ? "VB" : "C#");
            }

            // Import the standard Microsoft targets
            if (isVBProject)
            {
                root.AddImport("$(MSBuildToolsPath)\\Microsoft.VisualBasic.targets");
            }
            else
            {
                root.AddImport("$(MSBuildToolsPath)\\Microsoft.CSharp.targets");
            }
            root.AddProperty("OutputType", "library"); // build a library so we don't need a Main method

            return root;
        }

        /// <summary>
        /// Creates and returns a new MSBuild project using the supplied template
        /// </summary>
        public static ProjectRootElement CreateProjectFromTemplate(string projectFilePath, TestContext testContext, string templateXml, params object[] args)
        {
            var projectXml = templateXml;
            if (args != null && args.Any())
            {
                projectXml = string.Format(System.Globalization.CultureInfo.CurrentCulture, templateXml, args);
            }

            File.WriteAllText(projectFilePath, projectXml);
            testContext.AddResultFile(projectFilePath);

            var projectRoot = ProjectRootElement.Open(projectFilePath);
            return projectRoot;
        }

        #endregion Project creation helpers

        #region Build helper methods

        /// <summary>
        /// Builds the specified target and returns the build result.
        /// </summary>
        /// <param name="project">The project to build</param>
        /// <param name="logger">The build logger to use. If null then a default logger will be used that dumps the build output to the console.</param>
        /// <param name="targets">Optional list of targets to execute</param>
        public static BuildResult BuildTargets(ProjectRootElement projectRoot, ILogger logger, params string[] targets)
        {
            if (projectRoot == null)
            {
                throw new ArgumentNullException(nameof(projectRoot));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            var projectInstance = new ProjectInstance(projectRoot);
            return BuildTargets(projectInstance, logger, targets);
        }

        /// <summary>
        /// Builds the specified target and returns the build result.
        /// </summary>
        /// <param name="project">The project to build</param>
        /// <param name="logger">The build logger to use. If null then a default logger will be used that dumps the build output to the console.</param>
        /// <param name="targets">Optional list of targets to execute</param>
        public static BuildResult BuildTargets(ProjectInstance projectInstance, ILogger logger, params string[] targets)
        {
            if (projectInstance == null)
            {
                throw new ArgumentNullException(nameof(projectInstance));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            var parameters = new BuildParameters
            {
                Loggers = new ILogger[] { logger ?? new BuildLogger() },
                UseSynchronousLogging = true,
                ShutdownInProcNodeOnBuildFinish = true // required, other we can get an "Attempted to access an unloaded AppDomain" exception when the test finishes.
            };

            var requestData = new BuildRequestData(projectInstance, targets);

            BuildResult result = null;
            var mgr = new BuildManager();
            try
            {
                result = mgr.Build(parameters, requestData);

                result.ProjectStateAfterBuild = projectInstance;
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

        #endregion Build helper methods

        #region Miscellaneous public methods

        /// <summary>
        /// Sets properties to disable the normal ImportAfter/ImportBefore behavior to
        /// prevent any additional targets from being picked up.
        /// This is necessary so the tests run correctly on machines that have
        /// the installation targets installed.
        /// See the Microsoft Common targets for more info e.g. C:\Program Files (x86)\MSBuild\12.0\Bin\Microsoft.Common.CurrentVersion.targets
        /// Any existing settings for those properties will be over-ridden.
        /// </summary>
        public static void DisableStandardTargetsWildcardImporting(IDictionary<string, string> properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            properties[StandardImportBeforePropertyName] = "false";
            properties[StandardImportAfterPropertyName] = "false";
            properties[UserImportBeforePropertyName] = "false";
            properties[UserImportAfterPropertyName] = "false";
        }

        public static void LogMessage(string message, params string[] args)
        {
            Console.WriteLine(message, args);
        }

        public static void LogMessage()
        {
            LogMessage(string.Empty);
        }

        #endregion Miscellaneous public methods

        #region Private methods

        /// <summary>
        /// Creates and returns an empty MSBuild project using the data in the supplied descriptor.
        /// The project will import the SonarQube analysis targets file and the standard C# targets file.
        /// The project name and GUID will be set if the values are supplied in the descriptor.
        /// </summary>
        private static ProjectRootElement CreateAnalysisProject(TestContext testContext, ProjectDescriptor descriptor,
            IDictionary<string, string> preImportProperties)
        {
            if (testContext == null)
            {
                throw new ArgumentNullException(nameof(testContext));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var sqTargetFile = TestUtils.EnsureAnalysisTargetsExists(testContext);
            Assert.IsTrue(File.Exists(sqTargetFile), "Test error: the SonarQube analysis targets file could not be found. Full path: {0}", sqTargetFile);
            testContext.AddResultFile(sqTargetFile);

            var properties = preImportProperties ?? new Dictionary<string, string>();

            // Disable the standard "ImportBefore/ImportAfter" behavior if the caller
            // hasn't defined what they want to happen explicitly
            if (!properties.ContainsKey(StandardImportBeforePropertyName))
            {
                DisableStandardTargetsWildcardImporting(properties);
            }

            var root = CreateMinimalBuildableProject(properties, descriptor.IsVbProject, sqTargetFile);

            // Set the location of the task assembly
            if (!properties.ContainsKey(TargetProperties.SonarBuildTasksAssemblyFile))
            {
                root.AddProperty(TargetProperties.SonarBuildTasksAssemblyFile, typeof(WriteProjectInfoFile).Assembly.Location);
            }

            if (descriptor.ProjectGuid != Guid.Empty)
            {
                root.AddProperty(TargetProperties.ProjectGuid, descriptor.ProjectGuid.ToString("D"));
            }

            foreach (var file in descriptor.Files)
            {
                root.AddItem(file.ItemGroup, file.FilePath);
            }

            if (descriptor.IsTestProject && !root.Properties.Any(p => string.Equals(p.Name, TargetProperties.SonarQubeTestProject)))
            {
                root.AddProperty(TargetProperties.SonarQubeTestProject, "true");
            }

            if (descriptor.Encoding != null)
            {
                root.AddProperty(TargetProperties.CodePage, descriptor.Encoding.CodePage.ToString());
            }

            return root;
        }

        /// <summary>
        /// Dumps the project properties to the console
        /// </summary>
        /// <param name="projectInstance">The owning project</param>
        /// <param name="title">Optional title to be written to the console</param>
        private static void DumpProjectProperties(ProjectInstance projectInstance, string title)
        {
            if (projectInstance == null)
            {
                throw new ArgumentNullException(nameof(projectInstance));
            }

            LogMessage();
            LogMessage("******************************************************");
            LogMessage(title ?? "Project properties");
            foreach (var property in projectInstance.Properties ?? Enumerable.Empty<ProjectPropertyInstance>())
            {
                LogMessage("{0} = {1}{2}",
                    property.Name,
                    property.EvaluatedValue,
                    property.IsImmutable ? ", IMMUTABLE" : null);
            }
            LogMessage("******************************************************");
            LogMessage();
        }

        #endregion Private methods
    }
}
