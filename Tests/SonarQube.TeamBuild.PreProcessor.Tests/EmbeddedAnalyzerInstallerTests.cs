//-----------------------------------------------------------------------
// <copyright file="EmbeddedAnalyzerInstallerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.PreProcessor.Roslyn;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class EmbeddedAnalyzerInstallerTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void EmbeddedInstall_SinglePlugin_SingleResources_Succeeds()
        {
            // Arrange
            string localCacheDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            TestLogger logger = new TestLogger();
            MockSonarQubeServer mockServer = new MockSonarQubeServer();
            mockServer.Data.InstalledPlugins.Add("plugin1");

            mockServer.Data.AddEmbeddedFile("plugin1", "embeddedFile1.zip", CreateDummyZipFile("file1.dll", "file2.txt"));

            Plugin requestedPlugin = new Plugin()
            {
                Key = "plugin1",
                Version = "1.0",
                StaticResourceName = "embeddedFile1.zip"
            };

            string[] expectedFilePaths = CalculateExpectedCachedFilePaths(localCacheDir, requestedPlugin, "embeddedFile1.zip", "file1.dll", "file2.txt");

            EmbeddedAnalyzerInstaller testSubject = new EmbeddedAnalyzerInstaller(mockServer, localCacheDir, logger);

            // Act
            IEnumerable<string> actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestedPlugin });

            // Assert
            Assert.IsNotNull(actualFiles, "Returned list should not be null");
            AssertExpectedFilesReturned(actualFiles, expectedFilePaths);
            AssertExpectedFilesExist(expectedFilePaths);
            AssertExpectedFilesInCache(3, localCacheDir); // one zip containing two files
        }

        [TestMethod]
        public void EmbeddedInstall_SinglePluginMultipleResources_Succeeds()
        {
            //TODO
        }

        [TestMethod]
        public void EmbeddedInstall_MultiplePlugins_Succeeds()
        {
            //TODO
        }

        [TestMethod]
        public void EmbeddedInstall_MissingPlugin_SucceedsButNoFiles()
        {
            // Arrange
            string localCacheDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            TestLogger logger = new TestLogger();
            MockSonarQubeServer mockServer = CreateServerWithDummyPlugin("plugin1");

            Plugin requestedPlugin = new Plugin() { Key = "missingPlugin", Version = "1.0", StaticResourceName = "could.be.anything" };

            EmbeddedAnalyzerInstaller testSubject = new EmbeddedAnalyzerInstaller(mockServer, localCacheDir, logger);

            // Act
            IEnumerable<string> actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestedPlugin });

            // Assert
            Assert.IsNotNull(actualFiles, "Returned list should not be null");
            AssertExpectedFilesReturned(actualFiles /* no files expected */);

        }

        [TestMethod]
        public void EmbeddedInstall_PluginWithNoResources_SucceedsButNoFiles()
        {
            //TODO
        }

        [TestMethod]
        public void EmbeddedInstall_NoPluginsSpecified_SucceedsButNoFiles()
        {
            // Arrange
            string localCacheDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            TestLogger logger = new TestLogger();
            MockSonarQubeServer mockServer = CreateServerWithDummyPlugin("plugin1");

            EmbeddedAnalyzerInstaller testSubject = new EmbeddedAnalyzerInstaller(mockServer, localCacheDir, logger);

            // Act
            IEnumerable<string> actualFiles = testSubject.InstallAssemblies(new Plugin[] { });

            // Assert
            Assert.IsNotNull(actualFiles, "Returned list should not be null");
            AssertExpectedFilesReturned(actualFiles /* no files expected */);
        }

        #endregion

        #region Private methods

        private MockSonarQubeServer CreateServerWithDummyPlugin(string pluginId)
        {
            MockSonarQubeServer mockServer = new MockSonarQubeServer();
            mockServer.Data.InstalledPlugins.Add(pluginId);
            mockServer.Data.AddEmbeddedFile(pluginId, "embeddedFile1.zip", CreateDummyZipFile("file1.dll", "file2.txt"));
            return mockServer;
        }

        private byte[] CreateDummyZipFile(params string[] contentFileNames)
        {
            string fileName = "dummy.zip";

            // Create a temporary directory structure
            string tempDir = TestUtils.CreateTestSpecificFolder(this.TestContext, System.Guid.NewGuid().ToString());
            string zipDir = Path.Combine(tempDir, "zipDir");
            Directory.CreateDirectory(zipDir);
            string zippedFilePath = Path.Combine(tempDir, fileName);

            // Create and read the zip file           
            foreach(string contentFileName in contentFileNames)
            {
                TestUtils.CreateTextFile(zipDir, contentFileName, "dummy file content");
            }
            
            ZipFile.CreateFromDirectory(zipDir, zippedFilePath);
            byte[] zipData = File.ReadAllBytes(zippedFilePath);

            // Cleanup
            Directory.Delete(tempDir, true);

            return zipData;
        }

        private static string[] CalculateExpectedCachedFilePaths(string baseDir, Plugin plugin, params string[] fileNames)
        {
            List<string> files = new List<string>();

            string pluginDir = EmbeddedAnalyzerInstaller.GetResourceSpecificDir(baseDir, plugin);

            foreach (string fileName in fileNames)
            {
                string fullFilePath = Path.Combine(pluginDir, fileName);
                files.Add(fullFilePath);
            }
            return files.ToArray();
        }

        private void AssertExpectedFilesReturned(IEnumerable<string> actualFilePaths, params string[] expectedFileNames)
        {
            foreach (string expected in expectedFileNames)
            {
                Assert.IsTrue(actualFilePaths.Contains(expected, StringComparer.OrdinalIgnoreCase), "Expected file does not exist: {0}", expected);
            }

            Assert.AreEqual(expectedFileNames.Length, actualFilePaths.Count(), "Too many files returned");
        }

        private void AssertExpectedFilesExist(params string[] expectedFileNames)
        {
            foreach (string expected in expectedFileNames)
            {
                Assert.IsTrue(File.Exists(expected), "Expected file does not exist: {0}", expected);
            }

        }

        private static void AssertExpectedFilesInCache(int expected, string localCacheDir)
        {
            string[] allActualFiles = Directory.GetFiles(localCacheDir, "*.*", SearchOption.AllDirectories);
            Assert.AreEqual(expected, allActualFiles.Count(), "Too many files found in the cache directory");
        }

        #endregion

    }
}
