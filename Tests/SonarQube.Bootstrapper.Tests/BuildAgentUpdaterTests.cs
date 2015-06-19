//-----------------------------------------------------------------------
// <copyright file="BuildAgentUpdaterTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class BuildAgentUpdaterTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void VersionsMatch()
        {
            Assert.IsTrue(BuildAgentUpdater.CheckBootstrapperVersion(CreateVersionFile("1.0"), new Version("1.0")));
            Assert.IsTrue(BuildAgentUpdater.CheckBootstrapperVersion(CreateVersionFile("1.0", "2.0", "3"), new Version("1.0")));
            Assert.IsTrue(BuildAgentUpdater.CheckBootstrapperVersion(CreateVersionFile("2.0", "1.0"), new Version("1.0")));
            Assert.IsTrue(BuildAgentUpdater.CheckBootstrapperVersion(CreateVersionFile("2.0", "001.0"), new Version("1.0")));

            Assert.IsTrue(BuildAgentUpdater.CheckBootstrapperVersion(CreateVersionFile("bogus", "1.0.0.0"), new Version("1.0.0.0")));
            Assert.IsTrue(BuildAgentUpdater.CheckBootstrapperVersion(CreateVersionFile("", "1.0.0.0"), new Version("1.0.0.0")));
            Assert.IsTrue(BuildAgentUpdater.CheckBootstrapperVersion(CreateVersionFile("1.0", "2.0", "1.0"), new Version("1.0")));
        }

        [TestMethod]
        public void VersionsDoNotMatch()
        {
            Assert.IsFalse(BuildAgentUpdater.CheckBootstrapperVersion(CreateVersionFile("1"), new Version("1.0")));
            Assert.IsFalse(BuildAgentUpdater.CheckBootstrapperVersion(CreateVersionFile("1.0.0"), new Version("1.0")));
            Assert.IsFalse(BuildAgentUpdater.CheckBootstrapperVersion(CreateVersionFile("2.0", "3.0", "bogus"), new Version("1.0")));

        }

        [TestMethod]
        public void NoVersionFile()
        {
            string versionFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            if (File.Exists(versionFilePath))
            {
                Assert.Inconclusive("Test setup problem: file should not exist");
            }

            Assert.IsFalse(BuildAgentUpdater.CheckBootstrapperVersion(versionFilePath, new Version("1.0")));

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
