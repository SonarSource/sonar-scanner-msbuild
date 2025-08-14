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

using System.Security.Cryptography;
using NSubstitute.ExceptionExtensions;
using SonarScanner.MSBuild.PreProcessor.Interfaces;

namespace SonarScanner.MSBuild.PreProcessor.Caching.Test;

[TestClass]
public sealed class FileCacheTests : IDisposable
{
    private static readonly string SonarUserHome = Path.Combine("home", ".sonar");
    private static readonly string SonarUserHomeCache = Path.Combine(SonarUserHome, "cache");
    private readonly IChecksum checksum;
    private readonly IDirectoryWrapper directoryWrapper;
    private readonly IFileWrapper fileWrapper;
    private readonly FileCache fileCache;
    private readonly TestLogger testLogger;
    private readonly string downloadPath = Path.Combine(SonarUserHomeCache, "somePath");
    private readonly string downloadTarget = "someFile";
    private readonly string tempFileName = "xFirst.rnd";
    private readonly string expectedSha = "sha256";
    private readonly byte[] fileContentArray = new byte[3];
    private readonly MemoryStream fileContentStream;
    private readonly byte[] downloadContentArray = [1, 2, 3,];
    private readonly string tempFilePath;

    public FileCacheTests()
    {
        testLogger = new TestLogger();
        checksum = Substitute.For<IChecksum>();
        directoryWrapper = Substitute.For<IDirectoryWrapper>();
        fileWrapper = Substitute.For<IFileWrapper>();
        fileCache = new FileCache(testLogger, directoryWrapper, fileWrapper, checksum, SonarUserHome);

        directoryWrapper.GetRandomFileName().Returns(tempFileName);
        fileContentStream = new MemoryStream(fileContentArray, writable: true);
        tempFilePath = Path.Combine(downloadPath, tempFileName);
        fileWrapper.Create(tempFilePath).Returns(fileContentStream);
        checksum.ComputeHash(null).ReturnsForAnyArgs(expectedSha);
    }

    public void Dispose() =>
        fileContentStream.Dispose();

    [TestMethod]
    public void EnsureCacheRoot_DirectoryDoesNotExist_CreatesDirectory()
    {
        directoryWrapper.Exists(SonarUserHomeCache).Returns(false);

        var cacheRoot = fileCache.EnsureCacheRoot();

        cacheRoot.Should().Be(SonarUserHomeCache);
        directoryWrapper.Received(1).Exists(SonarUserHomeCache);
        directoryWrapper.Received(1).CreateDirectory(SonarUserHomeCache);
    }

    [TestMethod]
    public void EnsureCacheRoot_DirectoryExists_DoesNotCreateDirectory()
    {
        directoryWrapper.Exists(SonarUserHomeCache).Returns(true);

        var cacheRoot = fileCache.EnsureCacheRoot();

        cacheRoot.Should().Be(SonarUserHomeCache);
        directoryWrapper.Received(1).Exists(SonarUserHomeCache);
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureCacheRoot_CreateDirectoryThrows_ReturnsNull()
    {
        directoryWrapper.Exists(SonarUserHomeCache).Returns(false);
        directoryWrapper.When(x => x.CreateDirectory(SonarUserHomeCache)).Throw<IOException>();

        var cacheRoot = fileCache.EnsureCacheRoot();

        cacheRoot.Should().BeNull();
        directoryWrapper.Received(1).Exists(SonarUserHomeCache);
        directoryWrapper.Received(1).CreateDirectory(SonarUserHomeCache);
    }

    [TestMethod]
    public void EnsureDirectoryExists_DirectoryDoesNotExist_CreatesDirectory()
    {
        var dir = "some/dir";
        directoryWrapper.Exists(dir).Returns(false);

        var result = fileCache.EnsureDirectoryExists(dir);

        result.Should().Be(dir);
        directoryWrapper.Received(1).Exists(dir);
        directoryWrapper.Received(1).CreateDirectory(dir);
    }

    [TestMethod]
    public void EnsureDirectoryExists_DirectoryExists_DoesNotCreateDirectory()
    {
        var dir = "some/dir";
        directoryWrapper.Exists(dir).Returns(true);

        var result = fileCache.EnsureDirectoryExists(dir);

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

        var result = fileCache.EnsureDirectoryExists(dir);

        result.Should().BeNull();
        directoryWrapper.Received(1).Exists(dir);
        directoryWrapper.Received(1).CreateDirectory(dir);
    }

    [TestMethod]
    public void CacheRoot_ExpectedPath_IsReturned() =>
        fileCache.CacheRoot.Should().Be(SonarUserHomeCache);

    [TestMethod]
    public void IsFileCached_CacheHit_ReturnsFile()
    {
        var fileDescriptor = new FileDescriptor("somefile.jar", "sha256");
        var file = Path.Combine(SonarUserHomeCache, fileDescriptor.Sha256, fileDescriptor.Filename);
        fileWrapper.Exists(file).Returns(true);

        var result = fileCache.IsFileCached(fileDescriptor);

        result.Should().BeOfType<CacheHit>().Which.FilePath.Should().Be(file);
        fileWrapper.Received(1).Exists(file);
    }

    [TestMethod]
    public void IsFileCached_FileDoesNotExist_ReturnsCacheMiss()
    {
        var fileDescriptor = new FileDescriptor("somefile.jar", "sha256");
        var file = Path.Combine(SonarUserHomeCache, fileDescriptor.Sha256, fileDescriptor.Filename);
        fileWrapper.Exists(file).Returns(false);

        var result = fileCache.IsFileCached(fileDescriptor);

        result.Should().BeOfType<CacheMiss>();
        fileWrapper.Received(1).Exists(file);
    }

    [TestMethod]
    public void IsFileCached_CreateDirectoryThrows_ReturnsCacheFailure()
    {
        var fileDescriptor = new FileDescriptor("somefile.jar", "sha256");
        directoryWrapper.Exists(SonarUserHomeCache).Returns(false);
        directoryWrapper.When(x => x.CreateDirectory(SonarUserHomeCache)).Do(_ => throw new IOException("Disk full"));

        var result = fileCache.IsFileCached(fileDescriptor);

        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be($"The file cache directory in '{SonarUserHomeCache}' could not be created.");
    }

    [TestMethod]
    public void ValidateChecksum_ValidChecksum_ReturnsTrue()
    {
        ExecuteValidateChecksumTest(expectedSha, expectedSha, true);

        testLogger.AssertDebugLogged($"""
            The checksum of the downloaded file is '{expectedSha}' and the expected checksum is '{expectedSha}'.
            """);
    }

    [TestMethod]
    public void ValidateChecksum_InvalidChecksum_ReturnsFalse()
    {
        var returnedSha = "invalidsha";
        ExecuteValidateChecksumTest(returnedSha, expectedSha, false);

        testLogger.AssertDebugLogged($"""
            The checksum of the downloaded file is '{returnedSha}' and the expected checksum is '{expectedSha}'.
            """);
    }

    [TestMethod]
    public void ValidateChecksum_ChecksumCalculationFails_ReturnsFalse()
    {
        ExecuteValidateChecksumTest(null, "sha256", false, downloadTarget);

        testLogger.AssertDebugLogged($"""
            The calculation of the checksum of the file '{downloadTarget}' failed with message 'Operation is not valid due to the current state of the object.'.
            """);
    }

    [TestMethod]
    public async Task DownloadAndValidateFile_Success()
    {
        var result = await ExecuteDownloadAndValidateFile(new MemoryStream(downloadContentArray));

        result.Should().BeNull();
        AssertStreamDisposed();
        fileWrapper.Received(1).Create(tempFilePath);
        fileWrapper.Received(1).Move(tempFilePath, downloadTarget);
        fileContentArray.Should().BeEquivalentTo(downloadContentArray);
        testLogger.DebugMessages.Should().BeEquivalentTo($"The checksum of the downloaded file is '{expectedSha}' and the expected checksum is '{expectedSha}'.");
    }

    [TestMethod]
    public async Task DownloadAndValidateFile_NullStream_ReturnsInvalidOperationException()
    {
        var result = await ExecuteDownloadAndValidateFile(null);

        result.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(
            "The download stream is null. The server likely returned an error status code.");
        AssertTempFileCreatedAndDeleted();
        AssertStreamDisposed();
        testLogger.DebugMessages.Should().BeEquivalentTo($"Deleting file '{tempFilePath}'.");
    }

    [TestMethod]
    public async Task DownloadAndValidateFile_WrongChecksum_ReturnsCryptographicException()
    {
        checksum.ComputeHash(null).ReturnsForAnyArgs("someOtherHash");

        var result = await ExecuteDownloadAndValidateFile(new MemoryStream(downloadContentArray));

        result.Should().BeOfType<CryptographicException>().Which.Message.Should().Be("The checksum of the downloaded file does not match the expected checksum.");
        AssertTempFileCreatedAndDeleted();
        fileContentArray.Should().BeEquivalentTo(downloadContentArray);
        AssertStreamDisposed();
        testLogger.DebugMessages.Should().BeEquivalentTo(
            $"The checksum of the downloaded file is 'someOtherHash' and the expected checksum is '{expectedSha}'.",
            $"Deleting file '{tempFilePath}'.");
    }

    private async Task<Exception> ExecuteDownloadAndValidateFile(MemoryStream downloadContent) =>
        await fileCache.DownloadAndValidateFile(downloadPath, downloadTarget, new FileDescriptor(downloadTarget, expectedSha), () => Task.FromResult<Stream>(downloadContent));

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
        fileCache.ValidateChecksum(downloadTarget, expectedSha).Should().Be(expectSucces);
        fileWrapper.Received(1).Open(downloadTarget);
        checksum.Received(1).ComputeHash(stream);
    }

    private void AssertTempFileCreatedAndDeleted()
    {
        fileWrapper.Received(1).Create(tempFilePath);
        fileWrapper.Received(1).Delete(tempFilePath);
        fileWrapper.DidNotReceiveWithAnyArgs().Move(null, null);
    }

    private void AssertStreamDisposed()
    {
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after success and failure.");
    }
}
