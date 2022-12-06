/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.PreProcessor.Protobuf;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class CacheProcessorTests
    {
        private TestLogger logger;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize() =>
            logger = new();

        [TestMethod]
        public void Constructor_NullArguments_Throws()
        {
            var server = Mock.Of<ISonarWebService>();
            var locals = CreateProcessedArgs();
            var builds = Mock.Of<IBuildSettings>();
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
            File.ReadAllBytes(emptyNoBom).Should().HaveCount(0, "ASCII encoding should not generate BOM");

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
            var localSettings = ArgumentProcessor.TryProcessArgs(new[] { "/k:key", "/d:sonar.projectBaseDir=Custom" }, logger);
            var buildSettings = Mock.Of<IBuildSettings>(x => x.SourcesDirectory == @"C:\Sources\Directory" && x.SonarScannerWorkingDirectory == @"C:\SonarScanner\WorkingDirectory");
            using var sut = new CacheProcessor(Mock.Of<ISonarWebService>(), localSettings, buildSettings, logger);

            sut.PullRequestCacheBasePath.Should().Be(Path.Combine(workingDirectory, "Custom"));
        }

        [TestMethod]
        public void PullRequestCacheBasePath_NoProjectBaseDir_UsesSourcesDirectory()
        {
            var buildSettings = Mock.Of<IBuildSettings>(x => x.SourcesDirectory == @"C:\Sources\Directory" && x.SonarScannerWorkingDirectory == @"C:\SonarScanner\WorkingDirectory");
            using var sut = CreateSut(buildSettings);

            sut.PullRequestCacheBasePath.Should().Be(@"C:\Sources\Directory");
        }

        [TestMethod]
        public void PullRequestCacheBasePath_NoSourcesDirectory_UsesSonarScannerWorkingDirectory()
        {
            var buildSettings = Mock.Of<IBuildSettings>(x => x.SonarScannerWorkingDirectory == @"C:\SonarScanner\WorkingDirectory");
            using var sut = CreateSut(buildSettings);

            sut.PullRequestCacheBasePath.Should().Be(@"C:\SonarScanner\WorkingDirectory");
        }

        [TestMethod]
        public void PullRequestCacheBasePath_NoSonarScannerWorkingDirectory_IsNull()
        {
            using var sut = CreateSut();

            sut.PullRequestCacheBasePath.Should().Be(null);
        }

        [DataTestMethod]
        [DataRow("", null)]
        [DataRow(null, "")]
        public void PullRequestCacheBasePath_EmptyDirectories_IsNull(string sourcesDirectory, string sonarScannerWorkingDirectory)
        {
            var buildSettings = Mock.Of<IBuildSettings>(x => x.SourcesDirectory == sourcesDirectory && x.SonarScannerWorkingDirectory == sonarScannerWorkingDirectory);
            using var sut = CreateSut(buildSettings);

            sut.PullRequestCacheBasePath.Should().Be(null);
        }

        [TestMethod]
        public async Task Execute_MainBranch()
        {
            using var sut = CreateSut();
            await sut.Execute();

            logger.AssertDebugLogged("Base branch parameter was not provided. Incremental PR analysis is disabled.");
            sut.UnchangedFilesPath.Should().BeNull();
        }

        [TestMethod]
        public async Task Execute_PullRequest_NoBasePath()
        {
            using var sut = new CacheProcessor(Mock.Of<ISonarWebService>(), CreateProcessedArgs("/k:key /d:sonar.pullrequest.base=master"), Mock.Of<IBuildSettings>(), logger);
            await sut.Execute();

            logger.AssertWarningLogged("Cannot determine project base path. Incremental PR analysis is disabled.");
            sut.UnchangedFilesPath.Should().BeNull();
        }

        [TestMethod]
        public async Task Execute_PullRequest_NoCache()
        {
            var settings = Mock.Of<IBuildSettings>(x => x.SourcesDirectory == @"C:\Sources");
            using var sut = new CacheProcessor(Mock.Of<ISonarWebService>(), CreateProcessedArgs("/k:key /d:sonar.pullrequest.base=TARGET_BRANCH"), settings, logger);
            await sut.Execute();

            logger.AssertInfoLogged("Processing pull request with base branch 'TARGET_BRANCH'.");
            sut.UnchangedFilesPath.Should().BeNull();
        }

        [TestMethod]
        public async Task Execute_PullRequest_FullProcessing_NoCacheAvailable()
        {
            var context = new CacheContext(this, "/k:key-no-cache /d:sonar.pullrequest.base=TARGET_BRANCH");
            await context.Sut.Execute();

            logger.AssertInfoLogged("Processing pull request with base branch 'TARGET_BRANCH'.");
            logger.AssertInfoLogged("Cache data is not available. Incremental PR analysis is disabled.");
            context.Sut.UnchangedFilesPath.Should().BeNull();
        }

        [TestMethod]
        public async Task Execute_PullRequest_FullProcessing_WithCache()
        {
            var context = new CacheContext(this, "/k:key /d:sonar.pullrequest.base=TARGET_BRANCH");
            await context.Sut.Execute();

            logger.AssertInfoLogged("Processing pull request with base branch 'TARGET_BRANCH'.");
            context.Sut.UnchangedFilesPath.Should().EndWith("UnchangedFiles.txt");
        }

        [TestMethod]
        public void ProcessPullRequest_EmptyCache_DoesNotProduceOutput()
        {
            using var sut = CreateSut();
            var cache = new AnalysisCacheMsg();
            sut.ProcessPullRequest(cache);

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
            File.Delete(context.Paths.First());       // This file was in cache, but doesn't exist anymore
            File.WriteAllText(context.Paths.Last(), " // This file was modified");
            context.ProcessPullRequest();

            context.Sut.UnchangedFilesPath.Should().EndWith("UnchangedFiles.txt");
            File.ReadAllLines(context.Sut.UnchangedFilesPath).Should().BeEquivalentTo(context.Paths[1]);  // Only a single file was not modified
            logger.AssertInfoLogged("Incremental PR analysis: 1 files out of 3 are unchanged.");
        }

        [TestMethod]
        public void ProcessPullRequest_UnexpectedCacheKeys()
        {
            using var sut = CreateSut(Mock.Of<IBuildSettings>(x => x.SonarScannerWorkingDirectory == @"C:\ValidBasePath"));
            var cache = new AnalysisCacheMsg
            {
                Map =
                {
                    { new(Path.GetInvalidFileNameChars()), ByteString.Empty},
                    {new(Path.GetInvalidPathChars()), ByteString.Empty},
                    {string.Empty, ByteString.Empty},
                    {"  ", ByteString.Empty},
                    {"\t", ByteString.Empty},
                    {"\n", ByteString.Empty},
                    { "\r", ByteString.Empty }
                }
            };
            sut.ProcessPullRequest(cache);

            sut.UnchangedFilesPath.Should().BeNull();
            logger.AssertInfoLogged("Incremental PR analysis: 0 files out of 7 are unchanged.");
        }

        private CacheProcessor CreateSut(IBuildSettings buildSettings = null) =>
            new(Mock.Of<ISonarWebService>(), CreateProcessedArgs(), buildSettings ?? Mock.Of<IBuildSettings>(), logger);

        private ProcessedArgs CreateProcessedArgs(string commandLineArgs = "/k:key") =>
            CreateProcessedArgs(logger, commandLineArgs);

        private static ProcessedArgs CreateProcessedArgs(ILogger logger, string commandLineArgs = "/k:key")
        {
            using var scope = new EnvironmentVariableScope();
            scope.SetVariable("SONARQUBE_SCANNER_PARAMS", null);    // When CI is run for a PR, AzureDevOps extension sets this to the actual PR analysis of S4NET project.
            var processedArgs = ArgumentProcessor.TryProcessArgs(commandLineArgs.Split(' '), logger);
            processedArgs.Should().NotBeNull();
            return processedArgs;
        }

        private static string Serialize(byte[] value) =>
            string.Concat(value.Select(x => x.ToString("x2")));

        private static string CreateFile(string root, string fileName, string content, Encoding encoding)
        {
            var path = Path.Combine(root, fileName);
            File.WriteAllText(path, content, encoding);
            return path;
        }

        private sealed class CacheContext : IDisposable
        {
            public readonly string Root;
            public readonly List<string> Paths = new();
            public readonly CacheProcessor Sut;
            public readonly AnalysisCacheMsg Cache = new();

            public CacheContext(CacheProcessorTests owner, string commandLineArgs = "/k:key")
            {
                Root = TestUtils.CreateTestSpecificFolderWithSubPaths(owner.TestContext);
                var factory = new MockObjectFactory(owner.logger);
                var settings = Mock.Of<IBuildSettings>(x => x.SourcesDirectory == Root && x.SonarConfigDirectory == Root);
                factory.Server.Cache = Cache;
                Sut = new CacheProcessor(factory.Server, CreateProcessedArgs(factory.Logger, commandLineArgs), settings, factory.Logger);
                Paths.Add(CreateFile(Root, "File1.cs", "// Hello", Encoding.UTF8));
                Paths.Add(CreateFile(Root, "File2.cs", "// Hello World", Encoding.UTF8));
                Paths.Add(CreateFile(Root, "File3.vb", "' Hello World!", Encoding.UTF8));
                foreach (var path in Paths)
                {
                    Cache.Map.Add(Path.GetFileName(path), ByteString.CopyFrom(Sut.ContentHash(path)));
                }
                Sut.PullRequestCacheBasePath.Should().Be(Root, "Cache files must exist on expected path.");
            }

            public void ProcessPullRequest() =>
                Sut.ProcessPullRequest(Cache);

            public void Dispose() =>
                Sut.Dispose();
        }
    }
}
