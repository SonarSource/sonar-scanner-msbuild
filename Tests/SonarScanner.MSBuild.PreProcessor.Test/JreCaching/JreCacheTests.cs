/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.JreCaching;

namespace SonarScanner.MSBuild.PreProcessor.Test.JreCaching;

[TestClass]
public class JreCacheTests
{
    private static IEnumerable<object[]> DirectoryCreateExceptions
    {
        get
        {
            yield return [typeof(IOException)];
            yield return [typeof(UnauthorizedAccessException)];
            yield return [typeof(ArgumentException)];
            yield return [typeof(ArgumentNullException)];
            yield return [typeof(PathTooLongException)];
            yield return [typeof(DirectoryNotFoundException)];
            yield return [typeof(NotSupportedException)];
        }
    }

    [TestMethod]
    public void CacheDirectoryIsCreated()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(false);
        var fileWrapper = Substitute.For<IFileWrapper>();

        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = sut.IsJreCached(home, new("jre", "sha", "java"));
        result.Should().Be(new JreCacheMiss());
        directoryWrapper.Received().CreateDirectory(cache);
    }

    [DataTestMethod]
    [DynamicData(nameof(DirectoryCreateExceptions))]
    public void CacheHomeCreationFails(Type exceptionType)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.When(x => x.CreateDirectory(cache)).Throw((Exception)Activator.CreateInstance(exceptionType));
        var fileWrapper = Substitute.For<IFileWrapper>();

        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = sut.IsJreCached(home, new("jre", "sha", "java"));
        result.Should().Be(new JreCacheFailure(@"The JRE cache directory in 'C:\Users\user\.sonar\cache' could not be created."));
        directoryWrapper.Received().Exists(cache);
        directoryWrapper.Received().CreateDirectory(cache);
    }

    [TestMethod]
    public void ExtractedDirectoryDoesNotExists()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var expectedExtractedPath = Path.Combine(cache, "sha", "filename.tar.gz_extracted");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(expectedExtractedPath).Returns(false);
        var fileWrapper = Substitute.For<IFileWrapper>();

        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = sut.IsJreCached(home, new("filename.tar.gz", "sha", "jdk/bin/java"));
        result.Should().Be(new JreCacheMiss());
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public void JavaExecutableDoesNotExists()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var expectedExtractedPath = Path.Combine(cache, "sha", "filename.tar.gz_extracted");
        var expectedExtractedJavaExe = Path.Combine(expectedExtractedPath, "jdk/bin/java");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(expectedExtractedPath).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(expectedExtractedJavaExe).Returns(false);

        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = sut.IsJreCached(home, new("filename.tar.gz", "sha", "jdk/bin/java"));
        result.Should().Be(new JreCacheFailure(
            @"The java executable in the JRE cache could not be found at the expected location 'C:\Users\user\.sonar\cache\sha\filename.tar.gz_extracted\jdk/bin/java'."));
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public void CacheHit()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var expectedExtractedPath = Path.Combine(cache, "sha", "filename.tar.gz_extracted");
        var expectedExtractedJavaExe = Path.Combine(expectedExtractedPath, "jdk/bin/java");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(expectedExtractedPath).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(expectedExtractedJavaExe).Returns(true);

        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = sut.IsJreCached(home, new("filename.tar.gz", "sha", "jdk/bin/java"));
        result.Should().Be(new JreCacheHit(expectedExtractedJavaExe));
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public async Task Download_UserHomeDirectoryCanNotBeCreated()
    {
        var home = @"C:\Users\user\.sonar";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(false);
        directoryWrapper.When(x => x.CreateDirectory(home)).Throw<UnauthorizedAccessException>();
        var fileWrapper = Substitute.For<IFileWrapper>();
        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be(@"The JRE cache directory in 'C:\Users\user\.sonar\cache\sha256' could not be created.");
    }

    [TestMethod]
    public async Task Download_CacheDirectoryCanNotBeCreated()
    {
        var home = @"C:\Users\user\.sonar";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(true);
        directoryWrapper.When(x => x.CreateDirectory($@"{home}\cache")).Throw<UnauthorizedAccessException>();
        var fileWrapper = Substitute.For<IFileWrapper>();
        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be(@"The JRE cache directory in 'C:\Users\user\.sonar\cache\sha256' could not be created.");
    }

    [TestMethod]
    public async Task Download_ShaDirectoryCanNotBeCreated()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(true);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.When(x => x.CreateDirectory(sha)).Throw<UnauthorizedAccessException>();
        var fileWrapper = Substitute.For<IFileWrapper>();
        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be(@"The JRE cache directory in 'C:\Users\user\.sonar\cache\sha256' could not be created.");
    }

    [TestMethod]
    public async Task Download_DownloadFileExists()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(true);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(true);
        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be(@"NotImplemented. The JRE is downloaded, but we still need to check, unpack, and set permissions.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Success()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(true);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        var downloadContentArray = new byte[] { 1, 2, 3 };
        var fileContentArray = new byte[3];
        var fileContentStream = new MemoryStream(fileContentArray, writable: true);
        fileWrapper.Create($@"{sha}\filename.tar.gz").Returns(fileContentStream);
        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream(downloadContentArray)));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be(@"NotImplemented. The JRE is downloaded, but we still need to check, unpack, and set permissions.");
        fileWrapper.Received().Create($@"{sha}\filename.tar.gz");
        fileContentArray.Should().BeEquivalentTo(downloadContentArray);
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after success.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Failure_FileCreate()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(true);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        fileWrapper.Create($@"{sha}\filename.tar.gz").Throws<UnauthorizedAccessException>();
        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be(
            "The download of the Java runtime environment from the server failed with the exception 'Attempted to perform an unauthorized operation.'.");
        fileWrapper.Received().Create($@"{sha}\filename.tar.gz");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Failure_Download()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(home).Returns(true);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        var fileContentStream = new MemoryStream();
        fileWrapper.Create($@"{sha}\filename.tar.gz").Returns(fileContentStream);
        var sut = new JreCache(directoryWrapper, fileWrapper);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new InvalidOperationException("Download failure simulation."));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the exception 'Download failure simulation.'.");
        fileWrapper.Received().Create($@"{sha}\filename.tar.gz");
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after failure.");
    }
}
