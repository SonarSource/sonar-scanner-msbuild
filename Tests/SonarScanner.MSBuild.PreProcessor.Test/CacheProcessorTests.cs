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
using System.Text;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class CacheProcessorTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Constructor_NullArguments_Throws()
        {
            var server = Mock.Of<ISonarQubeServer>();
            var settings = CreateProcessedArgs();
            var logger = Mock.Of<ILogger>();
            ((Func<CacheProcessor>)(() => new CacheProcessor(server, settings, logger))).Should().NotThrow();
            ((Func<CacheProcessor>)(() => new CacheProcessor(null, settings, logger))).Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("server");
            ((Func<CacheProcessor>)(() => new CacheProcessor(server, null, logger))).Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("settings");
            ((Func<CacheProcessor>)(() => new CacheProcessor(server, settings, null))).Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void ContentHash_ComputeUniqueHash()
        {
            const string allNewLines = "public class Sample\n{\n\r\tint field;\n\r}\r";
            const string diacritics = "ěščřžýáí";
            var sut = new CacheProcessor(new Mock<ISonarQubeServer>().Object, CreateProcessedArgs(), new Mock<ILogger>().Object);
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

            sut.ContentHash(emptyWithBom).Should().Be("57218c316b6921e2cd61027a2387edc31a2d9471");
            sut.ContentHash(emptyNoBom).Should().Be("da39a3ee5e6b4b0d3255bfef95601890afd80709");
            sut.ContentHash(codeWithBom).Should().Be("2d6384153e0b4eea0e3d9fc50dcf5dfb5f41e5da");
            sut.ContentHash(codeNoBom).Should().Be("870f9b1e73d47d817957a1d13d3ea71add4c8a91");
            sut.ContentHash(utf8).Should().Be("0611f145cc23f580a749be262a3598511787dcae");
            sut.ContentHash(utf16).Should().Be("5e27d7cde2a1aeaca931297c23ab051394457248");
            sut.ContentHash(ansi).Should().Be("b705e5ea2fd011094db5f565a5133912aa97c1e6");
        }

        [TestMethod]
        public void ContentHash_IsDeterministic()
        {
            var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var path = CreateFile(root, "File.txt", "Lorem ipsum", Encoding.UTF8);
            var sut = new CacheProcessor(new Mock<ISonarQubeServer>().Object, CreateProcessedArgs(), new Mock<ILogger>().Object);
            var hash1 = sut.ContentHash(path);
            var hash2 = sut.ContentHash(path);
            var hash3 = sut.ContentHash(path);
            hash2.Should().Be(hash1);
            hash3.Should().Be(hash1);
        }

        private static string CreateFile(string root, string fileName, string content, Encoding encoding)
        {
            var path = Path.Combine(root, fileName);
            File.WriteAllText(path, content, encoding);
            return path;
        }

        private static ProcessedArgs CreateProcessedArgs()
        {
            var processedArgs = ArgumentProcessor.TryProcessArgs(new[] {"/k:key"}, Mock.Of<ILogger>());
            processedArgs.Should().NotBeNull();
            return processedArgs;
        }
    }
}
