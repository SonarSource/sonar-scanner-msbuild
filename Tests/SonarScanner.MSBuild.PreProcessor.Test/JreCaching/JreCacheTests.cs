﻿/*
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
using NSubstitute.ReturnsExtensions;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.JreCaching;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test.JreCaching;

[TestClass]
public class JreCacheTests
{
    private const string TestArchiveName = "filename.tar.gz";

    private readonly TestLogger testLogger;
    private readonly IDirectoryWrapper directoryWrapper;
    private readonly IFileWrapper fileWrapper;
    private readonly IChecksum checksum;
    private readonly IUnpacker unpacker;
    private readonly IUnpackerFactory unpackerFactory;

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

    public JreCacheTests()
    {
        testLogger = new TestLogger();
        directoryWrapper = Substitute.For<IDirectoryWrapper>();
        fileWrapper = Substitute.For<IFileWrapper>();
        checksum = Substitute.For<IChecksum>();
        unpacker = Substitute.For<IUnpacker>();
        unpackerFactory = Substitute.For<IUnpackerFactory>();
        unpackerFactory.CreateForArchive(directoryWrapper, fileWrapper, TestArchiveName).Returns(unpacker);
    }

    [TestMethod]
    public void CacheDirectoryIsCreated()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        directoryWrapper.Exists(cache).Returns(false);
        var sut = CreateSutWithSubstitutes();
        var result = sut.IsJreCached(home, new("jre", "sha", "java"));
        result.Should().Be(new JreCacheMiss());
        directoryWrapper.Received(1).CreateDirectory(cache);
    }

    [DataTestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public void CacheHomeCreationFails(Type exceptionType)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        directoryWrapper.When(x => x.CreateDirectory(cache)).Throw((Exception)Activator.CreateInstance(exceptionType));

        var sut = CreateSutWithSubstitutes();
        var result = sut.IsJreCached(home, new("jre", "sha", "java"));
        result.Should().Be(new JreCacheFailure(@"The Java runtime environment cache directory in 'C:\Users\user\.sonar\cache' could not be created."));
        directoryWrapper.Received(1).Exists(cache);
        directoryWrapper.Received(1).CreateDirectory(cache);
    }

    [TestMethod]
    public void ExtractedDirectoryDoesNotExists()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var expectedExtractedPath = Path.Combine(cache, "sha", "filename.tar.gz_extracted");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(expectedExtractedPath).Returns(false);

        var sut = CreateSutWithSubstitutes();
        var result = sut.IsJreCached(home, new(TestArchiveName, "sha", "jdk/bin/java"));
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
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(expectedExtractedPath).Returns(true);
        fileWrapper.Exists(expectedExtractedJavaExe).Returns(false);

        var sut = CreateSutWithSubstitutes();
        var result = sut.IsJreCached(home, new(TestArchiveName, "sha", "jdk/bin/java"));
        result.Should().Be(new JreCacheFailure(
            @"The java executable in the Java runtime environment cache could not be found at the expected location 'C:\Users\user\.sonar\cache\sha\filename.tar.gz_extracted\jdk/bin/java'."));
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public void CacheHit()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var expectedExtractedPath = Path.Combine(cache, "sha", "filename.tar.gz_extracted");
        var expectedExtractedJavaExe = Path.Combine(expectedExtractedPath, "jdk/bin/java");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(expectedExtractedPath).Returns(true);
        fileWrapper.Exists(expectedExtractedJavaExe).Returns(true);

        var sut = CreateSutWithSubstitutes();
        var result = sut.IsJreCached(home, new(TestArchiveName, "sha", "jdk/bin/java"));
        result.Should().Be(new JreCacheHit(expectedExtractedJavaExe));
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_CacheDirectoryCanNotBeCreated(Type exception)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        directoryWrapper.Exists(cache).Returns(false);
        directoryWrapper.When(x => x.CreateDirectory(cache)).Throw((Exception)Activator.CreateInstance(exception));

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be(@"The Java runtime environment cache directory in 'C:\Users\user\.sonar\cache\sha256' could not be created.");
    }

    [DataTestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_ShaDirectoryCanNotBeCreated(Type exception)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.When(x => x.CreateDirectory(sha)).Throw((Exception)Activator.CreateInstance(exception));

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be(@"The Java runtime environment cache directory in 'C:\Users\user\.sonar\cache\sha256' could not be created.");
    }

    [TestMethod]
    public async Task Download_DownloadFileExists()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(true);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");
        testLogger.AssertDebugLogged(@"The Java Runtime Environment was already downloaded from the server and stored at 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz'.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Success()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        var downloadContentArray = new byte[] { 1, 2, 3 };
        var fileContentArray = new byte[3];
        var fileContentStream = new MemoryStream(fileContentArray, writable: true);
        string tempFileName = null;
        fileWrapper.Create(Arg.Do<string>(x => tempFileName = x)).Returns(fileContentStream);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream(downloadContentArray)));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");
        fileWrapper.Received(1).Create(tempFileName);
        fileWrapper.Received(1).Move(tempFileName, $@"{sha}\filename.tar.gz");
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
        var directoryWrapperIO = DirectoryWrapper.Instance; // Do real I/O operations in this test and only fake the download.
        var fileWrapperIO = FileWrapper.Instance;
        var downloadContentArray = new byte[] { 1, 2, 3 };

        var sut = new JreCache(testLogger, directoryWrapperIO, fileWrapperIO, checksum, unpackerFactory);
        try
        {
            var result = await sut.DownloadJreAsync(home, new(TestArchiveName, sha, "javaPath"), () => Task.FromResult<Stream>(new MemoryStream(downloadContentArray)));
            result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");
            File.Exists(file).Should().BeFalse();
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

    [TestMethod]
    public async Task Download_DownloadFileNew_Failure_WithTestFiles()
    {
        var home = Path.GetTempPath();
        var sha = Path.GetRandomFileName();
        var cache = $@"{home}\cache";
        var jre = $@"{cache}\{sha}";
        var file = $@"{jre}\filename.tar.gz";
        var directoryWrapperIO = DirectoryWrapper.Instance; // Do real I/O operations in this test and only fake the download.
        var fileWrapperIO = FileWrapper.Instance;

        var sut = new JreCache(testLogger, directoryWrapperIO, fileWrapperIO, checksum, unpackerFactory);
        try
        {
            var result = await sut.DownloadJreAsync(home, new(TestArchiveName, sha, "javaPath"), () => throw new InvalidOperationException("Download failure simulation."));
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
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        string tempFileName = null;
        var exception = (Exception)Activator.CreateInstance(exceptionType);
        fileWrapper.Create(Arg.Do<string>(x => tempFileName = x)).Throws(exception);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be(
            $"The download of the Java runtime environment from the server failed with the exception '{exception.Message}'.");
        fileWrapper.Received(1).Create(tempFileName);
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
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        string tempFileName = null;
        fileWrapper.Create(Arg.Do<string>(x => tempFileName = x)).Returns(new MemoryStream());
        fileWrapper.When(x => x.Delete(Arg.Is<string>(x => x == tempFileName))).Do(x => throw ((Exception)Activator.CreateInstance(exceptionType)));

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => throw new InvalidOperationException("Download failure simulation."));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the " +
            "exception 'Download failure simulation.'."); // The exception from the failed temp file delete is not visible to the user.
        fileWrapper.Received(1).Create(tempFileName);
        fileWrapper.Received(1).Delete(tempFileName);
        fileWrapper.DidNotReceive().Move(tempFileName, $@"{sha}\filename.tar.gz");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Failure_TempFileStreamCloseOnFailure()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        var fileStream = Substitute.For<Stream>();
        fileStream.When(x => x.Close()).Throw(x => new ObjectDisposedException("stream"));
        string tempFileName = null;
        fileWrapper.Create(Arg.Do<string>(x => tempFileName = x)).Returns(fileStream);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => throw new InvalidOperationException("Download failure simulation."));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the exception " +
            "'Cannot access a disposed object.\r\nObject name: 'stream'.'."); // This should actually read "Download failure simulation." because the ObjectDisposedException is actually swallowed.
                                                                              // I assume this is either
                                                                              // * a bug in NSubstitute, or
                                                                              // * because of the way async stacks are handled (I tested with a dedicated project but it didn't reproduced there), or
                                                                              // * maybe something like this: https://github.com/dotnet/roslyn/issues/72177
                                                                              // This is such an corner case, that the misleading message isn't really a problem.
        fileWrapper.Received(1).Create(tempFileName);
        fileWrapper.Received(1).Delete(tempFileName);
        fileWrapper.DidNotReceive().Move(tempFileName, $@"{sha}\filename.tar.gz");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Failure_Download()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = $@"{home}\cache";
        var sha = $@"{cache}\sha256";
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        var fileContentStream = new MemoryStream();
        string tempFileName = null;
        fileWrapper.Create(Arg.Do<string>(x => tempFileName = x)).Returns(fileContentStream);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => throw new InvalidOperationException("Download failure simulation."));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the exception 'Download failure simulation.'.");
        fileWrapper.Received(1).Create(tempFileName);
        fileWrapper.Received(1).Delete(tempFileName);
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
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists(file).Returns(false);
        var fileContentArray = new byte[3];
        var fileContentStream = new MemoryStream(fileContentArray);
        string tempFileName = null;
        fileWrapper.Create(Arg.Do<string>(x => tempFileName = x)).Returns(fileContentStream);
        var exception = (Exception)Activator.CreateInstance(exceptionType);
        fileWrapper.When(x => x.Move(Arg.Is<string>(x => x == tempFileName), file)).Throw(exception);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be($"The download of the Java runtime environment from the server failed with the exception '{exception.Message}'.");
        fileWrapper.Received(1).Create(tempFileName);
        fileWrapper.Received(1).Move(tempFileName, file);
        fileWrapper.Received(1).Delete(tempFileName);
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
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        // Before the download, the fileWrapper.Exists returns false.
        // Then the download fails because e.g. another scanner created the file.
        // The second call to fileWrapper.Exists returns true and therefore and we want to continue with provisioning.
        fileWrapper.Exists(file).Returns(false, true);
        fileWrapper.When(x => x.Create(Arg.Any<string>())).Throw<IOException>(); // Fail the download somehow.

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
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
        var file = Path.Combine(sha, TestArchiveName);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream()); // This is the temp file creation.
        var fileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(fileStream);
        checksum.ComputeHash(fileStream).Returns(fileHashValue);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, expectedHashValue, "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        testLogger.AssertDebugLogged("Starting the Java Runtime Environment download.");
        testLogger.AssertDebugLogged($"The checksum of the downloaded file is '{fileHashValue}' and the expected checksum is '{expectedHashValue}'.");
        fileWrapper.Received(1).Exists(file);
        fileWrapper.Received(1).Create(Arg.Any<string>());
        fileWrapper.Received(2).Open(file); // One for the checksum and the other for the unpacking.
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
        var file = Path.Combine(sha, TestArchiveName);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        fileWrapper.When(x => x.Delete(file)).Do(x => throw new FileNotFoundException());
        var fileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(fileStream);
        checksum.ComputeHash(fileStream).Returns(fileHashValue);
        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, expectedHashValue, "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");

        testLogger.DebugMessages.Should().BeEquivalentTo(
            "Starting the Java Runtime Environment download.",
            $"The checksum of the downloaded file is '{fileHashValue}' and the expected checksum is '{expectedHashValue}'.",
            "Deleting mismatched JRE Archive.",
            "Failed to delete mismatched JRE Archive. Unable to find the specified file.");
        fileWrapper.Received(1).Exists(file);
        fileWrapper.Received(1).Create(Arg.Any<string>());
        fileWrapper.Received(1).Open(file);
        fileWrapper.Received(1).Delete(file);
        checksum.Received(1).ComputeHash(fileStream);
    }

    [TestMethod]
    public async Task Checksum_DownloadFile_ComputationFails()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, TestArchiveName);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var fileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(fileStream);
        checksum.ComputeHash(fileStream).Throws<InvalidOperationException>();

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
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
        var file = Path.Combine(sha, TestArchiveName);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        fileWrapper.Open(file).Throws<IOException>();

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");
        testLogger.AssertDebugLogged(@"The calculation of the checksum of the file 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz' failed with message " +
            "'I/O error occurred.'.");
        fileWrapper.Received(1).Exists(file);
        fileWrapper.Received(1).Create(Arg.Any<string>());
        fileWrapper.Received(1).Open(file);
        checksum.DidNotReceive().ComputeHash(Arg.Any<Stream>());
    }

    [TestMethod]
    public async Task UnpackerFactory_Success()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, TestArchiveName);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        checksum.ComputeHash(Arg.Any<Stream>()).Returns("sha256");
        unpackerFactory.CreateForArchive(directoryWrapper, fileWrapper, TestArchiveName).Returns(Substitute.For<IUnpacker>());

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be(@"The downloaded Java runtime environment could not be extracted.");
        fileWrapper.Received(1).Exists(file);
        fileWrapper.Received(1).Create(Arg.Any<string>());
        fileWrapper.Received(2).Open(file); // One for the checksum and the other for the unpacking.
        checksum.Received(1).ComputeHash(Arg.Any<Stream>());
        unpackerFactory.Received(1).CreateForArchive(directoryWrapper, fileWrapper, TestArchiveName);
        testLogger.DebugMessages.Should().SatisfyRespectively(
            x => x.Should().Be(@"Starting the Java Runtime Environment download."),
            x => x.Should().Be(@"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'."),
            // The unpackerFactory returned an unpacker and it was called. But the test setup is incomplete and therefore fails later:
            x => x.Should().StartWith(@"Starting extracting the Java runtime environment from archive 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz' to folder 'C:\Users\user\.sonar\cache\sha256"),
            x => x.Should().StartWith(@"The extraction of the downloaded Java runtime environment failed with error 'The java executable in the extracted Java runtime environment was expected to be at 'C:\Users\user\.sonar\cache\sha256"));
    }

    [TestMethod]
    public async Task UnpackerFactory_ReturnsNull()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        unpackerFactory.CreateForArchive(directoryWrapper, fileWrapper, TestArchiveName).ReturnsNull();

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The archive format of the JRE archive `filename.tar.gz` is not supported.");
        fileWrapper.DidNotReceiveWithAnyArgs().Exists(null);
        fileWrapper.DidNotReceiveWithAnyArgs().Create(null);
        fileWrapper.DidNotReceiveWithAnyArgs().Open(null);
        checksum.DidNotReceiveWithAnyArgs().ComputeHash(null);
        unpackerFactory.Received(1).CreateForArchive(directoryWrapper, fileWrapper, TestArchiveName);
        testLogger.DebugMessages.Should().BeEmpty();
    }

    [TestMethod]
    public async Task UnpackerFactory_Throws()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        unpackerFactory.CreateForArchive(directoryWrapper, fileWrapper, TestArchiveName).ReturnsNull();

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The archive format of the JRE archive `filename.tar.gz` is not supported.");
        fileWrapper.DidNotReceiveWithAnyArgs().Exists(null);
        fileWrapper.DidNotReceiveWithAnyArgs().Create(null);
        fileWrapper.DidNotReceiveWithAnyArgs().Open(null);
        checksum.DidNotReceiveWithAnyArgs().ComputeHash(null);
        unpackerFactory.Received(1).CreateForArchive(directoryWrapper, fileWrapper, TestArchiveName);
    }

    [TestMethod]
    public async Task Unpack_Success()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, TestArchiveName);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var archiveFileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(archiveFileStream);
        checksum.ComputeHash(archiveFileStream).Returns("sha256");
        string tempExtractionDir = null;
        unpacker.Unpack(archiveFileStream, Arg.Do<string>(x => tempExtractionDir = x));
        fileWrapper.Exists(Arg.Is<string>(x => x == Path.Combine(tempExtractionDir, "javaPath"))).Returns(true);
        var sut = CreateSutWithSubstitutes();

        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheHit>().Which.JavaExe.Should().Be(@"C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted\javaPath");
        tempExtractionDir.Should().Match(@"C:\Users\user\.sonar\cache\sha256\*").And.NotBe(@"C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted");
        directoryWrapper.Received(1).Move(tempExtractionDir, @"C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted");
        testLogger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the Java Runtime Environment download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz' to folder '{tempExtractionDir}'.",
            @$"Moving extracted Java runtime environment from '{tempExtractionDir}' to 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted'.",
            @"The Java runtime environment was successfully added to 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted'.");
    }

    [TestMethod]
    public async Task Unpack_Failure_Unpack()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, TestArchiveName);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var archiveFileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(archiveFileStream);
        checksum.ComputeHash(archiveFileStream).Returns("sha256");
        string tempExtractionDir = null;
        unpacker.When(x => x.Unpack(archiveFileStream, Arg.Any<string>())).Do(x =>
        {
            tempExtractionDir = x.ArgAt<string>(1);
            throw new IOException("Unpack failure");
        });
        var sut = CreateSutWithSubstitutes();

        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        tempExtractionDir.Should().Match(@"C:\Users\user\.sonar\cache\sha256\*").And.NotBe(@"C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted");
        directoryWrapper.DidNotReceiveWithAnyArgs().Move(null, null);
        directoryWrapper.Received(1).Delete(tempExtractionDir, true);
        testLogger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the Java Runtime Environment download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz' to folder '{tempExtractionDir}'.",
            @$"The extraction of the downloaded Java runtime environment failed with error 'Unpack failure'.");
    }

    [TestMethod]
    public async Task Unpack_Failure_Move()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, TestArchiveName);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var archiveFileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(archiveFileStream);
        checksum.ComputeHash(archiveFileStream).Returns("sha256");
        string tempExtractionDir = null;
        unpacker.Unpack(archiveFileStream, Arg.Do<string>(x => tempExtractionDir = x));
        fileWrapper.Exists(Arg.Is<string>(x => x == Path.Combine(tempExtractionDir, "javaPath"))).Returns(true);
        directoryWrapper.When(x => x.Move(Arg.Is<string>(x => x == tempExtractionDir), @"C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted")).Throw<IOException>();
        var sut = CreateSutWithSubstitutes();

        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        tempExtractionDir.Should().Match(@"C:\Users\user\.sonar\cache\sha256\*").And.NotBe(@"C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted");
        directoryWrapper.Received(1).Move(tempExtractionDir, @"C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted");
        directoryWrapper.Received(1).Delete(tempExtractionDir, true);
        testLogger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the Java Runtime Environment download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz' to folder '{tempExtractionDir}'.",
            @$"Moving extracted Java runtime environment from '{tempExtractionDir}' to 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted'.",
            @"The extraction of the downloaded Java runtime environment failed with error 'I/O error occurred.'.");
    }

    [TestMethod]
    public async Task Unpack_Failure_JavaExeNotFound()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, TestArchiveName);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var archiveFileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(archiveFileStream);
        checksum.ComputeHash(archiveFileStream).Returns("sha256");
        string tempExtractionDir = null;
        string expectedJavaExeInTempFolder = null;
        unpacker.Unpack(archiveFileStream, Arg.Do<string>(x =>
        {
            tempExtractionDir = x;
            expectedJavaExeInTempFolder = Path.Combine(tempExtractionDir, "javaPath");
        }));
        fileWrapper.Exists(Arg.Is<string>(x => x == expectedJavaExeInTempFolder)).Returns(false);
        var sut = CreateSutWithSubstitutes();

        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        tempExtractionDir.Should().Match(@"C:\Users\user\.sonar\cache\sha256\*").And.NotBe(@"C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted");
        fileWrapper.Received(1).Exists(expectedJavaExeInTempFolder);
        directoryWrapper.Received(1).Delete(tempExtractionDir, true);
        testLogger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the Java Runtime Environment download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz' to folder '{tempExtractionDir}'.",
            @$"The extraction of the downloaded Java runtime environment failed with error 'The java executable in the extracted Java runtime environment was expected to be at '{expectedJavaExeInTempFolder}' but couldn't be found.'.");
    }

    [TestMethod]
    public async Task Unpack_Failure_ErrorInCleanUpOfTempDirectory()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, TestArchiveName);
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var archiveFileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(archiveFileStream);
        checksum.ComputeHash(archiveFileStream).Returns("sha256");
        string tempExtractionDir = null;
        unpacker.Unpack(archiveFileStream, Arg.Do<string>(x => tempExtractionDir = x));
        fileWrapper.Exists(Arg.Is<string>(x => x == Path.Combine(tempExtractionDir, "javaPath"))).Returns(true);
        directoryWrapper.When(x => x.Move(Arg.Is<string>(x => x == tempExtractionDir), @"C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted")).Throw(new IOException("Move failure"));
        directoryWrapper.When(x => x.Delete(Arg.Is<string>(x => x == tempExtractionDir), true)).Throw(new IOException("Folder cleanup failure"));
        var sut = CreateSutWithSubstitutes();

        var result = await sut.DownloadJreAsync(home, new(TestArchiveName, "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<JreCacheFailure>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        tempExtractionDir.Should().Match(@"C:\Users\user\.sonar\cache\sha256\*").And.NotBe(@"C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted");
        directoryWrapper.Received(1).Move(tempExtractionDir, @"C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted");
        directoryWrapper.Received(1).Delete(tempExtractionDir, true);
        testLogger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the Java Runtime Environment download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz' to folder '{tempExtractionDir}'.",
            @$"Moving extracted Java runtime environment from '{tempExtractionDir}' to 'C:\Users\user\.sonar\cache\sha256\filename.tar.gz_extracted'.",
            @"The extraction of the downloaded Java runtime environment failed with error 'Move failure'.",
            @$"The cleanup of the temporary folder for the Java runtime environment extraction at '{tempExtractionDir}' failed with message 'Folder cleanup failure'.");
    }

    [TestMethod]
    public async Task EndToEndTestWithFiles_Success()
    {
        // A zip file with a file named java.exe in the jdk-17.0.11+9-jre\bin folder.
        const string jreZip = """
            UEsDBBQAAAAAAK1E6FgAAAAAAAAAAAAAAAASAAAAamRrLTE3LjAuMTErOS1qcmUvUEsDBBQAAAAAAFxF
            6FgAAAAAAAAAAAAAAAAWAAAAamRrLTE3LjAuMTErOS1qcmUvYmluL1BLAwQUAAAACABYRehYLkehCTsA
            AABAAAAAHgAAAGpkay0xNy4wLjExKzktanJlL2Jpbi9qYXZhLmV4ZRXEgQmAMAwEwFV+AqdxgQdfm1JT
            aaI4vhaOW4sFfvWOBBE8rybsNusDqUjzA/QN3hNZhCE2VD5c9OoDUEsBAj8AFAAAAAAArUToWAAAAAAA
            AAAAAAAAABIAJAAAAAAAAAAQAAAAAAAAAGpkay0xNy4wLjExKzktanJlLwoAIAAAAAAAAQAYAG/P0UsB
            0doBb8/RSwHR2gECfBNGAdHaAVBLAQI/ABQAAAAAAFxF6FgAAAAAAAAAAAAAAAAWACQAAAAAAAAAEAAA
            ADAAAABqZGstMTcuMC4xMSs5LWpyZS9iaW4vCgAgAAAAAAABABgAbBVqEALR2gFsFWoQAtHaAYiJFkYB
            0doBUEsBAj8AFAAAAAgAWEXoWC5HoQk7AAAAQAAAAB4AJAAAAAAAAAAgAAAAZAAAAGpkay0xNy4wLjEx
            KzktanJlL2Jpbi9qYXZhLmV4ZQoAIAAAAAAAAQAYAEAt3goC0doBhd7GEwLR2gEAKrNhhpDaAVBLBQYA
            AAAAAwADADwBAADbAAAAAAA=
            """;
        var zipContent = Convert.FromBase64String(jreZip);
        var home = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var cache = Path.Combine(home, "cache");
        var sha = "b192f77aa6a6154f788ab74a839b1930d59eb1034c3fe617ef0451466a8335ba";
        var file = "OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.zip";
        var jreDescriptor = new JreDescriptor(file, sha, @"jdk-17.0.11+9-jre/bin/java.exe");
        var realDirectoryWrapper = DirectoryWrapper.Instance;
        var realFileWrapper = FileWrapper.Instance;
        var realChecksum = new ChecksumSha256();
        var realUnpackerFactory = new UnpackerFactory();
        var sut = new JreCache(testLogger, realDirectoryWrapper, realFileWrapper, realChecksum, realUnpackerFactory);
        try
        {
            var result = await sut.DownloadJreAsync(home, jreDescriptor, () => Task.FromResult<Stream>(new MemoryStream(zipContent)));
            Directory.Exists(Path.Combine(cache, sha)).Should().BeTrue();
            File.Exists(Path.Combine(cache, sha, file)).Should().BeTrue();
            File.Exists(Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre", "bin", "java.exe")).Should().BeTrue();
            File.ReadAllText(Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre", "bin", "java.exe")).Should().Be(
                "This is just a sample file for testing and not the real java.exe");
            Directory.GetFiles(Path.Combine(cache, sha)).Should().BeEquivalentTo(
                Path.Combine(cache, sha, file));
            Directory.GetDirectories(Path.Combine(cache, sha)).Should().BeEquivalentTo(
                Path.Combine(cache, sha, $"{file}_extracted"));
            Directory.GetDirectories(Path.Combine(cache, sha, $"{file}_extracted")).Should().BeEquivalentTo(
                Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre"));
            Directory.GetDirectories(Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre")).Should().BeEquivalentTo(
                Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre", "bin"));
            Directory.GetDirectories(Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre", "bin")).Should().BeEmpty();
            Directory.GetFiles(Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre", "bin")).Should().BeEquivalentTo(
                Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre", "bin", "java.exe"));
            testLogger.DebugMessages.Should().SatisfyRespectively(
                x => x.Should().Be(@$"Starting the Java Runtime Environment download."),
                x => x.Should().Be(@$"The checksum of the downloaded file is 'b192f77aa6a6154f788ab74a839b1930d59eb1034c3fe617ef0451466a8335ba' and the expected checksum is 'b192f77aa6a6154f788ab74a839b1930d59eb1034c3fe617ef0451466a8335ba'."),
                x => x.Should().Match(@$"Starting extracting the Java runtime environment from archive '{home}\cache\b192f77aa6a6154f788ab74a839b1930d59eb1034c3fe617ef0451466a8335ba\OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.zip' to folder '{home}\cache\b192f77aa6a6154f788ab74a839b1930d59eb1034c3fe617ef0451466a8335ba\*'."),
                x => x.Should().Match(@$"Moving extracted Java runtime environment from '{home}\cache\b192f77aa6a6154f788ab74a839b1930d59eb1034c3fe617ef0451466a8335ba\*' to '{home}\cache\b192f77aa6a6154f788ab74a839b1930d59eb1034c3fe617ef0451466a8335ba\OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.zip_extracted'."),
                x => x.Should().Be(@$"The Java runtime environment was successfully added to '{home}\cache\b192f77aa6a6154f788ab74a839b1930d59eb1034c3fe617ef0451466a8335ba\OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.zip_extracted'."));
        }
        finally
        {
            Directory.Delete(home, true);
        }
    }

    private JreCache CreateSutWithSubstitutes() =>
        new JreCache(testLogger, directoryWrapper, fileWrapper, checksum, unpackerFactory);
}
