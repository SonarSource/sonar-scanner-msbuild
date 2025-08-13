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
public class FileCacheTests
{
    private static readonly string SonarUserHome = Path.Combine("home", ".sonar");
    private static readonly string SonarUserHomeCache = Path.Combine(SonarUserHome, "cache");
    private readonly IChecksum checksum;
    private readonly IDirectoryWrapper directoryWrapper;
    private readonly IFileWrapper fileWrapper;
    private readonly FileCache fileCache;
    private readonly TestLogger testLogger;

    public FileCacheTests()
    {
        testLogger = new TestLogger();
        checksum = Substitute.For<IChecksum>();
        directoryWrapper = Substitute.For<IDirectoryWrapper>();
        fileWrapper = Substitute.For<IFileWrapper>();
        fileCache = new FileCache(testLogger, directoryWrapper, fileWrapper, checksum, SonarUserHome);
    }

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
        var sha256 = "validsha256";

        ExecuteValidateChecksumTest(sha256, sha256, true);

        testLogger.AssertDebugLogged($"""
            The checksum of the downloaded file is '{sha256}' and the expected checksum is '{sha256}'.
            """);
    }

    [TestMethod]
    public void ValidateChecksum_InvalidChecksum_ReturnsFalse()
    {
        var returnedSha = "invalidsha";
        var expectedSha = "otherSha";

        ExecuteValidateChecksumTest(returnedSha, expectedSha, false);

        testLogger.AssertDebugLogged($"""
            The checksum of the downloaded file is '{returnedSha}' and the expected checksum is '{expectedSha}'.
            """);
    }

    [TestMethod]
    public void ValidateChecksum_ChecksumCalculationFails_ReturnsFalse()
    {
        var downloadTarget = "some.file";

        ExecuteValidateChecksumTest(null, "sha256", false, downloadTarget);

        testLogger.AssertDebugLogged($"""
            The calculation of the checksum of the file '{downloadTarget}' failed with message 'Operation is not valid due to the current state of the object.'.
            """);
    }

    [TestMethod]
    public async Task DownloadAndValidateFile_Success()
    {
        var context = new ChecksumContext(fileCache, directoryWrapper, fileWrapper, checksum);

        var result = await context.DownloadAndValidateFileTest(new MemoryStream(context.DownloadContentArray));

        result.Should().BeNull();
        context.AssertStreamDisposed();
        fileWrapper.Received(1).Create(Path.Combine(context.DownloadPath, context.TempFileName));
        fileWrapper.Received(1).Move(Path.Combine(context.DownloadPath, context.TempFileName), context.DownloadTarget);
        context.FileContentArray.Should().BeEquivalentTo(context.DownloadContentArray);
        testLogger.DebugMessages.Should().BeEquivalentTo($"The checksum of the downloaded file is '{context.ExpectedSha}' and the expected checksum is '{context.ExpectedSha}'.");
    }

    [TestMethod]
    public async Task DownloadAndValidateFile_NullStream_ReturnsInvalidOperationException()
    {
        var context = new ChecksumContext(fileCache, directoryWrapper, fileWrapper, checksum);

        var result = await context.DownloadAndValidateFileTest(null);

        result.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(
            "The download stream is null. The server likely returned an error status code.");
        context.AssertTempFileCreatedAndDeleted();
        context.AssertStreamDisposed();
        testLogger.DebugMessages.Should().BeEquivalentTo($"Deleting file '{Path.Combine(context.DownloadPath, context.TempFileName)}'.");
    }

    [TestMethod]
    public async Task DownloadAndValidateFile_WrongChecksum_ReturnsCryptographicException()
    {
        var context = new ChecksumContext(fileCache, directoryWrapper, fileWrapper, checksum);
        checksum.ComputeHash(context.FileContentStream).ReturnsForAnyArgs("someOtherHash");

        var result = await context.DownloadAndValidateFileTest(new MemoryStream(context.DownloadContentArray));

        result.Should().BeOfType<CryptographicException>().Which.Message.Should().Be("The checksum of the downloaded file does not match the expected checksum.");
        context.AssertTempFileCreatedAndDeleted();
        context.FileContentArray.Should().BeEquivalentTo(context.DownloadContentArray);
        context.AssertStreamDisposed();
        testLogger.DebugMessages.Should().BeEquivalentTo(
            $"The checksum of the downloaded file is 'someOtherHash' and the expected checksum is '{context.ExpectedSha}'.",
            $"Deleting file '{Path.Combine(context.DownloadPath, context.TempFileName)}'.");
    }

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

    private class ChecksumContext
    {
        public readonly byte[] FileContentArray = new byte[3];
        public readonly MemoryStream FileContentStream;
        public readonly byte[] DownloadContentArray = [1, 2, 3,];
        public readonly string DownloadPath = Path.Combine(SonarUserHomeCache, "somePath");
        public readonly string DownloadTarget = "someFile";
        public readonly string TempFileName = "xFirst.rnd";
        public readonly string ExpectedSha = "sha256";

        private readonly FileCache fileCache;
        private readonly IFileWrapper fileWrapper;

        public ChecksumContext(FileCache fileCache, IDirectoryWrapper directoryWrapper, IFileWrapper fileWrapper, IChecksum checksum)
        {
            this.fileCache = fileCache;
            this.fileWrapper = fileWrapper;
            directoryWrapper.GetRandomFileName().Returns(TempFileName);
            FileContentStream = new MemoryStream(FileContentArray, writable: true);
            fileWrapper.Create(Path.Combine(DownloadPath, TempFileName)).Returns(FileContentStream);
            checksum.ComputeHash(FileContentStream).ReturnsForAnyArgs(ExpectedSha);
        }

        public async Task<Exception> DownloadAndValidateFileTest(MemoryStream expectedResult) =>
            await fileCache.DownloadAndValidateFile(DownloadPath, DownloadTarget, new FileDescriptor(DownloadTarget, ExpectedSha), () => Task.FromResult<Stream>(expectedResult));

        public void AssertTempFileCreatedAndDeleted()
        {
            fileWrapper.Received(1).Create(Path.Combine(DownloadPath, TempFileName));
            fileWrapper.Received(1).Delete(Path.Combine(DownloadPath, TempFileName));
            fileWrapper.DidNotReceive().Move(Path.Combine(DownloadPath, TempFileName), DownloadTarget);
        }

        public void AssertStreamDisposed()
        {
            var streamAccess = () => FileContentStream.Position;
            streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after succes and failure.");
        }
    }
}
