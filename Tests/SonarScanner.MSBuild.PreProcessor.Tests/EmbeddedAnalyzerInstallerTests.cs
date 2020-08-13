/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Tests
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
            var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var logger = new TestLogger();

            var requestedPlugin = new Plugin("plugin1", "1.0", "embeddedFile1.zip");
            var mockServer = new MockSonarQubeServer();
            AddPlugin(mockServer, requestedPlugin, "file1.dll", "file2.txt");

            var expectedFilePaths = CalculateExpectedCachedFilePaths(localCacheDir, 0, "file1.dll", "file2.txt");

            var testSubject = new EmbeddedAnalyzerInstaller(mockServer, localCacheDir, logger);

            // Act
            var actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestedPlugin });

            // Assert
            actualFiles.Should().NotBeNull("Returned list should not be null");
            AssertExpectedFilesReturned(expectedFilePaths, actualFiles);
            AssertExpectedFilesExist(expectedFilePaths);
            AssertExpectedFilesInCache(4, localCacheDir); // one zip containing two files
        }

        [TestMethod]
        public void EmbeddedInstall_MultiplePlugins_Succeeds()
        {
            // Arrange
            var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var logger = new TestLogger();

            var request1 = new Plugin("no.matching.resource.plugin", "2.0", "non.existent.resource.zip");
            var request2 = new Plugin("plugin1", "1.0", "p1.resource1.zip");
            var request3 = new Plugin("plugin1", "1.0", "p1.resource2.zip"); // second resource for plugin 1
            var request4 = new Plugin("plugin2", "2.0", "p2.resource1.zip");

            var mockServer = new MockSonarQubeServer();
            AddPlugin(mockServer, request2, "p1.resource1.file1.dll", "p1.resource1.file2.dll");
            AddPlugin(mockServer, request3, "p1.resource2.file1.dll");
            AddPlugin(mockServer, request4, "p2.resource1.dll");

            var expectedPaths = new List<string>();
            expectedPaths.AddRange(CalculateExpectedCachedFilePaths(localCacheDir, 1, "p1.resource1.file1.dll", "p1.resource1.file2.dll"));
            expectedPaths.AddRange(CalculateExpectedCachedFilePaths(localCacheDir, 2, "p1.resource2.file1.dll"));
            expectedPaths.AddRange(CalculateExpectedCachedFilePaths(localCacheDir, 3, "p2.resource1.dll"));

            var testSubject = new EmbeddedAnalyzerInstaller(mockServer, localCacheDir, logger);

            // Act
            var actualPlugins = testSubject.InstallAssemblies(new Plugin[] { request1, request2, request3, request4 });

            // Assert
            actualPlugins.Should().NotBeNull("Returned list should not be null");
            AssertExpectedFilesReturned(expectedPaths, actualPlugins);
            AssertExpectedFilesExist(expectedPaths);
            // 8 = zip files + index file + contents
            AssertExpectedFilesInCache(8, localCacheDir);
        }

        [TestMethod]
        public void EmbeddedInstall_MissingResource_SucceedsWithWarningAndNoFiles()
        {
            // Arrange
            var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var logger = new TestLogger();
            var mockServer = CreateServerWithDummyPlugin("plugin1");

            var requestedPlugin = new Plugin() { Key = "missingPlugin", Version = "1.0", StaticResourceName = "could.be.anything" };

            var testSubject = new EmbeddedAnalyzerInstaller(mockServer, localCacheDir, logger);

            // Act
            var actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestedPlugin });

            // Assert
            actualFiles.Should().NotBeNull("Returned list should not be null");
            AssertExpectedFilesReturned(Enumerable.Empty<string>(), actualFiles);
            AssertExpectedFilesInCache(1, localCacheDir);  // the index file

            logger.AssertWarningsLogged(1);
        }

        [TestMethod]
        public void EmbeddedInstall_NoPluginsSpecified_SucceedsButNoFiles()
        {
            // Arrange
            var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var logger = new TestLogger();
            var mockServer = CreateServerWithDummyPlugin("plugin1");

            var testSubject = new EmbeddedAnalyzerInstaller(mockServer, localCacheDir, logger);

            // Act
            var actualFiles = testSubject.InstallAssemblies(new Plugin[] { });

            // Assert
            actualFiles.Should().NotBeNull("Returned list should not be null");
            AssertExpectedFilesReturned(Enumerable.Empty<string>(), actualFiles);
            AssertExpectedFilesInCache(0, localCacheDir);
        }

        [TestMethod]
        public void EmbeddedInstall_PluginWithNoFiles_Succeeds()
        {
            // Arrange
            var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var logger = new TestLogger();

            var request1 = new Plugin("plugin1", "1.0", "p1.resource1.zip");
            var request2 = new Plugin("plugin2", "2.0", "p2.resource1.zip");
            
            var mockServer = new MockSonarQubeServer();
            AddPlugin(mockServer, request1, "p1.resource1.file1.dll", "p1.resource1.file2.dll");
            AddPlugin(mockServer, request2 /* no assemblies */ );
            
            var expectedPaths = new List<string>();
            expectedPaths.AddRange(CalculateExpectedCachedFilePaths(localCacheDir, 0, "p1.resource1.file1.dll", "p1.resource1.file2.dll"));

            var testSubject = new EmbeddedAnalyzerInstaller(mockServer, localCacheDir, logger);

            // Act
            IEnumerable<AnalyzerPlugin> actualPlugins;
            using (new AssertIgnoreScope())
            {
                actualPlugins = testSubject.InstallAssemblies(new Plugin[] { request1, request2 });
            }

            // Assert
            actualPlugins.Should().NotBeNull("Returned list should not be null");
            AssertExpectedFilesReturned(expectedPaths, actualPlugins);
            AssertExpectedFilesExist(expectedPaths);
            // 5 = zip files + index file + content files
            AssertExpectedFilesInCache(5, localCacheDir);
            actualPlugins.Select(p => p.Key).Should().BeEquivalentTo(new string[] { "plugin1" }); // plugin with no resources should not be included
        }

        #endregion Fetching from server tests

        #region Caching tests

        [TestMethod]
        public void EmbeddedInstall_CachingScenarios()
        {
            // Arrange
            var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var logger = new TestLogger();

            var requestA = new Plugin("p111", "1.0-SNAPSHOT", "p1.zip");
            var requestB = new Plugin("p222", "9.1.3.0", "p2.zip");

            var mockServer = new MockSonarQubeServer();
            AddPlugin(mockServer, requestA, "aaa", "bbb");
            AddPlugin(mockServer, requestB, "ccc");

            var expectedPlugin111Paths = CalculateExpectedCachedFilePaths(localCacheDir, 0, "aaa", "bbb");
            var expectedPlugin222Paths = CalculateExpectedCachedFilePaths(localCacheDir, 1, "ccc");
            var allExpectedPaths = new List<string>(expectedPlugin111Paths);
            allExpectedPaths.AddRange(expectedPlugin222Paths);

            var testSubject = new EmbeddedAnalyzerInstaller(mockServer, localCacheDir, logger);

            AssertExpectedFilesInCache(0, localCacheDir); // cache should be empty to start with

            // 1. Empty cache -> cache miss -> server called
            var actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestA });
            mockServer.AssertMethodCalled(DownloadEmbeddedFileMethodName, 1); // should have tried to download

            AssertExpectedFilesReturned(expectedPlugin111Paths, actualFiles);
            AssertExpectedFilesExist(expectedPlugin111Paths);
            AssertExpectedFilesInCache(4, localCacheDir); // only files for the first request should exist

            // 2. New request + request -> partial cache miss -> server called only for the new request
            actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestA, requestB });
            mockServer.AssertMethodCalled(DownloadEmbeddedFileMethodName, 2); // new request

            AssertExpectedFilesReturned(allExpectedPaths, actualFiles);
            AssertExpectedFilesExist(allExpectedPaths);
            AssertExpectedFilesInCache(6, localCacheDir); // files for both plugins should exist

            // 3. Repeat the request -> cache hit -> server not called
            actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestA, requestB });
            mockServer.AssertMethodCalled(DownloadEmbeddedFileMethodName, 2); // call count should not have changed

            AssertExpectedFilesReturned(allExpectedPaths, actualFiles);

            // 4. Clear the cache and request both -> cache miss -> multiple requests
            Directory.Delete(localCacheDir, true);
            Directory.Exists(localCacheDir).Should().BeFalse("Test error: failed to delete the local cache directory");

            actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestA, requestB });
            mockServer.AssertMethodCalled(DownloadEmbeddedFileMethodName, 4); // two new requests

            AssertExpectedFilesReturned(allExpectedPaths, actualFiles);
            AssertExpectedFilesExist(allExpectedPaths);
            AssertExpectedFilesInCache(6, localCacheDir); // files for both plugins should exist
        }

        #endregion Caching tests

        #region Private methods

        /// <summary>
        /// Used by tests that don't care about the content of the plugin, just it's existence
        /// </summary>
        private MockSonarQubeServer CreateServerWithDummyPlugin(string languageKey)
        {
            var mockServer = new MockSonarQubeServer();
            mockServer.Data.Languages.Add(languageKey);
            mockServer.Data.AddEmbeddedZipFile(languageKey, "embeddedFile1.zip", "file1.dll", "file2.txt");
            return mockServer;
        }

        private void AddPlugin(MockSonarQubeServer mockServer, Plugin plugin, params string[] files)
        {
            mockServer.Data.AddEmbeddedZipFile(plugin.Key, plugin.StaticResourceName, files);
        }

        private static IList<string> CalculateExpectedCachedFilePaths(string baseDir, int count, params string[] fileNames)
        {
            var files = new List<string>();

            var pluginDir = Path.Combine(baseDir, count.ToString());

            foreach (var fileName in fileNames)
            {
                var fullFilePath = Path.Combine(pluginDir, fileName);
                files.Add(fullFilePath);
            }
            return files;
        }

        private void AssertExpectedFilesReturned(IEnumerable<string> expectedFileNames, IEnumerable<AnalyzerPlugin> actualPlugins)
        {
            var actualFileList = actualPlugins.SelectMany(x => x.AssemblyPaths).ToArray();
            DumpFileList("Expected files", expectedFileNames);
            DumpFileList("Actual files", actualFileList);

            foreach (var expected in expectedFileNames)
            {
                actualFileList.Contains(expected, StringComparer.OrdinalIgnoreCase).Should().BeTrue("Expected file does not exist: {0}", expected);
            }

            actualFileList.Should().HaveSameCount(expectedFileNames, "Too many files returned");
        }

        private void DumpFileList(string title, IEnumerable<string> files)
        {
            TestContext.WriteLine("");
            TestContext.WriteLine(title);
            TestContext.WriteLine("---------------");
            foreach (var file in files)
            {
                TestContext.WriteLine("\t{0}", file);
            }
            TestContext.WriteLine("");
        }

        private void AssertExpectedFilesExist(IEnumerable<string> expectedFileNames)
        {
            foreach (var expected in expectedFileNames)
            {
                File.Exists(expected).Should().BeTrue("Expected file does not exist: {0}", expected);
            }
        }

        private static void AssertExpectedFilesInCache(int expected, string localCacheDir)
        {
            var allActualFiles = Directory.GetFiles(localCacheDir, "*.*", SearchOption.AllDirectories);
            allActualFiles.Should().HaveCount(expected, "Too many files found in the cache directory");
        }

        #endregion Private methods
    }
}
