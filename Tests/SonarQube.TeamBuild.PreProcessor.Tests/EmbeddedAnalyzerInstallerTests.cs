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

        #region Fetching from server tests

        [TestMethod]
        public void EmbeddedInstall_SinglePlugin_SingleResources_Succeeds()
        {
            // Arrange
            string localCacheDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            TestLogger logger = new TestLogger();

            Plugin requestedPlugin = new Plugin("plugin1", "1.0", "embeddedFile1.zip");
            MockSonarQubeServer mockServer = new MockSonarQubeServer();
            AddPlugin(mockServer, requestedPlugin, "file1.dll", "file2.txt");

            string[] expectedFilePaths = CalculateExpectedCachedFilePaths(localCacheDir, requestedPlugin, "embeddedFile1.zip", "file1.dll", "file2.txt");

            EmbeddedAnalyzerInstaller testSubject = new EmbeddedAnalyzerInstaller(mockServer, localCacheDir, logger);

            // Act
            IEnumerable<string> actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestedPlugin });

            // Assert
            Assert.IsNotNull(actualFiles, "Returned list should not be null");
            AssertExpectedFilesReturned(expectedFilePaths, actualFiles);
            AssertExpectedFilesExist(expectedFilePaths);
            AssertExpectedFilesInCache(3, localCacheDir); // one zip containing two files
        }

        [TestMethod]
        public void EmbeddedInstall_MultiplePlugins_Succeeds()
        {
            // Arrange
            string localCacheDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            TestLogger logger = new TestLogger();

            Plugin request1 = new Plugin("no.matching.resource.plugin", "2.0", "non.existent.resource.zip");
            Plugin request2 = new Plugin("plugin1", "1.0", "p1.resource1.zip");
            Plugin request3 = new Plugin("plugin1", "1.0", "p1.resource2.zip"); // second resource for plugin 1
            Plugin request4 = new Plugin("plugin2", "2.0", "p2.resource1.zip");

            MockSonarQubeServer mockServer = new MockSonarQubeServer();
            AddPlugin(mockServer, request2, "p1.resource1.file1.dll", "p1.resource1.file2.dll");
            AddPlugin(mockServer, request3, "p1.resource2.file1.dll");
            AddPlugin(mockServer, request4, "p2.resource1.dll");

            List<string> expectedPaths = new List<string>();
            expectedPaths.AddRange(CalculateExpectedCachedFilePaths(localCacheDir, request2, "p1.resource1.zip", "p1.resource1.file1.dll", "p1.resource1.file2.dll"));
            expectedPaths.AddRange(CalculateExpectedCachedFilePaths(localCacheDir, request3, "p1.resource2.zip", "p1.resource2.file1.dll"));
            expectedPaths.AddRange(CalculateExpectedCachedFilePaths(localCacheDir, request4, "p2.resource1.zip", "p2.resource1.dll"));

            EmbeddedAnalyzerInstaller testSubject = new EmbeddedAnalyzerInstaller(mockServer, localCacheDir, logger);

            // Act
            IEnumerable<string> actualFiles = testSubject.InstallAssemblies(new Plugin[] { request1, request2, request3, request4});

            // Assert
            Assert.IsNotNull(actualFiles, "Returned list should not be null");
            AssertExpectedFilesReturned(expectedPaths, actualFiles);
            AssertExpectedFilesExist(expectedPaths);
            AssertExpectedFilesInCache(expectedPaths.Count, localCacheDir);
        }

        [TestMethod]
        public void EmbeddedInstall_MissingResource_SucceedsWithWarningAndNoFiles()
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
            AssertExpectedFilesReturned(Enumerable.Empty<string>(), actualFiles);
            AssertExpectedFilesInCache(0, localCacheDir);

            logger.AssertWarningsLogged(1);
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
            AssertExpectedFilesReturned(Enumerable.Empty<string>(), actualFiles);
            AssertExpectedFilesInCache(0, localCacheDir);
        }

        #endregion

        #region Caching tests

        #endregion

        #region Private methods

        /// <summary>
        /// Used by tests that don't care about the content of the plugin, just it's existence
        /// </summary>
        private MockSonarQubeServer CreateServerWithDummyPlugin(string pluginKey)
        {
            MockSonarQubeServer mockServer = new MockSonarQubeServer();
            mockServer.Data.InstalledPlugins.Add(pluginKey);
            mockServer.Data.AddEmbeddedFile(pluginKey, "embeddedFile1.zip", CreateDummyZipFile("file1.dll", "file2.txt"));
            return mockServer;
        }
        
        private void AddPlugin(MockSonarQubeServer mockServer, Plugin plugin, params string[] files)
        {
            mockServer.Data.InstalledPlugins.Add(plugin.Key);
            mockServer.Data.AddEmbeddedFile(plugin.Key, plugin.StaticResourceName, CreateDummyZipFile(files));
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

        private void AssertExpectedFilesReturned(IEnumerable<string> expectedFileNames, IEnumerable<string> actualFilePaths)
        {
            DumpFileList("Expected files", expectedFileNames);
            DumpFileList("Actual files", actualFilePaths);

            foreach (string expected in expectedFileNames)
            {
                Assert.IsTrue(actualFilePaths.Contains(expected, StringComparer.OrdinalIgnoreCase), "Expected file does not exist: {0}", expected);
            }

            Assert.AreEqual(expectedFileNames.Count(), actualFilePaths.Count(), "Too many files returned");
        }

        private void DumpFileList(string title, IEnumerable<string> files)
        {
            this.TestContext.WriteLine("");
            this.TestContext.WriteLine(title);
            this.TestContext.WriteLine("---------------");
            foreach (string file in files)
            {
                this.TestContext.WriteLine("\t{0}", file);
            }
            this.TestContext.WriteLine("");
        }

        private void AssertExpectedFilesExist(IEnumerable<string> expectedFileNames)
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
