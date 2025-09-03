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
using SonarScanner.MSBuild.PreProcessor.Caching;
using SonarScanner.MSBuild.PreProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor.Unpacking;

namespace SonarScanner.MSBuild.PreProcessor.JreResolution.Test;

[TestClass]
public class JreDownloaderTests
{
    private static readonly string SonarUserHome = Path.Combine("C:", "Users", "user", ".sonar");
    private static readonly string SonarCache = Path.Combine(SonarUserHome, "cache");
    private static readonly string ShaPath = Path.Combine(SonarCache, "sha256");

    private readonly TestRuntime runtime;
    private readonly IChecksum checksum;
    private readonly IUnpacker unpacker;
    private readonly MemoryStream failingStream;

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

    public JreDownloaderTests()
    {
        runtime = new();
        checksum = Substitute.For<IChecksum>();
        unpacker = Substitute.For<IUnpacker>();
        failingStream = Substitute.For<MemoryStream>();
        failingStream.CopyToAsync(null, default, default).ThrowsAsyncForAnyArgs(new InvalidOperationException("Download failure simulation."));
    }

    [TestMethod]
    public void ExtractedDirectoryDoesNotExists()
    {
        var expectedExtractedPath = Path.Combine(SonarCache, "sha256", "filename.tar.gz_extracted");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(expectedExtractedPath).Returns(false);

        var sut = CreateSutWithSubstitutes(new JreDescriptor("filename.tar.gz", "sha", "jdk/bin/java"));
        var result = sut.IsJreCached();
        result.Should().BeNull();
        runtime.Directory.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public void JavaExecutableDoesNotExists()
    {
        var expectedExtractedPath = Path.Combine(SonarCache, "sha256", "filename.tar.gz_extracted");
        var expectedExtractedJavaExe = Path.Combine(expectedExtractedPath, "javaPath");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(expectedExtractedPath).Returns(true);
        runtime.File.Exists(expectedExtractedJavaExe).Returns(false);

        var sut = CreateSutWithSubstitutes();
        var result = sut.IsJreCached();
        result.Should().BeNull();
        runtime.Directory.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public void CacheHit()
    {
        var expectedExtractedPath = Path.Combine(SonarCache, "sha256", "filename.tar.gz_extracted");
        var expectedExtractedJavaExe = Path.Combine(expectedExtractedPath, "javaPath");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(expectedExtractedPath).Returns(true);
        runtime.File.Exists(expectedExtractedJavaExe).Returns(true);

        var sut = CreateSutWithSubstitutes();
        var result = sut.IsJreCached();
        result.Should().Be(expectedExtractedJavaExe);
        runtime.Directory.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public async Task Download_DownloadDirectoryIsCreated()
    {
        runtime.Directory.Exists(SonarCache).Returns(false);
        var sut = CreateSutWithSubstitutes();
        await sut.DownloadJreAsync(() => Task.FromResult<Stream>(null));
        runtime.Directory.Received(1).CreateDirectory(ShaPath);
    }

    [TestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_ShaDirectoryCanNotBeCreated(Type exception)
    {
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.When(x => x.CreateDirectory(ShaPath)).Throw((Exception)Activator.CreateInstance(exception));

        var result = await ExecuteDownloadAndUnpack();

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be($"The directory '{ShaPath}' could not be created.");
    }

    [TestMethod]
    public async Task Download_DownloadFileExists_ChecksumInvalid_DeletesAndReDownloadsTheFile()
    {
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.File.Exists(Path.Combine(ShaPath, "javaPath")).Returns(true);
        runtime.File.Exists(Path.Combine(ShaPath, "filename.tar.gz")).Returns(true);

        runtime.File.Create(null).ReturnsForAnyArgs(new MemoryStream(new byte[3], writable: true));
        checksum.ComputeHash(null).ReturnsForAnyArgs(x => "notValid", x => "sha256");

        var result = await ExecuteDownloadAndUnpack();

        result.Should().BeOfType<DownloadSuccess>().Which.FilePath.Should().Be(Path.Combine(ShaPath, "filename.tar.gz_extracted", "javaPath"));
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            $"The file was already downloaded from the server and stored at '{Path.Combine(ShaPath, "filename.tar.gz")}'.",
            "The checksum of the downloaded file is 'notValid' and the expected checksum is 'sha256'.",
            $"Deleting file '{Path.Combine(ShaPath, "filename.tar.gz")}'.",
            "Starting the file download.",
            "The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            $"Starting extracting the Java runtime environment from archive '{Path.Combine(ShaPath, "filename.tar.gz")}' to folder '{ShaPath}'.",
            $"Moving extracted Java runtime environment from '{ShaPath}' to '{Path.Combine(ShaPath, "filename.tar.gz_extracted")}'.",
            $"The Java runtime environment was successfully added to '{Path.Combine(ShaPath, "filename.tar.gz_extracted")}'.");
    }

    [TestMethod]
    public async Task Download_DownloadFileExists_ChecksumValid()
    {
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.File.Exists(Path.Combine(ShaPath, "filename.tar.gz")).Returns(true);
        var fileContent = new MemoryStream();
        runtime.File.Open(Path.Combine(ShaPath, "filename.tar.gz")).Returns(fileContent);
        checksum.ComputeHash(fileContent).Returns("sha256");

        var result = await ExecuteDownloadAndUnpack();

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            $"The file was already downloaded from the server and stored at '{Path.Combine(ShaPath, "filename.tar.gz")}'.",
            "The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            $"Starting extracting the Java runtime environment from archive '{Path.Combine(ShaPath, "filename.tar.gz")}' to folder '{ShaPath}'.",
            $"The extraction of the downloaded Java runtime environment failed with error 'The java executable in the extracted Java runtime environment was expected to be at '{Path.Combine(ShaPath, "javaPath")}' but couldn't be found.'.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_ChecksumInvalid()
    {
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd");
        runtime.File.Exists($@"{ShaPath}\filename.tar.gz").Returns(false);
        var downloadContentArray = new byte[] { 1, 2, 3 };
        var fileContentArray = new byte[3];
        var fileContentStream = new MemoryStream(fileContentArray, writable: true);
        runtime.File.Create(Path.Combine(ShaPath, "xFirst.rnd")).Returns(fileContentStream);
        using var content = new MemoryStream(downloadContentArray);

        var result = await ExecuteDownloadAndUnpack(content);

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be("The download of the file from the server failed with the exception "
            + "'The checksum of the downloaded file does not match the expected checksum.'.");
        runtime.File.Received(1).Create(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.DidNotReceive().Move(Path.Combine(ShaPath, "xFirst.rnd"), Path.Combine(ShaPath, "filename.tar.gz"));
        fileContentArray.Should().BeEquivalentTo(downloadContentArray);
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after success.");
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            "Starting the file download.",
            "The checksum of the downloaded file is '' and the expected checksum is 'sha256'.",
            $"Deleting file '{Path.Combine(ShaPath, "xFirst.rnd")}'.",
            "The download of the file from the server failed with the exception 'The checksum of the downloaded file does not match the expected checksum.'.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Success_WithTestFiles()
    {
        var home = Path.GetTempPath();
        var sha = Path.GetRandomFileName();
        var cache = Path.Combine(home, "cache");
        var jre = Path.Combine(cache, sha);
        var file = Path.Combine(jre, "filename.tar.gz");
        var runtimeIO = new TestRuntime // Do real I/O operations in this test and only fake the download.
        {
            Directory = DirectoryWrapper.Instance,
            File = FileWrapper.Instance
        };
        var targzUnpacker = new TarGzUnpacker(runtimeIO);
        var downloadContentArray = new byte[] { 1, 2, 3 };

        var sut = new JreDownloader(runtimeIO, targzUnpacker, ChecksumSha256.Instance, home, new JreDescriptor("filename.tar.gz", sha, "javaPath"));
        try
        {
            using var content = new MemoryStream(downloadContentArray);
            var result = await sut.DownloadJreAsync(() => Task.FromResult<Stream>(content));
            result.Should().BeOfType<DownloadError>().Which.Message.Should().Be("The download of the file from the server failed with the exception "
                + "'The checksum of the downloaded file does not match the expected checksum.'.");
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
        var cache = Path.Combine(home, "cache");
        var jre = Path.Combine(cache, sha);
        var file = Path.Combine(jre, "filename.tar.gz");
        var runtimeIO = new TestRuntime // Do real I/O operations in this test and only fake the download.
        {
            Directory = DirectoryWrapper.Instance,
            File = FileWrapper.Instance
        };
        var targzUnpacker = new TarGzUnpacker(runtimeIO);

        var sut = new JreDownloader(runtimeIO, targzUnpacker, ChecksumSha256.Instance, home, new JreDescriptor("filename.tar.gz", sha, "javaPath"));
        try
        {
            var result = await sut.DownloadJreAsync(() => Task.FromResult<Stream>(failingStream));

            result.Should().BeOfType<DownloadError>().Which.Message.Should().Be(
                @"The download of the file from the server failed with the exception 'Download failure simulation.'.");
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

    [TestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_DownloadFileNew_Failure_FileCreate(Type exceptionType)
    {
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd");
        runtime.File.Exists(Path.Combine(ShaPath, "filename.tar.gz")).Returns(false);
        var exception = (Exception)Activator.CreateInstance(exceptionType);
        runtime.File.Create(Path.Combine(ShaPath, "xFirst.rnd")).Throws(exception);

        var result = await ExecuteDownloadAndUnpack();

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be(
            $"The download of the file from the server failed with the exception '{exception.Message}'.");
        runtime.File.Received(1).Create(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.Received(1).Delete(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.DidNotReceive().Move(Path.Combine(ShaPath, "xFirst.rnd"), Path.Combine(ShaPath, "filename.tar.gz"));
    }

    [TestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_DownloadFileNew_Failure_TempFileDeleteOnFailure(Type exceptionType)
    {
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd");
        runtime.File.Exists($@"{ShaPath}\filename.tar.gz").Returns(false);
        runtime.File.Create(Path.Combine(ShaPath, "xFirst.rnd")).Returns(new MemoryStream());
        runtime.File.When(x => x.Delete(Path.Combine(ShaPath, "xFirst.rnd"))).Do(x => throw ((Exception)Activator.CreateInstance(exceptionType)));

        var result = await ExecuteDownloadAndUnpack(failingStream);
        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be("The download of the file from the server failed with the "
            + "exception 'Download failure simulation.'."); // The exception from the failed temp file delete is not visible to the user.
        runtime.File.Received(1).Create(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.Received(1).Delete(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.DidNotReceive().Move(Path.Combine(ShaPath, "xFirst.rnd"), Path.Combine(ShaPath, "filename.tar.gz"));
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Failure_TempFileStreamCloseOnFailure()
    {
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd");
        runtime.File.Exists($@"{ShaPath}\filename.tar.gz").Returns(false);
        var fileStream = Substitute.For<Stream>();
        fileStream.When(x => x.Close()).Throw(x => new ObjectDisposedException("stream"));
        runtime.File.Create(Path.Combine(ShaPath, "xFirst.rnd")).Returns(fileStream);

        var result = await ExecuteDownloadAndUnpack(failingStream);
        result.Should().BeOfType<DownloadError>().Which.Message.Replace(Environment.NewLine, string.Empty).Should().Be("""
            The download of the file from the server failed with the exception 'Cannot access a disposed object.Object name: 'stream'.'.
            """); // This should actually read "Download failure simulation." because the ObjectDisposedException is actually swallowed.
        result.Should().BeOfType<DownloadError>().Which.Message.Replace(Environment.NewLine, string.Empty).Should().Be("""
            The download of the file from the server failed with the exception 'Cannot access a disposed object.Object name: 'stream'.'.
            """); // This should actually read "Download failure simulation." because the ObjectDisposedException is actually swallowed.
                  // I assume this is either
                  // * a bug in NSubstitute, or
                  // * because of the way async stacks are handled (I tested with a dedicated project but it didn't reproduced there), or
                  // * maybe something like this: https://github.com/dotnet/roslyn/issues/72177
                  // This is such an corner case, that the misleading message isn't really a problem.
        runtime.File.Received(1).Create(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.Received(1).Delete(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.DidNotReceive().Move(Path.Combine(ShaPath, "xFirst.rnd"), Path.Combine(ShaPath, "filename.tar.gz"));
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Failure_Download()
    {
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd");
        runtime.File.Exists(Path.Combine(ShaPath, "filename.tar.gz")).Returns(false);
        var fileContentStream = new MemoryStream();
        runtime.File.Create(Path.Combine(ShaPath, "xFirst.rnd")).Returns(fileContentStream);

        var result = await ExecuteDownloadAndUnpack(failingStream);

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be("The download of the file from the server failed with the exception 'Download failure simulation.'.");
        runtime.File.Received(1).Create(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.Received(1).Delete(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.DidNotReceive().Move(Path.Combine(ShaPath, "xFirst.rnd"), Path.Combine(ShaPath, "filename.tar.gz"));
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after failure.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Download_NullStream()
    {
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd");
        runtime.File.Exists(Path.Combine(ShaPath, "filename.tar.gz")).Returns(false);
        var fileContentStream = new MemoryStream();
        runtime.File.Create(Path.Combine(ShaPath, "xFirst.rnd")).Returns(fileContentStream);
        var sut = CreateSutWithSubstitutes();

        var result = await sut.DownloadJreAsync(() => Task.FromResult<Stream>(null));

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be(
            "The download of the file from the server failed with the exception 'The download stream is null. The server likely returned an error status code.'.");
        runtime.File.Received(1).Create(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.Received(1).Delete(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.DidNotReceive().Move(Path.Combine(ShaPath, "xFirst.rnd"), Path.Combine(ShaPath, "filename.tar.gz"));
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after failure.");
    }

    [TestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_DownloadFileNew_Failure_Move(Type exceptionType)
    {
        var file = Path.Combine(ShaPath, "filename.tar.gz");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd");
        runtime.File.Exists(file).Returns(false);
        var fileContentArray = new byte[3];
        var fileContentStream = new MemoryStream(fileContentArray);
        var computeHashStream = new MemoryStream();
        runtime.File.Create(Path.Combine(ShaPath, "xFirst.rnd")).Returns(fileContentStream);
        runtime.File.Open(Path.Combine(ShaPath, "xFirst.rnd")).Returns(computeHashStream);
        var exception = (Exception)Activator.CreateInstance(exceptionType);
        runtime.File.When(x => x.Move(Path.Combine(ShaPath, "xFirst.rnd"), file)).Throw(exception);
        checksum.ComputeHash(computeHashStream).Returns("sha256");
        using var content = new MemoryStream([1, 2, 3]);

        var result = await ExecuteDownloadAndUnpack(content);

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be($"The download of the file from the server failed with the exception '{exception.Message}'.");
        runtime.File.Received(1).Create(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.Received(1).Move(Path.Combine(ShaPath, "xFirst.rnd"), file);
        runtime.File.Received(1).Delete(Path.Combine(ShaPath, "xFirst.rnd"));
        fileContentArray.Should().BeEquivalentTo([1, 2, 3]);
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after failure.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_DownloadFailed_But_FileExists()
    {
        var file = Path.Combine(ShaPath, "filename.tar.gz");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        // Before the download, the runtime.File.Exists returns false.
        // Then the download fails because e.g. another scanner created the file.
        // The second call to runtime.File.Exists returns true and therefore and we want to continue with provisioning.
        runtime.File.Exists(file).Returns(false, true);
        runtime.File.When(x => x.Create(Arg.Any<string>())).Throw<IOException>(); // Fail the download somehow.

        var result = await ExecuteDownloadAndUnpack();

        // The download failed, but we still progress with the provisioning because somehow magically the file is there anyway.
        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be("The checksum of the downloaded file does not match the expected checksum.");
        runtime.Logger.AssertDebugLogged(@"Starting the file download.");
        runtime.Logger.AssertDebugLogged(@"The download of the file from the server failed with the exception 'I/O error occurred.'.");
        runtime.Logger.AssertDebugLogged(@"The file was found after the download failed. Another scanner downloaded the file in parallel.");
    }

    [TestMethod]
    [DataRow("sha256", "sha256")]
    [DataRow("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [DataRow("b5dffd0be08c464d9c3903e2947508c1a5c21804ea1cff5556991a2a47d617d8", "B5DFFD0BE08C464D9C3903E2947508C1A5C21804EA1CFF5556991A2A47D617D8")]
    public async Task Checksum_DownloadFilesChecksumFitsExpectation(string fileHashValue, string expectedHashValue)
    {
        var sha = Path.Combine(SonarCache, expectedHashValue);
        var file = Path.Combine(sha, "filename.tar.gz");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(sha).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd");
        runtime.File.Exists(file).Returns(false);
        runtime.File.Create(Path.Combine(sha, "xFirst.rnd")).Returns(new MemoryStream()); // This is the temp file creation.
        var fileStream = new MemoryStream();
        runtime.File.Open(Path.Combine(sha, "xFirst.rnd")).Returns(fileStream);
        checksum.ComputeHash(fileStream).Returns(fileHashValue);

        var result = await ExecuteDownloadAndUnpack(descriptor: new JreDescriptor("filename.tar.gz", expectedHashValue, "javaPath"));

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            "Starting the file download.",
            $"The checksum of the downloaded file is '{fileHashValue}' and the expected checksum is '{expectedHashValue}'.",
            $"Starting extracting the Java runtime environment from archive '{Path.Combine(SonarUserHome, "cache", expectedHashValue, "filename.tar.gz")}' "
            + $"to folder '{Path.Combine(SonarUserHome, "cache", expectedHashValue, "xSecond.rnd")}'.",
            "The extraction of the downloaded Java runtime environment failed with error 'The java executable in the extracted Java runtime environment "
            + $"was expected to be at '{Path.Combine(SonarUserHome, "cache", expectedHashValue, "xSecond.rnd", "javaPath")}' but couldn't be found.'.");
        runtime.File.Received(1).Exists(file);
        runtime.File.Received(1).Create(Path.Combine(sha, "xFirst.rnd"));
        runtime.File.Received(1).Move(Path.Combine(sha, "xFirst.rnd"), file);
        runtime.File.Received(1).Open(Path.Combine(sha, "xFirst.rnd")); // For the checksum.
        runtime.File.Received(1).Open(file); // For the unpacking.
        checksum.Received(1).ComputeHash(fileStream);
    }

    [TestMethod]
    [DataRow("fileHash", "expectedHash")]
    [DataRow("e3b0c ", "e3b0c")]
    [DataRow("e3b0c", "e3b0c ")]
    [DataRow("e3b0c", "")]
    [DataRow("", "e3b0c")]
    public async Task Checksum_DownloadFilesChecksumDoesNotMatchExpectation(string fileHashValue, string expectedHashValue)
    {
        var sha = Path.Combine(SonarCache, expectedHashValue);
        var file = Path.Combine(sha, "filename.tar.gz");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(sha).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd");
        runtime.File.Exists(file).Returns(false);
        runtime.File.Create(Path.Combine(sha, "xFirst.rnd")).Returns(new MemoryStream());
        runtime.File.When(x => x.Delete(Path.Combine(sha, "xFirst.rnd"))).Do(x => throw new FileNotFoundException());
        var fileStream = new MemoryStream();
        runtime.File.Open(Path.Combine(sha, "xFirst.rnd")).Returns(fileStream);
        checksum.ComputeHash(fileStream).Returns(fileHashValue);

        var result = await ExecuteDownloadAndUnpack(descriptor: new JreDescriptor("filename.tar.gz", expectedHashValue, "javaPath"));

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be("The download of the file from the server failed with the exception "
            + "'The checksum of the downloaded file does not match the expected checksum.'.");
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            "Starting the file download.",
            $"The checksum of the downloaded file is '{fileHashValue}' and the expected checksum is '{expectedHashValue}'.",
            @$"Deleting file '{Path.Combine(SonarCache, expectedHashValue, "xFirst.rnd")}'.",
            @$"Failed to delete file '{Path.Combine(SonarCache, expectedHashValue, "xFirst.rnd")}'. Unable to find the specified file.",
            "The download of the file from the server failed with the exception 'The checksum of the downloaded file does not match the expected checksum.'.");
        runtime.File.Received(2).Exists(file); // One before the download and one after the failed download.
        runtime.File.Received(1).Create(Path.Combine(sha, "xFirst.rnd"));
        runtime.File.Received(1).Open(Path.Combine(sha, "xFirst.rnd"));
        runtime.File.Received(1).Delete(Path.Combine(sha, "xFirst.rnd"));
        checksum.Received(1).ComputeHash(fileStream);
    }

    [TestMethod]
    public async Task Checksum_DownloadFile_ComputationFails()
    {
        var file = Path.Combine(ShaPath, "filename.tar.gz");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd");
        runtime.File.Exists(file).Returns(false);
        runtime.File.Create(Path.Combine(ShaPath, "xFirst.rnd")).Returns(new MemoryStream());
        var fileStream = new MemoryStream();
        runtime.File.Open(Path.Combine(ShaPath, "xFirst.rnd")).Returns(fileStream);
        checksum.ComputeHash(fileStream).Throws<InvalidOperationException>();

        var result = await ExecuteDownloadAndUnpack();

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be("The download of the file from the server failed with the exception "
            + "'The checksum of the downloaded file does not match the expected checksum.'.");
        runtime.Logger.AssertDebugLogged($"The calculation of the checksum of the file '{Path.Combine(ShaPath, "xFirst.rnd")}' failed with message "
            + "'Operation is not valid due to the current state of the object.'.");
        runtime.File.Received(2).Exists(file); // One before the download and one after the failed download.
        runtime.File.Received(1).Create(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.DidNotReceive().Move(Path.Combine(ShaPath, "xFirst.rnd"), file);
        runtime.File.DidNotReceive().Open(file);
        checksum.Received(1).ComputeHash(fileStream);
    }

    [TestMethod]
    public async Task Checksum_DownloadFile_FileOpenFails()
    {
        var file = Path.Combine(ShaPath, "filename.tar.gz");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd");
        runtime.File.Exists(file).Returns(false);
        runtime.File.Create(Path.Combine(ShaPath, "xFirst.rnd")).Returns(new MemoryStream());
        runtime.File.Open(Path.Combine(ShaPath, "xFirst.rnd")).Throws<IOException>();

        var result = await ExecuteDownloadAndUnpack();

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be("The download of the file from the server failed with the exception "
            + "'The checksum of the downloaded file does not match the expected checksum.'.");
        runtime.Logger.AssertDebugLogged(@$"The calculation of the checksum of the file '{Path.Combine(ShaPath, "xFirst.rnd")}' failed with message 'I/O error occurred.'.");
        runtime.File.Received(2).Exists(file); // One before the download and one after the failed download.
        runtime.File.Received(1).Create(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.Received(1).Open(Path.Combine(ShaPath, "xFirst.rnd"));
        runtime.File.DidNotReceive().Open(file);
        checksum.DidNotReceive().ComputeHash(Arg.Any<Stream>());
    }

    [TestMethod]
    public async Task UnpackerFactory_Success()
    {
        var file = Path.Combine(ShaPath, "filename.tar.gz");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd");
        runtime.File.Exists(file).Returns(false);
        runtime.File.Create(Arg.Any<string>()).Returns(new MemoryStream());
        checksum.ComputeHash(Arg.Any<Stream>()).Returns("sha256");

        var result = await ExecuteDownloadAndUnpack();

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be(@"The downloaded Java runtime environment could not be extracted.");
        runtime.File.Received(1).Exists(file);
        runtime.File.Received(1).Create(Arg.Any<string>());
        runtime.File.Received(1).Open(file); // For the unpacking.
        checksum.Received(1).ComputeHash(Arg.Any<Stream>());
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the file download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            // The unpackerFactory returned an unpacker and it was called. But the test setup is incomplete and therefore fails later:
            @$"Starting extracting the Java runtime environment from archive '{file}' to folder '{Path.Combine(ShaPath, "xSecond.rnd")}'.",
            @$"The extraction of the downloaded Java runtime environment failed with error 'The java executable in the extracted Java runtime environment was expected to be at '{Path.Combine(ShaPath, "xSecond.rnd", "javaPath")}' but couldn't be found.'.");
    }

    [TestMethod]
    public async Task Unpack_Success()
    {
        var file = Path.Combine(ShaPath, "filename.tar.gz");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd"); // First for the download file, second for the extraction temp folder.
        runtime.File.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var tempFileStream = new MemoryStream();
        runtime.File.Open(Path.Combine(ShaPath, "xFirst.rnd")).Returns(tempFileStream);
        var archiveFileStream = new MemoryStream();
        runtime.File.Open(file).Returns(archiveFileStream);
        checksum.ComputeHash(tempFileStream).Returns("sha256");
        runtime.File.Exists(Path.Combine(ShaPath, "xSecond.rnd", "javaPath")).Returns(true);

        var result = await ExecuteDownloadAndUnpack();

        result.Should().BeOfType<DownloadSuccess>().Which.FilePath.Should().Be(Path.Combine(file + "_extracted", "javaPath"));
        runtime.Directory.Received(2).GetRandomFileName();
        runtime.Directory.Received(1).Move(Path.Combine(ShaPath, "xSecond.rnd"), file + "_extracted");
        unpacker.Received(1).Unpack(archiveFileStream, Path.Combine(ShaPath, "xSecond.rnd"));
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the file download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive '{file}' to folder '{Path.Combine(ShaPath, "xSecond.rnd")}'.",
            @$"Moving extracted Java runtime environment from '{Path.Combine(ShaPath, "xSecond.rnd")}' to '{file}_extracted'.",
            $"The Java runtime environment was successfully added to '{file}_extracted'.");
    }

    [TestMethod]
    public async Task Unpack_Failure_Unpack()
    {
        var file = Path.Combine(ShaPath, "filename.tar.gz");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd"); // First for the download file, second for the extraction temp folder.
        runtime.File.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var tempFileStream = new MemoryStream();
        runtime.File.Open(Path.Combine(ShaPath, "xFirst.rnd")).Returns(tempFileStream);
        var archiveFileStream = new MemoryStream();
        runtime.File.Open(file).Returns(archiveFileStream);
        checksum.ComputeHash(tempFileStream).Returns("sha256");
        unpacker.When(x => x.Unpack(archiveFileStream, Path.Combine(ShaPath, "xSecond.rnd"))).Throw(new IOException("Unpack failure"));

        var result = await ExecuteDownloadAndUnpack();

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        runtime.Directory.Received(2).GetRandomFileName();
        runtime.Directory.DidNotReceiveWithAnyArgs().Move(null, null);
        runtime.Directory.Received(1).Delete(Path.Combine(ShaPath, "xSecond.rnd"), true);
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            "Starting the file download.",
            "The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            $"Starting extracting the Java runtime environment from archive '{file}' to folder '{Path.Combine(ShaPath, "xSecond.rnd")}'.",
            "The extraction of the downloaded Java runtime environment failed with error 'Unpack failure'.");
    }

    [TestMethod]
    public async Task Unpack_Failure_Move()
    {
        var file = Path.Combine(ShaPath, "filename.tar.gz");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd"); // First for the download file, second for the extraction temp folder.
        runtime.File.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var tempFileStream = new MemoryStream();
        runtime.File.Open(Path.Combine(ShaPath, "xFirst.rnd")).Returns(tempFileStream);
        var archiveFileStream = new MemoryStream();
        runtime.File.Open(file).Returns(archiveFileStream);
        checksum.ComputeHash(tempFileStream).Returns("sha256");
        runtime.File.Exists(Path.Combine(ShaPath, "xSecond.rnd", "javaPath")).Returns(true);
        runtime.Directory.When(x => x.Move(Path.Combine(ShaPath, "xSecond.rnd"), file + "_extracted")).Throw<IOException>();

        var result = await ExecuteDownloadAndUnpack();

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        runtime.Directory.Received(2).GetRandomFileName();
        runtime.Directory.Received(1).Move(Path.Combine(ShaPath, "xSecond.rnd"), file + "_extracted");
        runtime.Directory.Received(1).Delete(Path.Combine(ShaPath, "xSecond.rnd"), true);
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the file download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive '{file}' to folder '{Path.Combine(ShaPath, "xSecond.rnd")}'.",
            @$"Moving extracted Java runtime environment from '{Path.Combine(ShaPath, "xSecond.rnd")}' to '{file}_extracted'.",
            @"The extraction of the downloaded Java runtime environment failed with error 'I/O error occurred.'.");
    }

    [TestMethod]
    public async Task Unpack_Failure_JavaExeNotFound()
    {
        var file = Path.Combine(ShaPath, "filename.tar.gz");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd"); // First for the download file, second for the extraction temp folder.
        runtime.File.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var tempFileStream = new MemoryStream();
        runtime.File.Open(Path.Combine(ShaPath, "xFirst.rnd")).Returns(tempFileStream);
        var archiveFileStream = new MemoryStream();
        runtime.File.Open(file).Returns(archiveFileStream);
        checksum.ComputeHash(tempFileStream).Returns("sha256");
        runtime.File.Exists(Path.Combine("sha", "xSecond.rnd", "javaPath")).Returns(false);

        var result = await ExecuteDownloadAndUnpack();

        result.Should().BeOfType<DownloadError>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        runtime.Directory.Received(2).GetRandomFileName();
        runtime.File.Received(1).Exists(Path.Combine(ShaPath, "xSecond.rnd", "javaPath"));
        runtime.Directory.Received(1).Delete(Path.Combine(ShaPath, "xSecond.rnd"), true);
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the file download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive '{file}' to folder '{Path.Combine(ShaPath, "xSecond.rnd")}'.",
            @$"The extraction of the downloaded Java runtime environment failed with error 'The java executable in the extracted Java runtime environment was expected to be at '{Path.Combine(ShaPath, "xSecond.rnd", "javaPath")}' but couldn't be found.'.");
    }

    [TestMethod]
    public async Task Unpack_Failure_ErrorInCleanUpOfTempDirectory()
    {
        var file = Path.Combine(ShaPath, "filename.tar.gz");
        runtime.Directory.Exists(SonarCache).Returns(true);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd"); // First for the download file, second for the extraction temp folder.
        runtime.File.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var tempFileStream = new MemoryStream();
        runtime.File.Open(Path.Combine(ShaPath, "xFirst.rnd")).Returns(tempFileStream);
        runtime.File.Open(file).Returns(new MemoryStream());
        checksum.ComputeHash(tempFileStream).Returns("sha256");
        runtime.File.Exists(Path.Combine(ShaPath, "xSecond.rnd", "javaPath")).Returns(true);
        runtime.Directory.When(x => x.Move(Path.Combine(ShaPath, "xSecond.rnd"), file + "_extracted")).Throw(new IOException("Move failure"));
        runtime.Directory.When(x => x.Delete(Path.Combine(ShaPath, "xSecond.rnd"), true)).Throw(new IOException("Folder cleanup failure"));

        await ExecuteDownloadAndUnpack();

        runtime.Directory.Received(2).GetRandomFileName();
        runtime.Directory.Received(1).Move(Path.Combine(ShaPath, "xSecond.rnd"), file + "_extracted");
        runtime.Directory.Received(1).Delete(Path.Combine(ShaPath, "xSecond.rnd"), true);
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the file download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive '{file}' to folder '{Path.Combine(ShaPath, "xSecond.rnd")}'.",
            @$"Moving extracted Java runtime environment from '{Path.Combine(ShaPath, "xSecond.rnd")}' to '{file}_extracted'.",
            @"The extraction of the downloaded Java runtime environment failed with error 'Move failure'.",
            @$"The cleanup of the temporary folder for the Java runtime environment extraction at '{Path.Combine(ShaPath, "xSecond.rnd")}' failed with message 'Folder cleanup failure'.");
    }

    [TestMethod]
    public async Task EndToEndTestWithFiles_Zip_Success()
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

        var runtimeIO = new TestRuntime // Do real I/O operations in this test and only fake the download.
        {
            Directory = DirectoryWrapper.Instance,
            File = FileWrapper.Instance
        };
        var sut = new JreDownloader(runtimeIO, new ZipUnpacker(), ChecksumSha256.Instance, home, jreDescriptor);

        try
        {
            var result = await sut.DownloadJreAsync(() => Task.FromResult<Stream>(new MemoryStream(zipContent)));

            result.Should().BeOfType<DownloadSuccess>().Which.FilePath.Should().Be(
                Path.Combine(cache, sha, "OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.zip_extracted", "jdk-17.0.11+9-jre/bin/java.exe"));
            Directory.EnumerateFileSystemEntries(cache, "*", SearchOption.AllDirectories).Should().BeEquivalentTo(
                Path.Combine(cache, sha),
                Path.Combine(cache, sha, file),
                Path.Combine(cache, sha, $"{file}_extracted"),
                Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre"),
                Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre", "bin"),
                Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre", "bin", "java.exe"));
            File.ReadAllText(Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre", "bin", "java.exe")).Should().Be(
                "This is just a sample file for testing and not the real java.exe");
            runtimeIO.Logger.AssertSingleInfoMessageExists("""
                The JRE provisioning is a time consuming operation.
                JRE provisioned: OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.zip.
                If you already have a compatible Java version installed, please add either the parameter "/d:sonar.scanner.skipJreProvisioning=true" or "/d:sonar.scanner.javaExePath=<PATH>".
                """);
            runtimeIO.Logger.DebugMessages.Should().SatisfyRespectively(
                x => x.Should().Be("Starting the file download."),
                x => x.Should().Be($"The checksum of the downloaded file is '{sha}' and the expected checksum is '{sha}'."),
                x => x.Should().Match($"Starting extracting the Java runtime environment from archive '{Path.Combine(cache, sha, file)}' to folder '{Path.Combine(cache, sha, "*")}'."),
                x => x.Should().Match($"Moving extracted Java runtime environment from '{Path.Combine(cache, sha, "*")}' to '{Path.Combine(cache, sha, "OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.zip_extracted")}'."),
                x => x.Should().Be($"The Java runtime environment was successfully added to '{Path.Combine(cache, sha, "OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.zip_extracted")}'."));
        }
        finally
        {
            Directory.Delete(home, true);
        }
    }

    [TestMethod]
    public async Task EndToEndTestWithFiles_TarGz_Success()
    {
        // A tarball with a file named java.exe in the jdk-17.0.11+9-jre\bin folder.
        const string jreTarBall = """
            H4sICLHekGYEAGpkay0xNy4wLjExKzktanJlLnRhcgDt0kEKAiEUgGGP8vbR5HM06
            R5dwBhrtMkJtej4OUFQFEQgbsYPUReu/J/tjkuUDW0QF5ul9XpFsqOJlDKdD2/n84
            58zTkKyRAJRcoEIyBIAZcQlQcgM/XZf2dc5hn4qz+b+qcBaGv/Er73t+qqGn3TJIt
            f/fG1f5veIeOCEqCkgJn33/YmQFo2/QMoCOp0HjTszbSNHqIO0bgDKNeBGyPEXoPX
            aoC8E1JVVVWVdgcIF31QAAwAAA==
            """;
        var tarContent = Convert.FromBase64String(jreTarBall);
        var home = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var cache = Path.Combine(home, "cache");
        var sha = "347f62ce8b0aadffd19736a189b4b79fad87a83cc36ec1273081629c9cb06d3b";
        var file = "OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.tar.gz";
        var jreDescriptor = new JreDescriptor(file, sha, Path.Combine("jdk-17.0.11+9-jre", "bin", "java.exe"));
        var runtimeIO = new TestRuntime // Do real I/O operations in this test and only fake the download.
        {
            Directory = DirectoryWrapper.Instance,
            File = FileWrapper.Instance
        };
        var targzUnpacker = new TarGzUnpacker(runtimeIO);

        var sut = new JreDownloader(runtimeIO, targzUnpacker, ChecksumSha256.Instance, home, jreDescriptor);

        try
        {
            var result = await sut.DownloadJreAsync(() => Task.FromResult<Stream>(new MemoryStream(tarContent)));

            result.Should().BeOfType<DownloadSuccess>().Which.FilePath.Should().Be(
                Path.Combine(cache, sha, "OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.tar.gz_extracted", "jdk-17.0.11+9-jre", "bin", "java.exe"));
            Directory.EnumerateFileSystemEntries(cache, "*", SearchOption.AllDirectories).Should().BeEquivalentTo(
                Path.Combine(cache, sha),
                Path.Combine(cache, sha, file),
                Path.Combine(cache, sha, $"{file}_extracted"),
                Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre"),
                Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre", "bin"),
                Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre", "bin", "java.exe"));
            File.ReadAllText(Path.Combine(cache, sha, $"{file}_extracted", "jdk-17.0.11+9-jre", "bin", "java.exe")).Should().Be(
                "This is just a sample file for testing and not the real java.exe");
            runtimeIO.Logger.AssertSingleInfoMessageExists("""
                The JRE provisioning is a time consuming operation.
                JRE provisioned: OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.tar.gz.
                If you already have a compatible Java version installed, please add either the parameter "/d:sonar.scanner.skipJreProvisioning=true" or "/d:sonar.scanner.javaExePath=<PATH>".
                """);
            runtimeIO.Logger.DebugMessages.Should().SatisfyRespectively(
                x => x.Should().Be("Starting the file download."),
                x => x.Should().Be($"The checksum of the downloaded file is '{sha}' and the expected checksum is '{sha}'."),
                x => x.Should().Match($"Starting extracting the Java runtime environment from archive '{Path.Combine(cache, sha, "OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.tar.gz")}' to folder '{Path.Combine(cache, sha, "*")}'."),
                x => x.Should().Match($"Moving extracted Java runtime environment from '{Path.Combine(cache, sha, "*")}' to '{Path.Combine(cache, sha, "OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.tar.gz_extracted")}'."),
                x => x.Should().Be($"The Java runtime environment was successfully added to '{Path.Combine(cache, sha, "OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.tar.gz_extracted")}'."));
        }
        finally
        {
            Directory.Delete(home, true);
        }
    }

    private async Task<DownloadResult> ExecuteDownloadAndUnpack(MemoryStream content = null, JreDescriptor descriptor = null)
    {
        var jreDescriptor = descriptor ?? new JreDescriptor("filename.tar.gz", "sha256", "javaPath");
        var sut = CreateSutWithSubstitutes(jreDescriptor);
        var memoryStream = content ?? new MemoryStream();
        return await sut.DownloadJreAsync(() => Task.FromResult<Stream>(memoryStream));
    }

    private JreDownloader CreateSutWithSubstitutes(JreDescriptor jreDescriptor = null)
    {
        jreDescriptor ??= new JreDescriptor("filename.tar.gz", "sha256", "javaPath");
        return new JreDownloader(runtime, unpacker, checksum, SonarUserHome, jreDescriptor);
    }
}
