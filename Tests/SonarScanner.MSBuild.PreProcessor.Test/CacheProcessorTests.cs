/*
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

using Google.Protobuf;
using SonarScanner.MSBuild.PreProcessor.Protobuf;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class CacheProcessorTests
{
    private static readonly Version SonarQubeVersion99 = new(9, 9);
    private TestLogger logger;
    private ISonarWebServer server;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Initialize()
    {
        logger = new();
        server = Substitute.For<ISonarWebServer>();
    }

    [TestMethod]
    public void Constructor_NullArguments_Throws()
    {
        var locals = CreateProcessedArgs();
        var builds = Substitute.For<IBuildSettings>();
        ((Func<CacheProcessor>)(() => new CacheProcessor(server, locals, builds, logger))).Should().NotThrow();
        ((Func<CacheProcessor>)(() => new CacheProcessor(null, locals, builds, logger))).Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("server");
        ((Func<CacheProcessor>)(() => new CacheProcessor(server, null, builds, logger))).Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("localSettings");
        ((Func<CacheProcessor>)(() => new CacheProcessor(server, locals, null, logger))).Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("buildSettings");
        ((Func<CacheProcessor>)(() => new CacheProcessor(server, locals, builds, null))).Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void ContentHash_ComputeUniqueHash()
    {
        const string allNewLines = "public class Sample\n{\n\r\tint field;\n\r}\r";
        const string diacritics = "ěščřžýáí";
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var sut = CreateSut();
        var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var emptyWithBom = CreateFile(root, "EmptyWithBom.cs", string.Empty, Encoding.UTF8);
        var emptyNoBom = CreateFile(root, "EmptyNoBom.cs", string.Empty, Encoding.ASCII);
        var codeWithBom = CreateFile(root, "CodeWithBom.cs", allNewLines, Encoding.UTF8);
        var codeNoBom = CreateFile(root, "CodeNoBom.cs", allNewLines, Encoding.ASCII);
        var utf8 = CreateFile(root, "Utf8.cs", diacritics, Encoding.UTF8);
        var utf16 = CreateFile(root, "Utf16.cs", diacritics, Encoding.Unicode);
        var ansi = CreateFile(root, "Ansi.cs", diacritics, Encoding.GetEncoding(1250));
        File.ReadAllBytes(emptyWithBom).Should().HaveCount(3, "UTF8 encoding should generate BOM");
        File.ReadAllBytes(emptyNoBom).Should().BeEmpty("ASCII encoding should not generate BOM");

        Serialize(sut.ContentHash(emptyWithBom)).Should().Be("f1945cd6c19e56b3c1c78943ef5ec18116907a4ca1efc40a57d48ab1db7adfc5");
        Serialize(sut.ContentHash(emptyNoBom)).Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        Serialize(sut.ContentHash(codeWithBom)).Should().Be("b98aaf2ce5a3f9cdf8ab785563951f2309d577baa6351098f78908300fdc610a");
        Serialize(sut.ContentHash(codeNoBom)).Should().Be("8c7535a8e3679bf8cc241b5749cef5fc38243401556f2b7869495c7b48ee4980");
        Serialize(sut.ContentHash(utf8)).Should().Be("13aa54e315a806270810f3a91501f980a095a2ef1bcc53167d4c750a1b78684d");
        Serialize(sut.ContentHash(utf16)).Should().Be("a9b3c4402770855d090ba4b49adeb5ad601cb3bbd6de18495302f45f242ef932");
        Serialize(sut.ContentHash(ansi)).Should().Be("b965073262109da4f106cd90a5eeea025e2441c244af272537afa2cfb03c3ab8");
    }

    [TestMethod]
    public void ContentHash_IsDeterministic()
    {
        using var sut = CreateSut();
        var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var path = CreateFile(root, "File.txt", "Lorem ipsum", Encoding.UTF8);
        var hash1 = sut.ContentHash(path);
        var hash2 = sut.ContentHash(path);
        var hash3 = sut.ContentHash(path);
        hash2.SequenceEqual(hash1).Should().BeTrue();
        hash3.SequenceEqual(hash1).Should().BeTrue();
    }

    [TestMethod]
    public void PullRequestCacheBasePath_ProjectBaseDir_HasPriority_IsAbsolute()
    {
        var workingDirectory = TestContext.ResultsDirectory;
        using var scope = new WorkingDirectoryScope(workingDirectory);
        var localSettings = ArgumentProcessor.TryProcessArgs(["/k:key", "/d:sonar.projectBaseDir=Custom"], new TestRuntime { Logger = logger });
        var buildSettings = Substitute.For<IBuildSettings>();
        buildSettings.SourcesDirectory.Returns(@"C:\Sources\Directory");
        buildSettings.SonarScannerWorkingDirectory.Returns(@"C:\SonarScanner\WorkingDirectory");
        using var sut = new CacheProcessor(Substitute.For<ISonarWebServer>(), localSettings, buildSettings, logger);

        sut.PullRequestCacheBasePath.Should().Be(Path.Combine(workingDirectory, "Custom"));
    }

    [TestMethod]
    public void PullRequestCacheBasePath_NoProjectBaseDir_UsesSourcesDirectory()
    {
        var buildSettings = Substitute.For<IBuildSettings>();
        buildSettings.SourcesDirectory.Returns(Path.Combine(TestUtils.DriveRoot(), "Sources", "Directory"));
        buildSettings.SonarScannerWorkingDirectory.Returns(TestUtils.DriveRoot(), "SonarScanner", "WorkingDirectory");
        using var sut = CreateSut(buildSettings);

        sut.PullRequestCacheBasePath.Should().Be(Path.Combine(TestUtils.DriveRoot(), "Sources", "Directory"));
    }

    [TestMethod]
    public void PullRequestCacheBasePath_NoSourcesDirectory_UsesSonarScannerWorkingDirectory()
    {
        var buildSettings = Substitute.For<IBuildSettings>();
        buildSettings.SonarScannerWorkingDirectory.Returns(TestUtils.DriveRoot(), "SonarScanner", "WorkingDirectory");
        using var sut = CreateSut(buildSettings);

        sut.PullRequestCacheBasePath.Should().Be(TestUtils.DriveRoot(), "SonarScanner", "WorkingDirectory");
    }

    [TestMethod]
    public void PullRequestCacheBasePath_NoSonarScannerWorkingDirectory_IsNull()
    {
        using var sut = CreateSut();

        sut.PullRequestCacheBasePath.Should().Be(null);
    }

    [TestMethod]
    [DataRow("", null)]
    [DataRow(null, "")]
    public void PullRequestCacheBasePath_EmptyDirectories_IsNull(string sourcesDirectory, string sonarScannerWorkingDirectory)
    {
        var buildSettings = Substitute.For<IBuildSettings>();
        buildSettings.SourcesDirectory.Returns(sourcesDirectory);
        buildSettings.SonarScannerWorkingDirectory.Returns(sonarScannerWorkingDirectory);
        using var sut = CreateSut(buildSettings);

        sut.PullRequestCacheBasePath.Should().Be(null);
    }

    [TestMethod]
    public async Task Execute_PullRequest_NoBasePath()
    {
        using var sut = new CacheProcessor(server, CreateProcessedArgs("/k:key /d:sonar.pullrequest.base=master"), Substitute.For<IBuildSettings>(), logger);
        await sut.Execute();

        logger.AssertSingleInfoExists("Cannot determine project base path. Incremental PR analysis is disabled.");
        sut.UnchangedFilesPath.Should().BeNull();
    }

    [TestMethod]
    public async Task Execute_PullRequest_CacheNull()
    {
        var settings = Substitute.For<IBuildSettings>();
        settings.SourcesDirectory.Returns(@"C:\Sources");
        using var sut = new CacheProcessor(server, CreateProcessedArgs("/k:key /d:sonar.pullrequest.base=TARGET_BRANCH"), settings, logger);
        await sut.Execute();

        logger.AssertInfoLogged("Cache data is empty. A full analysis will be performed.");
        sut.UnchangedFilesPath.Should().BeNull();
    }

    [TestMethod]
    public async Task Execute_PullRequest_CacheEmpty()
    {
        var context = new CacheContext(this, "/k:key-no-cache /d:sonar.pullrequest.base=TARGET_BRANCH");
        await context.Sut.Execute();

        logger.AssertInfoLogged("Cache data is empty. A full analysis will be performed.");
        context.Sut.UnchangedFilesPath.Should().BeNull();
    }

    [TestMethod]
    public async Task Execute_PullRequest_CacheHappyFlow()
    {
        var context = new CacheContext(this, "/k:key /d:sonar.pullrequest.base=TARGET_BRANCH");
        await context.Sut.Execute();

        logger.AssertDebugLogged($"Using cache base path: {context.Root}");
        context.Sut.UnchangedFilesPath.Should().EndWith("UnchangedFiles.txt");
    }

    [TestMethod]
    public void ProcessPullRequest_EmptyCache_DoesNotProduceOutput()
    {
        using var sut = CreateSut();
        sut.ProcessPullRequest(Array.Empty<SensorCacheEntry>());

        sut.UnchangedFilesPath.Should().BeNull();
        logger.AssertInfoLogged("Incremental PR analysis: 0 files out of 0 are unchanged.");
    }

    [TestMethod]
    public void ProcessPullRequest_AllFilesChanged_DoesNotProduceOutput()
    {
        using var context = new CacheContext(this);
        foreach (var path in context.Paths)
        {
            File.WriteAllText(path, "Content of this file has changed");
        }
        context.ProcessPullRequest();

        context.Sut.UnchangedFilesPath.Should().BeNull();
        logger.AssertInfoLogged("Incremental PR analysis: 0 files out of 3 are unchanged.");
    }

    [TestMethod]
    public void ProcessPullRequest_NoFilesChanged_ProducesAllFiles()
    {
        using var context = new CacheContext(this);
        context.ProcessPullRequest();

        context.Sut.UnchangedFilesPath.Should().EndWith("UnchangedFiles.txt");
        File.ReadAllLines(context.Sut.UnchangedFilesPath).Should().BeEquivalentTo(context.Paths);
        logger.AssertInfoLogged("Incremental PR analysis: 3 files out of 3 are unchanged.");
    }

    [TestMethod]
    public void ProcessPullRequest_SomeFilesChanged_ProducesOnlyUnchangedFiles()
    {
        using var context = new CacheContext(this);
        CreateFile(context.Root, "AddedFile.cs", "// This file is not in cache", Encoding.UTF8);
        File.Delete(context.Paths[0]);       // This file was in cache, but doesn't exist anymore
        File.WriteAllText(context.Paths[context.Paths.Count - 1], " // This file was modified");
        context.ProcessPullRequest();

        context.Sut.UnchangedFilesPath.Should().EndWith("UnchangedFiles.txt");
        File.ReadAllLines(context.Sut.UnchangedFilesPath).Should().BeEquivalentTo(context.Paths[1]);  // Only a single file was not modified
        logger.AssertInfoLogged("Incremental PR analysis: 1 files out of 3 are unchanged.");
    }

    [TestMethod]
    public void ProcessPullRequest_UnexpectedCacheKeys()
    {
        var settings = Substitute.For<IBuildSettings>();
        settings.SonarScannerWorkingDirectory.Returns(@"C:\ValidBasePath");
        using var sut = CreateSut(settings);
        var cache = new SensorCacheEntry[]
                    {
                        new() { Key = new(Path.GetInvalidFileNameChars()), Data = ByteString.Empty},
                        new() { Key = new(Path.GetInvalidPathChars()), Data = ByteString.Empty},
                        new() { Key = string.Empty, Data = ByteString.Empty},
                        new() { Key = "  ", Data = ByteString.Empty},
                        new() { Key = "\t", Data = ByteString.Empty},
                        new() { Key = "\n", Data = ByteString.Empty},
                        new() { Key = "\r", Data = ByteString.Empty}
                    };

        sut.ProcessPullRequest(cache);

        sut.UnchangedFilesPath.Should().BeNull();
        logger.AssertInfoLogged("Incremental PR analysis: 0 files out of 7 are unchanged.");
    }

    private CacheProcessor CreateSut(IBuildSettings buildSettings = null) =>
        new(server, CreateProcessedArgs(), buildSettings ?? Substitute.For<IBuildSettings>(), logger);

    private ProcessedArgs CreateProcessedArgs(string commandLineArgs = "/k:key") =>
        CreateProcessedArgs(logger, commandLineArgs);

    private static ProcessedArgs CreateProcessedArgs(TestLogger logger, string commandLineArgs = "/k:key")
    {
        // When CI is run for a PR, AzureDevOps extension sets this to the actual PR analysis of S4NET project.
        using var scope = new EnvironmentVariableScope().SetVariable("SONARQUBE_SCANNER_PARAMS", null);
        var processedArgs = ArgumentProcessor.TryProcessArgs(commandLineArgs.Split(' '), new TestRuntime { Logger = logger });
        processedArgs.Should().NotBeNull();
        return processedArgs;
    }

    private static string Serialize(byte[] value) =>
        string.Concat(value.Select(x => x.ToString("x2")));

    private static string CreateFile(string root, string fileName, string content, Encoding encoding)
    {
        var path = Path.Combine(root, fileName);
        Directory.CreateDirectory(Directory.GetParent(path).FullName); // Ensures that all file path directories are created.
        File.WriteAllText(path, content, encoding);
        return path;
    }

    private sealed class CacheContext : IDisposable
    {
        public readonly string Root;
        public readonly List<string> Paths = [];
        public readonly CacheProcessor Sut;

        private readonly List<SensorCacheEntry> cache = [];

        public CacheContext(CacheProcessorTests owner, string commandLineArgs = "/k:key")
        {
            // The paths in the cache are:
            // - serialized with Unix separator `/`
            // - are part of a directory structure
            // The tests should reflect that.
            (string RelativeFilePath, string Content)[] fileData =
            [
                new("Project/File1.cs", "// Hello"),
                new("Project/Common/File2.cs", "// Hello World"),
                new("File3.vb", "' Hello World!")
            ];
            Root = TestUtils.CreateTestSpecificFolderWithSubPaths(owner.TestContext);
            var runtime = new TestRuntime { Logger = owner.logger };
            var factory = new MockObjectFactory(runtime);
            var settings = Substitute.For<IBuildSettings>();
            settings.SourcesDirectory.Returns(Root);
            settings.SonarConfigDirectory.Returns(Root);
            factory.Server.Cache = cache;
            factory.Server.Data.SonarQubeVersion = SonarQubeVersion99;
            Sut = new CacheProcessor(factory.Server, CreateProcessedArgs(runtime.Logger, commandLineArgs), settings, runtime.Logger);
            foreach (var (relativeFilePath, content) in fileData)
            {
                var fullFilePath = Path.GetFullPath(CreateFile(Root, relativeFilePath, content, Encoding.UTF8));
                Paths.Add(fullFilePath);
                cache.Add(new SensorCacheEntry
                          {
                              Key = relativeFilePath,
                              Data = ByteString.CopyFrom(Sut.ContentHash(fullFilePath))
                          });
            }
            Sut.PullRequestCacheBasePath.Should().Be(Root, "Cache files must exist on expected path.");
        }

        public void ProcessPullRequest() =>
            Sut.ProcessPullRequest(cache);

        public void Dispose() =>
            Sut.Dispose();
    }
}
