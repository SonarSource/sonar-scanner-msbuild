/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

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

        private const string DownloadEmbeddedFileMethodName = "TryDownloadEmbeddedFile";

        #region Fetching from server tests

        [TestMethod]
        public void EmbeddedInstall_SinglePlugin_SingleResource_Succeeds()
        {
            // Arrange
            string localCacheDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            TestLogger logger = new TestLogger();

            Plugin requestedPlugin = new Plugin("plugin1", "1.0", "embeddedFile1.zip");
            MockSonarQubeServer mockServer = new MockSonarQubeServer();
            AddPlugin(mockServer, requestedPlugin, "file1.dll", "file2.txt");

            IList<string> expectedFilePaths = CalculateExpectedCachedFilePaths(localCacheDir, requestedPlugin, "embeddedFile1.zip", "file1.dll", "file2.txt");

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

        [TestMethod]
        public void EmbeddedInstall_CachingScenarios()
        {
            // Arrange
            string localCacheDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            TestLogger logger = new TestLogger();

            Plugin requestA = new Plugin("p111", "1.0-SNAPSHOT", "p1.zip");
            Plugin requestB = new Plugin("p222", "9.1.3.0", "p2.zip");

            MockSonarQubeServer mockServer = new MockSonarQubeServer();
            AddPlugin(mockServer, requestA, "aaa", "bbb");
            AddPlugin(mockServer, requestB, "ccc");

            IList<string> expectedPlugin111Paths = CalculateExpectedCachedFilePaths(localCacheDir, requestA, "p1.zip", "aaa", "bbb");
            IList<string> expectedPlugin222Paths = CalculateExpectedCachedFilePaths(localCacheDir, requestB, "p2.zip", "ccc");
            List<string> allExpectedPaths = new List<string>(expectedPlugin111Paths);
            allExpectedPaths.AddRange(expectedPlugin222Paths);

            EmbeddedAnalyzerInstaller testSubject = new EmbeddedAnalyzerInstaller(mockServer, localCacheDir, logger);

            AssertExpectedFilesInCache(0, localCacheDir); // cache should be empty to start with


            // 1. Empty cache -> cache miss -> server called
            IEnumerable<string> actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestA });
            mockServer.AssertMethodCalled(DownloadEmbeddedFileMethodName, 1); // should have tried to download

            AssertExpectedFilesReturned(expectedPlugin111Paths, actualFiles);
            AssertExpectedFilesExist(expectedPlugin111Paths);
            AssertExpectedFilesInCache(3, localCacheDir); // only files for the first request should exist


            // 2. New request + request request -> partial cache miss -> server called only for the new request
            actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestA, requestB });
            mockServer.AssertMethodCalled(DownloadEmbeddedFileMethodName, 2); // new request

            AssertExpectedFilesReturned(allExpectedPaths, actualFiles);
            AssertExpectedFilesExist(allExpectedPaths);
            AssertExpectedFilesInCache(5, localCacheDir); // files for both plugins should exist


            // 3. Repeat the request -> cache hit -> server not called
            actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestA, requestB });
            mockServer.AssertMethodCalled(DownloadEmbeddedFileMethodName, 2); // call count should not have changed

            AssertExpectedFilesReturned(allExpectedPaths, actualFiles);

            // 4. Clear the cache and request both -> cache miss -> multiple requests
            Directory.Delete(localCacheDir, true);
            Assert.IsFalse(Directory.Exists(localCacheDir), "Test error: failed to delete the local cache directory");

            actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestA, requestB });
            mockServer.AssertMethodCalled(DownloadEmbeddedFileMethodName, 4); // two new requests

            AssertExpectedFilesReturned(allExpectedPaths, actualFiles);
            AssertExpectedFilesExist(allExpectedPaths);
            AssertExpectedFilesInCache(5, localCacheDir); // files for both plugins should exist
        }

        [TestMethod]
        public void EmbeddedInstall_EmptyCacheDirectoryExists_CacheMissAndServerCalled()
        {
            // Arrange
            string localCacheDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            TestLogger logger = new TestLogger();

            Plugin requestA = new Plugin("p111", "1.0-SNAPSHOT", "p1.zip");

            MockSonarQubeServer mockServer = new MockSonarQubeServer();
            AddPlugin(mockServer, requestA, "aaa.txt");

            IList<string> expectedPlugin111Paths = CalculateExpectedCachedFilePaths(localCacheDir, requestA, "p1.zip", "aaa.txt");
            Assert.AreNotEqual(0, expectedPlugin111Paths.Count, "Test setup error: expecting at least one file path");

            // Create the expected directories, but not the files
            foreach(string file in expectedPlugin111Paths)
            {
                string dir = Path.GetDirectoryName(file);
                Directory.CreateDirectory(dir);
            }

            AssertExpectedFilesInCache(0, localCacheDir); // cache should be empty to start with
            Assert.AreNotEqual(0, Directory.GetDirectories(localCacheDir, "*.*", SearchOption.AllDirectories)); // ... but should have directories


            EmbeddedAnalyzerInstaller testSubject = new EmbeddedAnalyzerInstaller(mockServer, localCacheDir, logger);

            // 1. Empty directory = cache miss -> server called
            IEnumerable<string> actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestA });
            mockServer.AssertMethodCalled(DownloadEmbeddedFileMethodName, 1); // should have tried to download

            AssertExpectedFilesReturned(expectedPlugin111Paths, actualFiles);
            AssertExpectedFilesExist(expectedPlugin111Paths);
            AssertExpectedFilesInCache(2, localCacheDir);
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Used by tests that don't care about the content of the plugin, just it's existence
        /// </summary>
        private MockSonarQubeServer CreateServerWithDummyPlugin(string pluginKey)
        {
            MockSonarQubeServer mockServer = new MockSonarQubeServer();
            mockServer.Data.InstalledPlugins.Add(pluginKey);
            mockServer.Data.AddEmbeddedZipFile(pluginKey, "embeddedFile1.zip", "file1.dll", "file2.txt");
            return mockServer;
        }
        
        private void AddPlugin(MockSonarQubeServer mockServer, Plugin plugin, params string[] files)
        {
            mockServer.Data.InstalledPlugins.Add(plugin.Key);
            mockServer.Data.AddEmbeddedZipFile(plugin.Key, plugin.StaticResourceName, files);
        }

        private static IList<string> CalculateExpectedCachedFilePaths(string baseDir, Plugin plugin, params string[] fileNames)
        {
            List<string> files = new List<string>();

            string pluginDir = EmbeddedAnalyzerInstaller.GetResourceSpecificDir(baseDir, plugin);

            foreach (string fileName in fileNames)
            {
                string fullFilePath = Path.Combine(pluginDir, fileName);
                files.Add(fullFilePath);
            }
            return files;
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
