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
        public void Updater_CheckDownloadUrl()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            BuildAgentUpdater updater = new BuildAgentUpdater();

            string downloadDir = this.TestContext.DeploymentDirectory;
            string nonExistentUrl = "http://updater.checkdownload.url.dummy.url:9000";

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
