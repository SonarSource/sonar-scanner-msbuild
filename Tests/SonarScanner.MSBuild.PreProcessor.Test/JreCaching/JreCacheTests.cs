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
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test.JreCaching;

[TestClass]
public class JreCacheTests
{
    private readonly TestLogger testLogger = new TestLogger();

    // https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.createdirectory
    // https://learn.microsoft.com/en-us/dotnet/api/system.io.file.create
    // https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move
    private static IEnumerable<object[]> DirectoryAndFileCreateAndMoveExceptions
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
        var checksum = Substitute.For<IChecksum>();
        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = sut.IsJreCached(home, new("jre", "sha", "java"));
        result.Should().Be(new JreCacheMiss());
        directoryWrapper.Received().CreateDirectory(cache);
    }

    [DataTestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public void CacheHomeCreationFails(Type exceptionType)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.When(x => x.CreateDirectory(cache)).Throw((Exception)Activator.CreateInstance(exceptionType));
        var fileWrapper = Substitute.For<IFileWrapper>();
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
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
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
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
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
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
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = sut.IsJreCached(home, new("filename.tar.gz", "sha", "jdk/bin/java"));
        result.Should().Be(new JreCacheHit(expectedExtractedJavaExe));
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_CacheDirectoryCanNotBeCreated(Type exception)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(false);
        directoryWrapper.When(x => x.CreateDirectory(cache)).Throw((Exception)Activator.CreateInstance(exception));
        var fileWrapper = Substitute.For<IFileWrapper>();
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be(@"The JRE cache directory in 'C:\Users\user\.sonar\cache\sha256' could not be created.");
    }

    [DataTestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_ShaDirectoryCanNotBeCreated(Type exception)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.When(x => x.CreateDirectory(sha)).Throw((Exception)Activator.CreateInstance(exception));
        var fileWrapper = Substitute.For<IFileWrapper>();
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
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
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(true);
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");
        testLogger.AssertDebugLogged(@"The Java Runtime Environment was already downloaded from the server and stored at 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz'.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Success()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        var downloadContentArray = new byte[] { 1, 2, 3 };
        var fileContentArray = new byte[3];
        var fileContentStream = new MemoryStream(fileContentArray, writable: true);
        string tempFileName = null;
        fileWrapper.Create(Arg.Do<string>(x => tempFileName = x)).Returns(fileContentStream);
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream(downloadContentArray)));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");
        fileWrapper.Received().Create(tempFileName);
        fileWrapper.Received().Move(tempFileName, $@"{sha}\filename.tar.gz");
        fileContentArray.Should().BeEquivalentTo(downloadContentArray);
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after success.");
        testLogger.AssertDebugLogged(@"Starting the Java Runtime Environment download.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Success_WithTestFiles()
    {
        var home = Path.GetTempPath();
        var sha = Path.GetRandomFileName();
        var cache = $@"{home}\cache";
        var jre = $@"{cache}\{sha}";
        var file = $@"{jre}\filename.tar.gz";
        var directoryWrapper = DirectoryWrapper.Instance; // Do real I/O operations in this test and only fake the download.
        var fileWrapper = FileWrapper.Instance;
        var downloadContentArray = new byte[] { 1, 2, 3 };
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        try
        {
            var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", sha, "javaPath"), () => Task.FromResult<Stream>(new MemoryStream(downloadContentArray)));
            result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");
            File.Exists(file).Should().BeTrue();
            File.ReadAllBytes(file).Should().BeEquivalentTo(downloadContentArray);
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(jre);
            try
            {
                Directory.Delete(cache);
            }
            catch
            {
                // This delete may fail for parallel tests.
            }
        }
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Failure_WithTestFiles()
    {
        var home = Path.GetTempPath();
        var sha = Path.GetRandomFileName();
        var cache = $@"{home}\cache";
        var jre = $@"{cache}\{sha}";
        var file = $@"{jre}\filename.tar.gz";
        var directoryWrapper = DirectoryWrapper.Instance; // Do real I/O operations in this test and only fake the download.
        var fileWrapper = FileWrapper.Instance;
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        try
        {
            var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", sha, "javaPath"), () => throw new InvalidOperationException("Download failure simulation."));
            result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be(
                @"The download of the Java runtime environment from the server failed with the exception 'Download failure simulation.'.");
            File.Exists(file).Should().BeFalse();
            Directory.GetFiles(jre).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(jre);
            try
            {
                Directory.Delete(cache);
            }
            catch
            {
                // This delete may fail for parallel tests.
            }
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_DownloadFileNew_Failure_FileCreate(Type exceptionType)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        string tempFileName = null;
        var exception = (Exception)Activator.CreateInstance(exceptionType);
        fileWrapper.Create(Arg.Do<string>(x => tempFileName = x)).Throws(exception);
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be(
            $"The download of the Java runtime environment from the server failed with the exception '{exception.Message}'.");
        fileWrapper.Received().Create(tempFileName);
        fileWrapper.DidNotReceive().Move(tempFileName, $@"{sha}\filename.tar.gz");
        fileWrapper.DidNotReceive().Delete(tempFileName);
    }

    [DataTestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_DownloadFileNew_Failure_TempFileDeleteOnFailure(Type exceptionType)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        string tempFileName = null;
        fileWrapper.Create(Arg.Do<string>(x => tempFileName = x)).Returns(new MemoryStream());
        fileWrapper.When(x => x.Delete(Arg.Is<string>(x => x == tempFileName))).Do(x => throw ((Exception)Activator.CreateInstance(exceptionType)));
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new InvalidOperationException("Download failure simulation."));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the " +
            "exception 'Download failure simulation.'."); // The exception from the failed temp file delete is not visible to the user.
        fileWrapper.Received().Create(tempFileName);
        fileWrapper.Received().Delete(tempFileName);
        fileWrapper.DidNotReceive().Move(tempFileName, $@"{sha}\filename.tar.gz");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Failure_TempFileStreamCloseOnFailure()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        var fileStream = Substitute.For<Stream>();
        fileStream.When(x => x.Close()).Throw(x => new ObjectDisposedException("stream"));
        string tempFileName = null;
        fileWrapper.Create(Arg.Do<string>(x => tempFileName = x)).Returns(fileStream);
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new InvalidOperationException("Download failure simulation."));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the exception " +
            "'Cannot access a disposed object.\r\nObject name: 'stream'.'."); // This should actually read "Download failure simulation." because the ObjectDisposedException is actually swallowed.
                                                                              // I assume this is either
                                                                              // * a bug in NSubstitute, or
                                                                              // * because of the way async stacks are handled (I tested with a dedicated project but it didn't reproduced there), or
                                                                              // * maybe something like this: https://github.com/dotnet/roslyn/issues/72177
                                                                              // This is such an corner case, that the misleading message isn't really a problem.
        fileWrapper.Received().Create(tempFileName);
        fileWrapper.Received().Delete(tempFileName);
        fileWrapper.DidNotReceive().Move(tempFileName, $@"{sha}\filename.tar.gz");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Failure_Download()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        var fileContentStream = new MemoryStream();
        string tempFileName = null;
        fileWrapper.Create(Arg.Do<string>(x => tempFileName = x)).Returns(fileContentStream);
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new InvalidOperationException("Download failure simulation."));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the exception 'Download failure simulation.'.");
        fileWrapper.Received().Create(tempFileName);
        fileWrapper.Received().Delete(tempFileName);
        fileWrapper.DidNotReceive().Move(tempFileName, $@"{sha}\filename.tar.gz");
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after failure.");
    }

    [DataTestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_DownloadFileNew_Failure_Move(Type exceptionType)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        var file = $@"{sha}\filename.tar.gz";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(file).Returns(false);
        var fileContentArray = new byte[3];
        var fileContentStream = new MemoryStream(fileContentArray);
        string tempFileName = null;
        fileWrapper.Create(Arg.Do<string>(x => tempFileName = x)).Returns(fileContentStream);
        var exception = (Exception)Activator.CreateInstance(exceptionType);
        fileWrapper.When(x => x.Move(Arg.Is<string>(x => x == tempFileName), file)).Throw(exception);
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be($"The download of the Java runtime environment from the server failed with the exception '{exception.Message}'.");
        fileWrapper.Received().Create(tempFileName);
        fileWrapper.Received().Move(tempFileName, file);
        fileWrapper.Received().Delete(tempFileName);
        fileContentArray.Should().BeEquivalentTo([1, 2, 3]);
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after failure.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_DownloadFailed_But_FileExists()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        var file = $@"{sha}\filename.tar.gz";
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        // Before the download, the fileWrapper.Exists returns false.
        // Then the download fails because e.g. another scanner created the file.
        // The second call to fileWrapper.Exists returns true and therefore and we want to continue with provisioning.
        fileWrapper.Exists(file).Returns(false, true);
        fileWrapper.When(x => x.Create(Arg.Any<string>())).Throw<IOException>(); // Fail the download somehow.
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        // The download failed, but we still progress with the provisioning because somehow magically the file is there anyway.
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");
        testLogger.AssertDebugLogged(@"Starting the Java Runtime Environment download.");
        testLogger.AssertDebugLogged(@"The download of the Java runtime environment from the server failed with the exception 'I/O error occurred.'.");
        testLogger.AssertDebugLogged(@"The Java Runtime Environment archive was found after the download failed. Another scanner did the download in the parallel.");
    }

    [DataTestMethod]
    [DataRow("sha256", "sha256")]
    [DataRow("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [DataRow("b5dffd0be08c464d9c3903e2947508c1a5c21804ea1cff5556991a2a47d617d8", "B5DFFD0BE08C464D9C3903E2947508C1A5C21804EA1CFF5556991A2A47D617D8")]
    public async Task Checksum_DownloadFilesChecksumFitsExpectation(string fileHashValue, string expectedHashValue)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, expectedHashValue);
        var file = Path.Combine(sha, "filename.tar.gz");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream()); // This is the temp file creation.
        var fileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(fileStream);
        var checksum = Substitute.For<IChecksum>();
        checksum.ComputeHash(fileStream).Returns(fileHashValue);

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", expectedHashValue, "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("NotImplemented. The JRE is downloaded and validated, but we still need to unpack, and set permissions.");
        testLogger.AssertDebugLogged("Starting the Java Runtime Environment download.");
        testLogger.AssertDebugLogged($"The checksum of the downloaded file is '{fileHashValue}' and the expected checksum is '{expectedHashValue}'.");
        fileWrapper.Received(1).Exists(file);
        fileWrapper.Received(1).Create(Arg.Any<string>());
        fileWrapper.Received(1).Open(file);
        checksum.Received(1).ComputeHash(fileStream);
    }

    [DataTestMethod]
    [DataRow("fileHash", "expectedHash")]
    [DataRow("e3b0c ", "e3b0c")]
    [DataRow("e3b0c", "e3b0c ")]
    [DataRow("e3b0c", "")]
    [DataRow("", "e3b0c")]
    public async Task Checksum_DownloadFilesChecksumDoesNotMatchExpectation(string fileHashValue, string expectedHashValue)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, expectedHashValue);
        var file = Path.Combine(sha, "filename.tar.gz");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var fileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(fileStream);
        var checksum = Substitute.For<IChecksum>();
        checksum.ComputeHash(fileStream).Returns(fileHashValue);

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", expectedHashValue, "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");
        testLogger.AssertDebugLogged($"The checksum of the downloaded file is '{fileHashValue}' and the expected checksum is '{expectedHashValue}'.");
        fileWrapper.Received(1).Exists(file);
        fileWrapper.Received(1).Create(Arg.Any<string>());
        fileWrapper.Received(1).Open(file);
        checksum.Received(1).ComputeHash(fileStream);
    }

    [TestMethod]
    public async Task Checksum_DownloadFile_ComputationFails()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, "filename.tar.gz");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var fileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(fileStream);
        var checksum = Substitute.For<IChecksum>();
        checksum.ComputeHash(fileStream).Throws<InvalidOperationException>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");
        testLogger.AssertDebugLogged(@"The calculation of the checksum of the file 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz' failed with message " +
            "'Operation is not valid due to the current state of the object.'.");
        fileWrapper.Received(1).Exists(file);
        fileWrapper.Received(1).Create(Arg.Any<string>());
        fileWrapper.Received(1).Open(file);
        checksum.Received(1).ComputeHash(fileStream);
    }

    [TestMethod]
    public async Task Checksum_DownloadFile_FileOpenFails()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, "filename.tar.gz");
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        fileWrapper.Open(file).Throws<IOException>();
        var checksum = Substitute.For<IChecksum>();

        var sut = new JreCache(testLogger, directoryWrapper, fileWrapper, checksum);
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");
        testLogger.AssertDebugLogged(@"The calculation of the checksum of the file 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz' failed with message " +
            "'I/O error occurred.'.");
        fileWrapper.Received(1).Exists(file);
        fileWrapper.Received(1).Create(Arg.Any<string>());
        fileWrapper.Received(1).Open(file);
        checksum.DidNotReceive().ComputeHash(Arg.Any<Stream>());
    }
}
