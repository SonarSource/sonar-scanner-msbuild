//-----------------------------------------------------------------------
// <copyright file="TargetsInstallerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using TestUtilities;
using System.IO;
using System.Linq;
using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor.UnitTests
{
    [TestClass]
    public class TargetsInstallerTests
    {
        string WorkingDirectory;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Init()
        {
            CleanupMsbuildDirectories();
            WorkingDirectory = TestUtils.CreateTestSpecificFolder(this.TestContext, "sonarqube");
        }

        [TestCleanup]
        public void TearDown()
        {
            CleanupMsbuildDirectories();
        }

        [TestMethod]
        [Description("The targets file should be copied if none are present. The files should not be copied if they already exist and have not been changed.")]
        public void InstallTargetsFile_Copy()
        {
            // In case the dummy targets file somehow does not get deleted (e.g. when debugging) , make sure its content is valid XML
            string sourceTargetsContent = @"<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />";
            CreateDummySourceTargetsFile(sourceTargetsContent);

            InstallTargetsFileAndAssert(sourceTargetsContent, expectCopy: true);

            // if we try to inject again, the targets should not be copied because they have the same content
            InstallTargetsFileAndAssert(sourceTargetsContent, expectCopy: false);
        }

        [TestMethod]
        [Description("The targets should be copied if they don't exist. If they have been changed, the updater should overwrite them")]
        public void InstallTargetsFile_Overwrite()
        {
            // In case the dummy targets file somehow does not get deleted (e.g. when debugging), make sure its content valid XML
            string sourceTargetsContent1 = @"<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />";
            string sourceTargetsContent2 = @"<Project ToolsVersion=""12.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />";

            CreateDummySourceTargetsFile(sourceTargetsContent1);

            InstallTargetsFileAndAssert(sourceTargetsContent1, expectCopy: true);
            Assert.IsTrue(FileConstants.ImportBeforeDestinationDirectoryPaths.Count == 2, "Expecting two destination directories");

            string path = Path.Combine(FileConstants.ImportBeforeDestinationDirectoryPaths[0], FileConstants.ImportBeforeTargetsName);
            File.Delete(path);

            CreateDummySourceTargetsFile(sourceTargetsContent2);
            InstallTargetsFileAndAssert(sourceTargetsContent2, expectCopy: true);
            InstallTargetsFileAndAssert(sourceTargetsContent2, expectCopy: true);
        }

        private static void CleanupMsbuildDirectories()
        {
            // SONARMSBRU-149: we used to deploy the targets file to the 4.0 directory but this
            // is no longer supported. To be on the safe side we'll clean up the old location too.
            IList<string> cleanUpDirs = new List<string>(FileConstants.ImportBeforeDestinationDirectoryPaths);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            cleanUpDirs.Add(Path.Combine(appData, "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"));

            foreach (string destinationDir in cleanUpDirs)
            {
                string path = Path.Combine(destinationDir, FileConstants.ImportBeforeTargetsName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static void CreateDummySourceTargetsFile(string sourceTargetsContent1)
        {
            string exeLocation = Path.GetDirectoryName(typeof(TargetsInstaller).Assembly.Location);

            string dummyLoaderBeforeTargets = Path.Combine(exeLocation, "Targets", FileConstants.ImportBeforeTargetsName);
            string dummyLoaderTargets = Path.Combine(exeLocation, "Targets", FileConstants.IntegrationTargetsName);


            if (File.Exists(dummyLoaderBeforeTargets))
            {
                File.Delete(dummyLoaderBeforeTargets);
            }
            if (File.Exists(dummyLoaderTargets))
            {
                File.Delete(dummyLoaderTargets);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dummyLoaderBeforeTargets));

            File.AppendAllText(dummyLoaderBeforeTargets, sourceTargetsContent1);
            File.AppendAllText(dummyLoaderTargets, sourceTargetsContent1);
        }

        private void InstallTargetsFileAndAssert(string expectedContent, bool expectCopy)
        {
            TargetsInstaller installer = new TargetsInstaller();
            TestLogger logger = new TestLogger();
            installer.InstallLoaderTargets(logger, WorkingDirectory);

            foreach (string destinationDir in FileConstants.ImportBeforeDestinationDirectoryPaths)
            {
                string path = Path.Combine(destinationDir, FileConstants.ImportBeforeTargetsName);
                Assert.IsTrue(File.Exists(path), ".targets file not found at: " + path);
                Assert.AreEqual(
                    expectedContent,
                    File.ReadAllText(path),
                    ".targets does not have expected content at " + path);

                Assert.IsTrue(logger.DebugMessages.Any(m => m.Contains(destinationDir)));
            }

            string targetsPath = Path.Combine(WorkingDirectory, "bin", "targets", FileConstants.IntegrationTargetsName);
            Assert.IsTrue(File.Exists(targetsPath), ".targets file not found at: " + targetsPath);

            if (expectCopy)
            {
                Assert.AreEqual(
                    FileConstants.ImportBeforeDestinationDirectoryPaths.Count + 1,
                    logger.DebugMessages.Count,
                    "All destinations should have been covered");
            }

        }
    }
}
