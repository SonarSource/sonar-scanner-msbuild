//-----------------------------------------------------------------------
// <copyright file="BuildAgentUpdaterTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System;
using TestUtilities;
using System.Reflection;
using System.Linq;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class BuildAgentUpdaterTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void VersionsMatch()
        {
            BuildAgentUpdater updater = new BuildAgentUpdater();
            Assert.IsTrue(updater.CheckBootstrapperApiVersion(CreateVersionFile("1.0"), new Version("1.0")));
            Assert.IsTrue(updater.CheckBootstrapperApiVersion(CreateVersionFile("1.0", "2.0", "3"), new Version("1.0")));
            Assert.IsTrue(updater.CheckBootstrapperApiVersion(CreateVersionFile("2.0", "1.0"), new Version("1.0")));
            Assert.IsTrue(updater.CheckBootstrapperApiVersion(CreateVersionFile("2.0", "001.0"), new Version("1.0")));

            Assert.IsTrue(updater.CheckBootstrapperApiVersion(CreateVersionFile("bogus", "1.0.0.0"), new Version("1.0.0.0")));
            Assert.IsTrue(updater.CheckBootstrapperApiVersion(CreateVersionFile("", "1.0.0.0"), new Version("1.0.0.0")));
            Assert.IsTrue(updater.CheckBootstrapperApiVersion(CreateVersionFile("1.0", "2.0", "1.0"), new Version("1.0")));
        }

        [TestMethod]
        public void VersionsDoNotMatch()
        {
            BuildAgentUpdater updater = new BuildAgentUpdater();
            Assert.IsFalse(updater.CheckBootstrapperApiVersion(CreateVersionFile("1"), new Version("1.0")));
            Assert.IsFalse(updater.CheckBootstrapperApiVersion(CreateVersionFile("1.0.0"), new Version("1.0")));
            Assert.IsFalse(updater.CheckBootstrapperApiVersion(CreateVersionFile("2.0", "3.0", "bogus"), new Version("1.0")));

        }

        [TestMethod]
        public void NoVersionFile()
        {
            BuildAgentUpdater updater = new BuildAgentUpdater();
            string versionFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            if (File.Exists(versionFilePath))
            {
                Assert.Inconclusive("Test setup problem: file should not exist");
            }

            Assert.IsFalse(updater.CheckBootstrapperApiVersion(versionFilePath, new Version("1.0")));

        }

        [TestMethod]
        public void Updater_CheckDownloadUrl_ResoultionFailure()
        {
            // this URL produces a resolution failure
            CheckInvalidUrlFails("http://updater.checkdownload.url.dummy.url:9000");
        }

        [TestMethod]
        public void Updater_CheckDownloadUrl_ConnectionFailure()
        {
            // this URL produces a connection failure
            CheckInvalidUrlFails("http://localhost:9000");
        }

        [TestMethod]
        [Description("The targets file should be copied if none are present. The files should not be copied if they already exist and have not been changed.")]
        public void InjectTargetsFile_Copy()
        {
            // In case the dummy targets file somehow does not get deleted (e.g. when debugging) , make sure its content is valid XML 
            string sourceTargetsContent = @"<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />";
            CreateDummySourceTargetsFile(sourceTargetsContent);

            try
            {
                InjectTargetsFileAndAssert(sourceTargetsContent, expectCopy: true);

                // if we try to inject again, the targets should not be copied because they have the same content
                InjectTargetsFileAndAssert(sourceTargetsContent, expectCopy: false);
            }
            finally
            {
                foreach (string destinationDir in BuildAgentUpdater.DestinationDirs)
                {
                    string path = Path.Combine(destinationDir, BuildAgentUpdater.LoaderTargetsName);
                    File.Delete(path);
                }
            }
        }

        [TestMethod]
        [Description("The targets should be copied if they don't exist. If they have been changed, the updater should overwrite them")]
        public void InjectTargetsFile_Overwrite()
        {
            // In case the dummy targets file somehow does not get deleted (e.g. when debugging), make sure its content valid XML
            string sourceTargetsContent1 = @"<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />";
            string sourceTargetsContent2 = @"<Project ToolsVersion=""12.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />";

            CreateDummySourceTargetsFile(sourceTargetsContent1);

            try
            {
                InjectTargetsFileAndAssert(sourceTargetsContent1, expectCopy: true);
                Assert.IsTrue(BuildAgentUpdater.DestinationDirs.Count >= 2, "Expecting at least 2 destination directories");

                string path = Path.Combine(BuildAgentUpdater.DestinationDirs[0], BuildAgentUpdater.LoaderTargetsName);
                File.Delete(path);

                CreateDummySourceTargetsFile(sourceTargetsContent2);
                InjectTargetsFileAndAssert(sourceTargetsContent2, expectCopy: true);

            }
            finally
            {
                foreach (string destinationDir in BuildAgentUpdater.DestinationDirs)
                {
                    string path = Path.Combine(destinationDir, BuildAgentUpdater.LoaderTargetsName);
                    File.Delete(path);
                }
            }
        }

        private static void CreateDummySourceTargetsFile(string sourceTargetsContent1)
        {
            string bootstrapperLocation = Path.GetDirectoryName(typeof(BuildAgentUpdater).Assembly.Location);
            string dummyLoaderTargets = Path.Combine(bootstrapperLocation, BuildAgentUpdater.LoaderTargetsName);

            if (File.Exists(dummyLoaderTargets))
            {
                File.Delete(dummyLoaderTargets);
            }

            File.AppendAllText(dummyLoaderTargets, sourceTargetsContent1);
        }

        private static void InjectTargetsFileAndAssert(string sourceTargetsContent, bool expectCopy)
        {
            BuildAgentUpdater updater = new BuildAgentUpdater();
            TestLogger logger = new TestLogger();
            updater.InjectLoaderTargets(logger);

            foreach (string destinationDir in BuildAgentUpdater.DestinationDirs)
            {
                string path = Path.Combine(destinationDir, BuildAgentUpdater.LoaderTargetsName);
                Assert.IsTrue(File.Exists(path), "Targets file not found at: " + path);
                Assert.AreEqual(sourceTargetsContent, File.ReadAllText(path));

                if (expectCopy)
                {
                    Assert.AreEqual(BuildAgentUpdater.DestinationDirs.Count, logger.Messages.Count, "All destinations should have been covered");
                    Assert.IsTrue(logger.Messages.Any(m => m.Contains(destinationDir)));
                }
            }
        }

        private void CheckInvalidUrlFails(string url)
        {
            // Arrange
            TestLogger logger = new TestLogger();
            BuildAgentUpdater updater = new BuildAgentUpdater();

            string downloadDir = this.TestContext.DeploymentDirectory;
            string nonExistentUrl = url;

            string expectedUrl = nonExistentUrl + BootstrapperSettings.IntegrationUrlSuffix;
            string expectedDownloadPath = Path.Combine(downloadDir, BootstrapperSettings.SonarQubeIntegrationFilename);

            // Act
            bool success = updater.TryUpdate(nonExistentUrl, downloadDir, logger);

            // Assert
            Assert.IsFalse(success, "Not expecting the update to succeed");
            logger.AssertSingleMessageExists(expectedUrl, expectedDownloadPath);
            logger.AssertSingleErrorExists(nonExistentUrl);
            logger.AssertErrorsLogged(1);
        }

        private string CreateVersionFile(params string[] versionStrings)
        {
            BootstrapperSupportedVersions versions = new BootstrapperSupportedVersions();
            versions.Versions.AddRange(versionStrings);
            string path = Path.Combine(TestContext.TestRunDirectory, "versions.xml");
            versions.Save(path);

            return path;
        }

    }

}
