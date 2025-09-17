﻿/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto: info AT sonarsource DOT com
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

using SonarScanner.MSBuild.PreProcessor.Roslyn;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class EmbeddedAnalyzerInstallerTests
{
    private const string DownloadEmbeddedFileMethodName = "TryDownloadEmbeddedFile";

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_NullSonarQubeServer_ThrowsArgumentNullException() =>
        ((Action)(() => new EmbeddedAnalyzerInstaller(
            null,
            "NonNullPath",
            new TestLogger()))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("server");

    [TestMethod]
    public void Constructor_NullLogger_ThrowsArgumentNullException() =>
        ((Action)(() => new EmbeddedAnalyzerInstaller(
            new MockSonarWebServer(),
            "NonNullPath",
            null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");

    [TestMethod]
    public void InstallAssemblies_NullPlugins_ThrowsArgumentNullException()
    {
        var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var embeddedAnalyzerInstaller = new EmbeddedAnalyzerInstaller(new MockSonarWebServer(), localCacheDir, new TestLogger());
        ((Action)(() => embeddedAnalyzerInstaller.InstallAssemblies(null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("plugins");
    }

    [TestMethod]
    public void EmbeddedInstall_SinglePlugin_SingleResource_Succeeds()
    {
        var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var logger = new TestLogger();
        var requestedPlugin = new Plugin() { Key = "plugin1", Version = "1.0", StaticResourceName = "embeddedFile1.zip" };
        var server = new MockSonarWebServer();
        AddPlugin(server, requestedPlugin, "file1.dll", "file2.txt");
        var expectedFilePaths = CalculateExpectedCachedFilePaths(localCacheDir, 0, "file1.dll", "file2.txt");
        var testSubject = new EmbeddedAnalyzerInstaller(server, localCacheDir, logger);

        var actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestedPlugin });

        logger.Should().HaveInfos("Processing plugin: plugin1 version 1.0");
        actualFiles.Should().NotBeNull("Returned list should not be null");
        AssertExpectedFilesReturned(expectedFilePaths, actualFiles);
        AssertExpectedFilesExist(expectedFilePaths);
        AssertExpectedFilesInCache(4, localCacheDir); // one zip containing two files
    }

    [TestMethod]
    public void EmbeddedInstall_TempPath_Succeeds()
    {
        var localCacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), ".sonarqube", "resources");
        try
        {
            var logger = new TestLogger();
            var requestedPlugin = new Plugin() { Key = "plugin1", Version = "1.0", StaticResourceName = "embeddedFile1.zip" };
            var server = new MockSonarWebServer();
            AddPlugin(server, requestedPlugin, "file1.dll", "file2.txt");
            var expectedFilePaths = CalculateExpectedCachedFilePaths(localCacheDir, 0, "file1.dll", "file2.txt");
            var testSubject = new EmbeddedAnalyzerInstaller(server, localCacheDir, logger);

            var actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestedPlugin });

            actualFiles.Should().NotBeNull("Returned list should not be null");
            AssertExpectedFilesReturned(expectedFilePaths, actualFiles);
            AssertExpectedFilesExist(expectedFilePaths);
            AssertExpectedFilesInCache(4, localCacheDir); // one zip containing two files
        }
        finally
        {
            if (Directory.Exists(localCacheDir))
            {
                Directory.Delete(localCacheDir, true);
            }
        }
    }

    [TestMethod]
    public void EmbeddedInstall_MultiplePlugins_Succeeds()
    {
        const string p1Resource1 = "p1.resource1";
        const string p1Resource2 = "p1.resource2";
        const string p2Resource1 = "p2.resource1";
        var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var logger = new TestLogger();
        var request1 = new Plugin() { Key = "plugin1", Version = "1.0", StaticResourceName = $"{p1Resource1}.zip" };
        var request2 = new Plugin() { Key = "plugin1", Version = "1.0", StaticResourceName = $"{p1Resource2}.zip" };
        var request3 = new Plugin() { Key = "plugin2", Version = "2.0", StaticResourceName = $"{p2Resource1}.zip" };
        var server = new MockSonarWebServer();
        AddPlugin(server, request1, $"{p1Resource1}.file1.dll", $"{p1Resource1}.file2.dll");
        AddPlugin(server, request2, $"{p1Resource2}.file1.dll");
        AddPlugin(server, request3, $"{p2Resource1}.dll");
        var expectedPaths = new List<string>();
        expectedPaths.AddRange(CalculateExpectedCachedFilePaths(localCacheDir, 0, $"{p1Resource1}.file1.dll", $"{p1Resource1}.file2.dll"));
        expectedPaths.AddRange(CalculateExpectedCachedFilePaths(localCacheDir, 1, $"{p1Resource2}.file1.dll"));
        expectedPaths.AddRange(CalculateExpectedCachedFilePaths(localCacheDir, 2, $"{p2Resource1}.dll"));
        var testSubject = new EmbeddedAnalyzerInstaller(server, localCacheDir, logger);

        var actualPlugins = testSubject.InstallAssemblies(new Plugin[] { request1, request2, request3 });

        actualPlugins.Should().NotBeNull("Returned list should not be null");
        AssertExpectedFilesReturned(expectedPaths, actualPlugins);
        AssertExpectedFilesExist(expectedPaths);
        // 8 = zip files + index file + contents
        AssertExpectedFilesInCache(8, localCacheDir);
    }

    [TestMethod]
    public void EmbeddedInstall_MissingResource_ThrowFileNotFoundException()
    {
        const string missingPluginKey = "could.be.anything";
        const string missingPluginVersion = "1.0";
        const string missingPluginResource = "resource.txt";
        var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var requestedPlugin = new Plugin() { Key = missingPluginKey, Version = missingPluginVersion, StaticResourceName = missingPluginResource };
        var testSubject = new EmbeddedAnalyzerInstaller(CreateServerWithDummyPlugin("plugin1"), localCacheDir, new TestLogger());

        Action act = () => testSubject.InstallAssemblies(new Plugin[] { requestedPlugin });

        act.Should().Throw<FileNotFoundException>().WithMessage($"Plugin resource not found: {missingPluginKey}, version {missingPluginVersion}. Resource: {missingPluginResource}.");
    }

    [TestMethod]
    public void EmbeddedInstall_NoPluginsSpecified_SucceedsButNoFiles()
    {
        var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var logger = new TestLogger();
        var server = CreateServerWithDummyPlugin("plugin1");
        var testSubject = new EmbeddedAnalyzerInstaller(server, localCacheDir, logger);

        var actualFiles = testSubject.InstallAssemblies(new Plugin[] { });

        actualFiles.Should().NotBeNull("Returned list should not be null");
        AssertExpectedFilesReturned(Enumerable.Empty<string>(), actualFiles);
        AssertExpectedFilesInCache(0, localCacheDir);
    }

    [TestMethod]
    public void EmbeddedInstall_PluginWithNoFiles_Succeeds()
    {
        var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var logger = new TestLogger();
        var request1 = new Plugin() { Key = "plugin1", Version = "1.0", StaticResourceName = "p1.resource1.zip" };
        var request2 = new Plugin() { Key = "plugin2", Version = "2.0", StaticResourceName = "p2.resource1.zip" };
        var server = new MockSonarWebServer();
        AddPlugin(server, request1, "p1.resource1.file1.dll", "p1.resource1.file2.dll");
        AddPlugin(server, request2 /* no assemblies */);
        var expectedPaths = new List<string>();
        expectedPaths.AddRange(CalculateExpectedCachedFilePaths(localCacheDir, 0, "p1.resource1.file1.dll", "p1.resource1.file2.dll"));
        var testSubject = new EmbeddedAnalyzerInstaller(server, localCacheDir, logger);

        IEnumerable<AnalyzerPlugin> actualPlugins;
        using (new AssertIgnoreScope())
        {
            actualPlugins = testSubject.InstallAssemblies(new Plugin[] { request1, request2 });
        }

        actualPlugins.Should().NotBeNull("Returned list should not be null");
        AssertExpectedFilesReturned(expectedPaths, actualPlugins);
        AssertExpectedFilesExist(expectedPaths);
        // 5 = zip files + index file + content files
        AssertExpectedFilesInCache(5, localCacheDir);
        actualPlugins.Select(p => p.Key).Should().BeEquivalentTo(new string[] { "plugin1" }); // plugin with no resources should not be included
    }

    [TestMethod]
    public void EmbeddedInstall_CachingScenarios()
    {
        var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var logger = new TestLogger();
        var requestA = new Plugin() { Key = "p111", Version = "1.0-SNAPSHOT", StaticResourceName = "p1.zip" };
        var requestB = new Plugin() { Key = "p222", Version = "9.1.3.0", StaticResourceName = "p2.zip" };
        var server = new MockSonarWebServer();
        AddPlugin(server, requestA, "aaa", "bbb");
        AddPlugin(server, requestB, "ccc");
        var expectedPlugin111Paths = CalculateExpectedCachedFilePaths(localCacheDir, 0, "aaa", "bbb");
        var expectedPlugin222Paths = CalculateExpectedCachedFilePaths(localCacheDir, 1, "ccc");
        var allExpectedPaths = new List<string>(expectedPlugin111Paths);
        allExpectedPaths.AddRange(expectedPlugin222Paths);
        var testSubject = new EmbeddedAnalyzerInstaller(server, localCacheDir, logger);

        AssertExpectedFilesInCache(0, localCacheDir); // cache should be empty to start with

        // 1. Empty cache -> cache miss -> server called
        var actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestA });
        server.AssertMethodCalled(DownloadEmbeddedFileMethodName, 1); // should have tried to download

        AssertExpectedFilesReturned(expectedPlugin111Paths, actualFiles);
        AssertExpectedFilesExist(expectedPlugin111Paths);
        AssertExpectedFilesInCache(4, localCacheDir); // only files for the first request should exist

        // 2. New request + request -> partial cache miss -> server called only for the new request
        actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestA, requestB });
        server.AssertMethodCalled(DownloadEmbeddedFileMethodName, 2); // new request

        AssertExpectedFilesReturned(allExpectedPaths, actualFiles);
        AssertExpectedFilesExist(allExpectedPaths);
        AssertExpectedFilesInCache(6, localCacheDir); // files for both plugins should exist

        // 3. Repeat the request -> cache hit -> server not called
        actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestA, requestB });
        server.AssertMethodCalled(DownloadEmbeddedFileMethodName, 2); // call count should not have changed

        AssertExpectedFilesReturned(allExpectedPaths, actualFiles);

        // 4. Clear the cache and request both -> cache miss -> multiple requests
        Directory.Delete(localCacheDir, true);
        Directory.Exists(localCacheDir).Should().BeFalse("Test error: failed to delete the local cache directory");

        actualFiles = testSubject.InstallAssemblies(new Plugin[] { requestA, requestB });
        server.AssertMethodCalled(DownloadEmbeddedFileMethodName, 4); // two new requests

        AssertExpectedFilesReturned(allExpectedPaths, actualFiles);
        AssertExpectedFilesExist(allExpectedPaths);
        AssertExpectedFilesInCache(6, localCacheDir); // files for both plugins should exist
    }

    /// <summary>
    /// Used by tests that don't care about the content of the plugin, just it's existence.
    /// </summary>
    private MockSonarWebServer CreateServerWithDummyPlugin(string languageKey)
    {
        var server = new MockSonarWebServer();
        server.Data.Languages.Add(languageKey);
        server.Data.AddEmbeddedZipFile(languageKey, "embeddedFile1.zip", "file1.dll", "file2.txt");
        return server;
    }

    private void AddPlugin(MockSonarWebServer server, Plugin plugin, params string[] files) =>
        server.Data.AddEmbeddedZipFile(plugin.Key, plugin.StaticResourceName, files);

    private static List<string> CalculateExpectedCachedFilePaths(string baseDir, int count, params string[] fileNames) =>
        fileNames.Select(x => Path.Combine(baseDir, count.ToString(), x)).ToList();

    private void AssertExpectedFilesReturned(IEnumerable<string> expectedFileNames, IEnumerable<AnalyzerPlugin> actualPlugins)
    {
        var actualFileList = actualPlugins.SelectMany(x => x.AssemblyPaths).ToArray();
        DumpFileList("Expected files", expectedFileNames);
        DumpFileList("Actual files", actualFileList);
        foreach (var expected in expectedFileNames)
        {
            actualFileList.Should().Contain(expected, "Expected file does not exist: {0}", expected);
        }
        actualFileList.Should().HaveSameCount(expectedFileNames, "Too many files returned");
    }

    private void DumpFileList(string title, IEnumerable<string> files)
    {
        TestContext.WriteLine(string.Empty);
        TestContext.WriteLine(title);
        TestContext.WriteLine("---------------");
        foreach (var file in files)
        {
            TestContext.WriteLine("\t{0}", file);
        }
        TestContext.WriteLine(string.Empty);
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
}
