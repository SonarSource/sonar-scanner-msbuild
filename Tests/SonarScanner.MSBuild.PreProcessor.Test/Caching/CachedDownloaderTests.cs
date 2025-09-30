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
    private const string TempFileName = "xFirst.rnd";
    private static readonly FileDescriptor FileDescriptor = new("someFile.jar", "sha256");
    private static readonly string SonarUserHome = Path.Combine("home", ".sonar");
    private static readonly string SonarUserHomeCache = Path.Combine(SonarUserHome, "cache");
    private static readonly string DownloadPath = Path.Combine(SonarUserHomeCache, ExpectedSha);
    private static readonly string DownloadFilePath = Path.Combine(DownloadPath, FileDescriptor.Filename);
    private static readonly string TempFilePath = Path.Combine(DownloadPath, TempFileName);
    private readonly IChecksum checksum;
    private readonly TestRuntime runtime;
    private readonly CachedDownloader cachedDownloader;
    private readonly byte[] fileContentArray = new byte[3];
    private readonly MemoryStream fileContentStream;
    private readonly byte[] downloadContentArray = [1, 2, 3,];

    public CachedDownloaderTests()
    {
        runtime = new();
        checksum = Substitute.For<IChecksum>();
        cachedDownloader = new CachedDownloader(runtime, checksum, FileDescriptor, SonarUserHome);
        runtime.Directory.GetRandomFileName().Returns(TempFileName);
        fileContentStream = new MemoryStream(fileContentArray, writable: true);
        runtime.File.Create(TempFilePath).Returns(fileContentStream);
        checksum.ComputeHash(null).ReturnsForAnyArgs(ExpectedSha);
    }

    public void Dispose() =>
        fileContentStream.Dispose();

    [TestMethod]
    public async Task DownloadFileAsync_DirectoryDoesNotExist_CreatesDirectory()
    {
        runtime.Directory.Exists(DownloadPath).Returns(false);

        await cachedDownloader.DownloadFileAsync(null);

        runtime.Directory.Received(1).Exists(DownloadPath);
        runtime.Directory.Received(1).CreateDirectory(DownloadPath);
    }

    [TestMethod]
    public async Task DownloadFileAsync_DirectoryExists_DoesNotCreateDirectory()
    {
        runtime.Directory.Exists(DownloadPath).Returns(true);

        await cachedDownloader.DownloadFileAsync(null);

        runtime.Directory.Received(1).Exists(DownloadPath);
        runtime.Directory.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public async Task DownloadFileAsync_CreateDirectoryThrows_ReturnsNull()
    {
        runtime.Directory.Exists(DownloadPath).Returns(false);
        runtime.Directory.When(x => x.CreateDirectory(DownloadPath)).Throw<IOException>();

        (await cachedDownloader.DownloadFileAsync(null)).Should().Be(new DownloadError($"The directory '{DownloadPath}' could not be created."));

        runtime.Directory.Received(1).Exists(DownloadPath);
        runtime.Directory.Received(1).CreateDirectory(DownloadPath);
    }

    [TestMethod]
    public async Task DownloadFileAsync_CacheHit_ReturnsFile()
    {
        var file = Path.Combine(SonarUserHomeCache, FileDescriptor.Sha256, FileDescriptor.Filename);
        runtime.File.Exists(file).Returns(true);

        var result = await cachedDownloader.DownloadFileAsync(() => Task.FromResult<Stream>(null));

        result.Should().BeOfType<CacheHit>().Which.FilePath.Should().Be(file);
        runtime.File.Received(1).Exists(file);
    }

    [TestMethod]
    public async Task DownloadFileAsync_Succeeds()
    {
        var result = await ExecuteDownloadFileAsync(new MemoryStream(downloadContentArray));

        result.Should().BeOfType<Downloaded>().Which.FilePath.Should().Be(DownloadFilePath);
        AssertStreamDisposed();
        runtime.File.Received(1).Create(TempFilePath);
        runtime.File.Received(1).Move(TempFilePath, DownloadFilePath);
        fileContentArray.Should().BeEquivalentTo(downloadContentArray);
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            $"Cache miss. Could not find '{DownloadFilePath}'.",
            $"The checksum of the downloaded file is '{ExpectedSha}' and the expected checksum is '{ExpectedSha}'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_NullStream_ReturnsDownloadError()
    {
        var result = await ExecuteDownloadFileAsync(null);

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be(
            "The download of the file from the server failed with the exception 'The download stream is null. The server likely returned an error status code.'.");
        AssertTempFileCreatedAndDeleted();
        AssertStreamDisposed();
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            $"Cache miss. Could not find '{DownloadFilePath}'.",
            $"Deleting file '{TempFilePath}'.",
            "The download of the file from the server failed with the exception 'The download stream is null. The server likely returned an error status code.'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_WrongChecksum_ReturnsDownloadError()
    {
        checksum.ComputeHash(null).ReturnsForAnyArgs("someOtherHash");

        var result = await ExecuteDownloadFileAsync(new MemoryStream(downloadContentArray));

        result.Should().BeOfType<DownloadError>().Which.Message
            .Should().Be("The download of the file from the server failed with the exception 'The checksum of the downloaded file does not match the expected checksum.'.");
        AssertTempFileCreatedAndDeleted();
        AssertStreamDisposed();
        fileContentArray.Should().BeEquivalentTo(downloadContentArray);
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            $"Cache miss. Could not find '{DownloadFilePath}'.",
            $"The checksum of the downloaded file is 'someOtherHash' and the expected checksum is '{ExpectedSha}'.",
            $"Deleting file '{Path.Combine(DownloadPath, TempFileName)}'.",
            "The download of the file from the server failed with the exception 'The checksum of the downloaded file does not match the expected checksum.'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_ChecksumCalculationFails_ReturnsDownloadError()
    {
        checksum.ComputeHash(null).ThrowsForAnyArgs<InvalidOperationException>();

        var result = await ExecuteDownloadFileAsync(new MemoryStream(downloadContentArray));

        result.Should().BeOfType<DownloadError>().Which.Message
            .Should().Be("The download of the file from the server failed with the exception 'The checksum of the downloaded file does not match the expected checksum.'.");
        AssertTempFileCreatedAndDeleted();
        AssertStreamDisposed();
        fileContentArray.Should().BeEquivalentTo(downloadContentArray);
        runtime.Logger.Should().HaveDebugs(
            $"Cache miss. Could not find '{DownloadFilePath}'.",
            $"The calculation of the checksum of the file '{TempFilePath}' failed with message 'Operation is not valid due to the current state of the object.'.",
            $"Deleting file '{Path.Combine(DownloadPath, TempFileName)}'.",
            "The download of the file from the server failed with the exception 'The checksum of the downloaded file does not match the expected checksum.'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_ValidFileCached_Succeeds()
    {
        runtime.File.Exists(DownloadFilePath).Returns(true);

        var result = await ExecuteDownloadFileAsync(new MemoryStream(downloadContentArray));

        result.Should().BeOfType<CacheHit>().Which.FilePath.Should().Be(DownloadFilePath);
        runtime.File.DidNotReceiveWithAnyArgs().Create(null);
        runtime.File.DidNotReceiveWithAnyArgs().Move(null, null);
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            $"The file was already downloaded from the server and stored at '{DownloadFilePath}'.",
            $"The checksum of the downloaded file is '{ExpectedSha}' and the expected checksum is '{ExpectedSha}'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_InvalidFileCached_DeletesAndRedownloads()
    {
        runtime.File.Exists(DownloadFilePath).Returns(true);
        checksum.ComputeHash(null).ReturnsForAnyArgs(x => "someOtherHash", x => ExpectedSha);

        var result = await ExecuteDownloadFileAsync(new MemoryStream(downloadContentArray));

        result.Should().BeOfType<Downloaded>().Which.FilePath.Should().Be(DownloadFilePath);
        runtime.File.Received(1).Delete(DownloadFilePath);
        runtime.File.Received(1).Move(TempFilePath, DownloadFilePath);
        runtime.File.Received(1).Create(TempFilePath);
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            $"The file was already downloaded from the server and stored at '{DownloadFilePath}'.",
            $"The checksum of the downloaded file is 'someOtherHash' and the expected checksum is '{ExpectedSha}'.",
            $"Deleting file '{DownloadFilePath}'.",
            $"Cache miss. Could not find '{DownloadFilePath}'.",
            $"The checksum of the downloaded file is '{ExpectedSha}' and the expected checksum is '{ExpectedSha}'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_DownloadFails_FileDownloadedByOtherScanner_Succeeds()
    {
        runtime.File.Exists(DownloadFilePath).Returns(false, true);

        var result = await ExecuteDownloadFileAsync(null);

        result.Should().BeOfType<Downloaded>().Which.FilePath.Should().Be(DownloadFilePath);
        AssertTempFileCreatedAndDeleted();
        AssertStreamDisposed();
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            $"Cache miss. Could not find '{DownloadFilePath}'.",
            $"Deleting file '{TempFilePath}'.",
            "The download of the file from the server failed with the exception 'The download stream is null. The server likely returned an error status code.'.",
            "The file was found after the download failed. Another scanner downloaded the file in parallel.",
            $"The checksum of the downloaded file is '{ExpectedSha}' and the expected checksum is '{ExpectedSha}'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_DownloadFails_FileDownloadedByOtherScannerInvalid_Fails()
    {
        runtime.File.Exists(DownloadFilePath).Returns(false, true);
        checksum.ComputeHash(null).ReturnsForAnyArgs("someOtherHash");

        var result = await ExecuteDownloadFileAsync(null);

        result.Should().BeOfType<DownloadError>().Which.Message
            .Should().Be("The checksum of the downloaded file does not match the expected checksum.");
        AssertTempFileCreatedAndDeleted();
        AssertStreamDisposed();
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            $"Cache miss. Could not find '{DownloadFilePath}'.",
            $"Deleting file '{TempFilePath}'.",
            "The download of the file from the server failed with the exception 'The download stream is null. The server likely returned an error status code.'.",
            "The file was found after the download failed. Another scanner downloaded the file in parallel.",
            $"The checksum of the downloaded file is 'someOtherHash' and the expected checksum is '{ExpectedSha}'.",
            $"Deleting file '{DownloadFilePath}'.");
    }

    [TestMethod]
    public async Task DownloadFileAsync_EnsureDownloadDirectoryFails()
    {
        runtime.Directory.When(x => x.CreateDirectory(DownloadPath)).Do(_ => throw new IOException());
        var result = await ExecuteDownloadFileAsync(new MemoryStream(downloadContentArray));
        result.Should().BeOfType<DownloadError>().Which.Message
            .Should().Be($"The directory '{DownloadPath}' could not be created.");
    }

    private async Task<DownloadResult> ExecuteDownloadFileAsync(MemoryStream downloadContent) =>
        await cachedDownloader.DownloadFileAsync(() => Task.FromResult<Stream>(downloadContent));

    private void AssertTempFileCreatedAndDeleted()
    {
        runtime.File.Received(1).Create(TempFilePath);
        runtime.File.Received(1).Delete(TempFilePath);
        runtime.File.DidNotReceiveWithAnyArgs().Move(null, null);
    }

    private void AssertStreamDisposed()
    {
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after success and failure.");
    }
}
