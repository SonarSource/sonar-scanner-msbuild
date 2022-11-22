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
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
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
        private readonly ILogger logger = Mock.Of<ILogger>();

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Constructor_NullArguments_Throws()
        {
            var server = Mock.Of<ISonarQubeServer>();
            var locals = CreateProcessedArgs();
            var builds = Mock.Of<IBuildSettings>();
            ;
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
            using var sut = new CacheProcessor(Mock.Of<ISonarQubeServer>(), localSettings, buildSettings, logger);

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
            using var sut = new CacheProcessor(Mock.Of<ISonarQubeServer>(), CreateProcessedArgs(), buildSettings, logger);

            sut.PullRequestCacheBasePath.Should().Be(null);
        }

        [TestMethod]
        public void Execute_MainBranch()
        {
            Assert.Inconclusive(); // FIXME: Implement
        }

        [TestMethod]
        public void Execute_PullRequest()
        {
            Assert.Inconclusive(); // FIXME: Implement
        }

        [TestMethod]
        public void ProcessPullRequest_EmptyCache_DoesNotProduceOutput()
        {
            using var sut = new CacheProcessor(Mock.Of<ISonarQubeServer>(), CreateProcessedArgs(), Mock.Of<IBuildSettings>(), Mock.Of<ILogger>());
            var cache = new AnalysisCacheMsg();
            sut.ProcessPullRequest(cache);

            sut.UnchangedFilesPath.Should().BeNull();
        }

        [TestMethod]
        public void ProcessPullRequest_AllFilesChanged_DoesNotProduceOutput()
        {
            var factory = new MockObjectFactory();
            using var sut = new CacheProcessor(factory.Server, CreateProcessedArgs(), Mock.Of<IBuildSettings>(), factory.Logger);
            var cache = new AnalysisCacheMsg();
            sut.ProcessPullRequest(cache);

            sut.UnchangedFilesPath.Should().BeNull();

        }

        [TestMethod]
        public void ProcessPullRequest_SomeFilesChanged_ProducesOnlyUnchangedFiles()
        {
            Assert.Inconclusive();
            // FIXME: Unchanged
            // FIXME: Added
            // FIXME: Deleted
            // FIXME: Modified
            // FIXME: Change BOM
        }

        private static string Serialize(byte[] value) =>
            string.Concat(value.Select(x => x.ToString("x2")));

        private CacheProcessor CreateSut(IBuildSettings buildSettings = null) =>
            new(Mock.Of<ISonarQubeServer>(), CreateProcessedArgs(), buildSettings ?? Mock.Of<IBuildSettings>(), logger);

        private static string CreateFile(string root, string fileName, string content, Encoding encoding)
        {
            var path = Path.Combine(root, fileName);
            File.WriteAllText(path, content, encoding);
            return path;
        }

        private static string Serialize(byte[] value) =>
            string.Concat(value.Select(x => x.ToString("x2")));

        private ProcessedArgs CreateProcessedArgs()
        {
            var processedArgs = ArgumentProcessor.TryProcessArgs(new[] {"/k:key"}, logger);
            processedArgs.Should().NotBeNull();
            return processedArgs;
        }
    }
}
