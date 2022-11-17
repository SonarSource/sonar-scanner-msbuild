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
            using var sut = new CacheProcessor(new Mock<ISonarQubeServer>().Object, CreateProcessedArgs(), new Mock<ILogger>().Object);
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

            sut.ContentHash(emptyWithBom).Should().Be("f1945cd6c19e56b3c1c78943ef5ec18116907a4ca1efc40a57d48ab1db7adfc5");
            sut.ContentHash(emptyNoBom).Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
            sut.ContentHash(codeWithBom).Should().Be("b98aaf2ce5a3f9cdf8ab785563951f2309d577baa6351098f78908300fdc610a");
            sut.ContentHash(codeNoBom).Should().Be("8c7535a8e3679bf8cc241b5749cef5fc38243401556f2b7869495c7b48ee4980");
            sut.ContentHash(utf8).Should().Be("13aa54e315a806270810f3a91501f980a095a2ef1bcc53167d4c750a1b78684d");
            sut.ContentHash(utf16).Should().Be("a9b3c4402770855d090ba4b49adeb5ad601cb3bbd6de18495302f45f242ef932");
            sut.ContentHash(ansi).Should().Be("b965073262109da4f106cd90a5eeea025e2441c244af272537afa2cfb03c3ab8");
        }

        [TestMethod]
        public void ContentHash_IsDeterministic()
        {
            using var sut = new CacheProcessor(new Mock<ISonarQubeServer>().Object, CreateProcessedArgs(), new Mock<ILogger>().Object);
            var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var path = CreateFile(root, "File.txt", "Lorem ipsum", Encoding.UTF8);
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
