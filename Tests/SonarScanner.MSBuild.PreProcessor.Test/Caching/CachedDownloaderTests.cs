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

using NSubstitute.ExceptionExtensions;
using SonarScanner.MSBuild.PreProcessor.Interfaces;

namespace SonarScanner.MSBuild.PreProcessor.Caching.Test;

[TestClass]
public sealed class CachedDownloaderTests : IDisposable
{
    private const string ExpectedSha = "sha256";
    private const string DownloadTarget = "someFile";
    private const string TempFileName = "xFirst.rnd";
    private static readonly string SonarUserHome = Path.Combine("home", ".sonar");
    private static readonly string SonarUserHomeCache = Path.Combine(SonarUserHome, "cache");
    private static readonly string DownloadPath = Path.Combine(SonarUserHomeCache, ExpectedSha);
    private static readonly string DownloadFilePath = Path.Combine(DownloadPath, DownloadTarget);
    private static readonly string TempFilePath = Path.Combine(DownloadPath, TempFileName);
    private readonly IChecksum checksum;
    private readonly IDirectoryWrapper directoryWrapper;
    private readonly IFileWrapper fileWrapper;
    private readonly CachedDownloader cachedDownloader;
    private readonly TestLogger testLogger;
    private readonly byte[] fileContentArray = new byte[3];
    private readonly MemoryStream fileContentStream;
    private readonly byte[] downloadContentArray = [1, 2, 3,];

    public CachedDownloaderTests()
    {
        testLogger = new TestLogger();
        checksum = Substitute.For<IChecksum>();
        directoryWrapper = Substitute.For<IDirectoryWrapper>();
        fileWrapper = Substitute.For<IFileWrapper>();
        cachedDownloader = new CachedDownloader(testLogger, directoryWrapper, fileWrapper, checksum, SonarUserHome);
        directoryWrapper.GetRandomFileName().Returns(TempFileName);
        fileContentStream = new MemoryStream(fileContentArray, writable: true);
        fileWrapper.Create(TempFilePath).Returns(fileContentStream);
        checksum.ComputeHash(null).ReturnsForAnyArgs(ExpectedSha);
    }

    public void Dispose() =>
        fileContentStream.Dispose();

    [TestMethod]
    public void EnsureCacheRoot_DirectoryDoesNotExist_CreatesDirectory()
    {
        directoryWrapper.Exists(SonarUserHomeCache).Returns(false);

        var cacheRoot = cachedDownloader.EnsureCacheRoot();

        cacheRoot.Should().Be(SonarUserHomeCache);
        directoryWrapper.Received(1).Exists(SonarUserHomeCache);
        directoryWrapper.Received(1).CreateDirectory(SonarUserHomeCache);
    }

    [TestMethod]
    public void EnsureCacheRoot_DirectoryExists_DoesNotCreateDirectory()
    {
        directoryWrapper.Exists(SonarUserHomeCache).Returns(true);

        var cacheRoot = cachedDownloader.EnsureCacheRoot();

        cacheRoot.Should().Be(SonarUserHomeCache);
        directoryWrapper.Received(1).Exists(SonarUserHomeCache);
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureCacheRoot_CreateDirectoryThrows_ReturnsNull()
    {
        directoryWrapper.Exists(SonarUserHomeCache).Returns(false);
        directoryWrapper.When(x => x.CreateDirectory(SonarUserHomeCache)).Throw<IOException>();

        var cacheRoot = cachedDownloader.EnsureCacheRoot();

        cacheRoot.Should().BeNull();
        directoryWrapper.Received(1).Exists(SonarUserHomeCache);
        directoryWrapper.Received(1).CreateDirectory(SonarUserHomeCache);
    }

    [TestMethod]
    public void EnsureDirectoryExists_DirectoryDoesNotExist_CreatesDirectory()
    {
        var dir = "some/dir";
        directoryWrapper.Exists(dir).Returns(false);

        var result = cachedDownloader.EnsureDirectoryExists(dir);

        result.Should().Be(dir);
        directoryWrapper.Received(1).Exists(dir);
        directoryWrapper.Received(1).CreateDirectory(dir);
    }

    [TestMethod]
    public void EnsureDirectoryExists_DirectoryExists_DoesNotCreateDirectory()
    {
        var dir = "some/dir";
        directoryWrapper.Exists(dir).Returns(true);

        var result = cachedDownloader.EnsureDirectoryExists(dir);

        result.Should().Be(dir);
        directoryWrapper.Received(1).Exists(dir);
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureDirectoryExists_CreateDirectoryFails_ReturnsNull()
    {
        var dir = "some/dir";
        directoryWrapper.Exists(dir).Returns(false);
        directoryWrapper.When(x => x.CreateDirectory(dir)).Throw<IOException>();

        var result = cachedDownloader.EnsureDirectoryExists(dir);

        result.Should().BeNull();
        directoryWrapper.Received(1).Exists(dir);
        directoryWrapper.Received(1).CreateDirectory(dir);
    }

    [TestMethod]
    public void CacheRoot_ExpectedPath_IsReturned() =>
        cachedDownloader.CacheRoot.Should().Be(SonarUserHomeCache);

    [TestMethod]
    public void IsFileCached_CacheHit_ReturnsFile()
    {
        var fileDescriptor = new FileDescriptor("somefile.jar", "sha256");
        var file = Path.Combine(SonarUserHomeCache, fileDescriptor.Sha256, fileDescriptor.Filename);
        fileWrapper.Exists(file).Returns(true);

        var result = cachedDownloader.IsFileCached(fileDescriptor);

        result.Should().BeOfType<CacheHit>().Which.FilePath.Should().Be(file);
        fileWrapper.Received(1).Exists(file);
    }

    [TestMethod]
    public void IsFileCached_FileDoesNotExist_ReturnsCacheMiss()
    {
        var fileDescriptor = new FileDescriptor("somefile.jar", "sha256");
        var file = Path.Combine(SonarUserHomeCache, fileDescriptor.Sha256, fileDescriptor.Filename);
        fileWrapper.Exists(file).Returns(false);

        var result = cachedDownloader.IsFileCached(fileDescriptor);

        result.Should().BeOfType<CacheMiss>();
        fileWrapper.Received(1).Exists(file);
    }

    [TestMethod]
    public void IsFileCached_CreateDirectoryThrows_ReturnsCacheFailure()
    {
        var fileDescriptor = new FileDescriptor("somefile.jar", "sha256");
        directoryWrapper.Exists(SonarUserHomeCache).Returns(false);
        directoryWrapper.When(x => x.CreateDirectory(SonarUserHomeCache)).Do(_ => throw new IOException("Disk full"));

        var result = cachedDownloader.IsFileCached(fileDescriptor);

        result.Should().BeOfType<CacheError>().Which.Message.Should().Be($"The file cache directory in '{SonarUserHomeCache}' could not be created.");
    }

    [TestMethod]
    public void ValidateChecksum_ValidChecksum_ReturnsTrue()
    {
        ExecuteValidateChecksumTest(ExpectedSha, ExpectedSha, true);

        testLogger.AssertDebugLogged($"""
            The checksum of the downloaded file is '{ExpectedSha}' and the expected checksum is '{ExpectedSha}'.
            """);
    }

    [TestMethod]
    public void ValidateChecksum_InvalidChecksum_ReturnsFalse()
    {
        var returnedSha = "invalidsha";
        ExecuteValidateChecksumTest(returnedSha, ExpectedSha, false);

        testLogger.AssertDebugLogged($"""
            The checksum of the downloaded file is '{returnedSha}' and the expected checksum is '{ExpectedSha}'.
            """);
    }

    [TestMethod]
    public void ValidateChecksum_ChecksumCalculationFails_ReturnsFalse()
    {
        ExecuteValidateChecksumTest(null, "sha256", false, DownloadTarget);

        testLogger.AssertDebugLogged($"""
            The calculation of the checksum of the file '{DownloadTarget}' failed with message 'Operation is not valid due to the current state of the object.'.
            """);
    }

    [TestMethod]
    public async Task DownloadFileAsync_Succeeds()
    {
        var result = await ExecuteDownloadFileAsync(new MemoryStream(downloadContentArray));

        result.Should().BeOfType<DownloadSuccess>().Which.FilePath.Should().Be(DownloadFilePath);
        AssertStreamDisposed();
        fileWrapper.Received(1).Create(TempFilePath);
        fileWrapper.Received(1).Move(TempFilePath, DownloadFilePath);
        fileContentArray.Should().BeEquivalentTo(downloadContentArray);
        testLogger.DebugMessages.Should().BeEquivalentTo(
            "Starting the file download.",
            $"The checksum of the downloaded file is '{ExpectedSha}' and the expected checksum is '{ExpectedSha}'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_NullStream_ReturnsCacheFailure()
    {
        var result = await ExecuteDownloadFileAsync(null);

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be(
            "The download of the file from the server failed with the exception 'The download stream is null. The server likely returned an error status code.'.");
        AssertTempFileCreatedAndDeleted();
        AssertStreamDisposed();
        testLogger.DebugMessages.Should().BeEquivalentTo(
            "Starting the file download.",
            $"Deleting file '{TempFilePath}'.",
            "The download of the file from the server failed with the exception 'The download stream is null. The server likely returned an error status code.'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_WrongChecksum_ReturnsCacheFailure()
    {
        checksum.ComputeHash(null).ReturnsForAnyArgs("someOtherHash");

        var result = await ExecuteDownloadFileAsync(new MemoryStream(downloadContentArray));

        result.Should().BeOfType<DownloadError>().Which.Message
            .Should().Be("The download of the file from the server failed with the exception 'The checksum of the downloaded file does not match the expected checksum.'.");
        AssertTempFileCreatedAndDeleted();
        AssertStreamDisposed();
        fileContentArray.Should().BeEquivalentTo(downloadContentArray);
        testLogger.DebugMessages.Should().BeEquivalentTo(
            "Starting the file download.",
            $"The checksum of the downloaded file is 'someOtherHash' and the expected checksum is '{ExpectedSha}'.",
            $"Deleting file '{Path.Combine(DownloadPath, TempFileName)}'.",
            "The download of the file from the server failed with the exception 'The checksum of the downloaded file does not match the expected checksum.'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_ValidFileCached_Succeeds()
    {
        fileWrapper.Exists(DownloadFilePath).Returns(true);
        var result = await ExecuteDownloadFileAsync(new MemoryStream(downloadContentArray));

        result.Should().BeOfType<DownloadSuccess>().Which.FilePath.Should().Be(DownloadFilePath);
        fileWrapper.DidNotReceiveWithAnyArgs().Create(null);
        fileWrapper.DidNotReceiveWithAnyArgs().Move(null, null);
        testLogger.DebugMessages.Should().BeEquivalentTo(
            $"The file was already downloaded from the server and stored at '{DownloadFilePath}'.",
            $"The checksum of the downloaded file is '{ExpectedSha}' and the expected checksum is '{ExpectedSha}'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_InvalidFileCached_ReturnsCacheFailure()
    {
        fileWrapper.Exists(DownloadFilePath).Returns(true);
        checksum.ComputeHash(null).ReturnsForAnyArgs("someOtherHash");

        var result = await ExecuteDownloadFileAsync(new MemoryStream(downloadContentArray));

        result.Should().BeOfType<DownloadError>().Which.Message
            .Should().Be("The checksum of the downloaded file does not match the expected checksum.");
        fileWrapper.DidNotReceiveWithAnyArgs().Create(null);
        fileWrapper.DidNotReceiveWithAnyArgs().Move(null, null);
        testLogger.DebugMessages.Should().BeEquivalentTo(
            $"The file was already downloaded from the server and stored at '{DownloadFilePath}'.",
            $"The checksum of the downloaded file is 'someOtherHash' and the expected checksum is '{ExpectedSha}'.",
            $"Deleting file '{DownloadFilePath}'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_DownloadFails_FileDownloadedByOtherScanner_Succeeds()
    {
        fileWrapper.Exists(DownloadFilePath).Returns(false, true);

        var result = await ExecuteDownloadFileAsync(null);

        result.Should().BeOfType<DownloadSuccess>().Which.FilePath.Should().Be(DownloadFilePath);
        AssertTempFileCreatedAndDeleted();
        AssertStreamDisposed();
        testLogger.DebugMessages.Should().BeEquivalentTo(
            "Starting the file download.",
            $"Deleting file '{TempFilePath}'.",
            "The download of the file from the server failed with the exception 'The download stream is null. The server likely returned an error status code.'.",
            "The file was found after the download failed. Another scanner downloaded the file in parallel.",
            $"The file was already downloaded from the server and stored at '{DownloadFilePath}'.",
            $"The checksum of the downloaded file is '{ExpectedSha}' and the expected checksum is '{ExpectedSha}'.");
    }

    [TestMethod]
    public async Task DownloadFileAsyncDownloadFails_FileDownloadedByOtherScannerInvalid_Fails()
    {
        fileWrapper.Exists(DownloadFilePath).Returns(false, true);
        checksum.ComputeHash(null).ReturnsForAnyArgs("someOtherHash");

        var result = await ExecuteDownloadFileAsync(null);

        result.Should().BeOfType<DownloadError>().Which.Message
            .Should().Be("The checksum of the downloaded file does not match the expected checksum.");
        AssertTempFileCreatedAndDeleted();
        AssertStreamDisposed();
        testLogger.DebugMessages.Should().BeEquivalentTo(
            "Starting the file download.",
            $"Deleting file '{TempFilePath}'.",
            "The download of the file from the server failed with the exception 'The download stream is null. The server likely returned an error status code.'.",
            "The file was found after the download failed. Another scanner downloaded the file in parallel.",
            $"The file was already downloaded from the server and stored at '{DownloadFilePath}'.",
            $"The checksum of the downloaded file is 'someOtherHash' and the expected checksum is '{ExpectedSha}'.",
            $"Deleting file '{DownloadFilePath}'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_EnsureDownloadDirectory_Fails()
    {
        directoryWrapper.When(x => x.CreateDirectory(SonarUserHomeCache)).Do(_ => throw new IOException());
        var result = await ExecuteDownloadFileAsync(new MemoryStream(downloadContentArray));
        result.Should().BeOfType<DownloadError>().Which.Message
            .Should().Be($"The file cache directory in '{DownloadPath}' could not be created.");
    }

    private async Task<DownloadResult> ExecuteDownloadFileAsync(MemoryStream downloadContent) =>
        await cachedDownloader.DownloadFileAsync(new FileDescriptor(DownloadTarget, ExpectedSha), () => Task.FromResult<Stream>(downloadContent));

    private void ExecuteValidateChecksumTest(string returnedSha, string expectedSha, bool expectSucces, string downloadTarget = "some.file")
    {
        using var stream = new MemoryStream();
        fileWrapper.Open(downloadTarget).Returns(stream);
        if (returnedSha is null)
        {
            checksum.ComputeHash(stream).Throws<InvalidOperationException>();
        }
        else
        {
            checksum.ComputeHash(stream).Returns(returnedSha);
        }
        cachedDownloader.ValidateChecksum(downloadTarget, expectedSha).Should().Be(expectSucces);
        fileWrapper.Received(1).Open(downloadTarget);
        checksum.Received(1).ComputeHash(stream);
    }

    private void AssertTempFileCreatedAndDeleted()
    {
        fileWrapper.Received(1).Create(TempFilePath);
        fileWrapper.Received(1).Delete(TempFilePath);
        fileWrapper.DidNotReceiveWithAnyArgs().Move(null, null);
    }

    private void AssertStreamDisposed()
    {
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after success and failure.");
    }
}
