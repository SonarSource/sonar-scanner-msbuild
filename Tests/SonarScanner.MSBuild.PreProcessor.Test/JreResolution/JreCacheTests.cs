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
using NSubstitute.ReturnsExtensions;
using SonarScanner.MSBuild.PreProcessor.Caching;
using SonarScanner.MSBuild.PreProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor.JreResolution;
using SonarScanner.MSBuild.PreProcessor.Unpacking;

namespace SonarScanner.MSBuild.PreProcessor.Test.JreResolution;

[TestClass]
public class JreCacheTests
{
    private readonly TestLogger testLogger;
    private readonly IDirectoryWrapper directoryWrapper;
    private readonly IFileWrapper fileWrapper;
    private readonly IFileCache fileCache;
    private readonly IChecksum checksum;
    private readonly IUnpacker unpacker;
    private readonly IUnpackerFactory unpackerFactory;
    private readonly IFilePermissionsWrapper filePermissionsWrapper;

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
        fileCache = Substitute.For<FileCache>(directoryWrapper, fileWrapper);
        checksum = Substitute.For<IChecksum>();
        unpacker = Substitute.For<IUnpacker>();
        unpackerFactory = Substitute.For<IUnpackerFactory>();
        filePermissionsWrapper = Substitute.For<IFilePermissionsWrapper>();
        unpackerFactory.Create(testLogger, directoryWrapper, fileWrapper, filePermissionsWrapper, "filename.tar.gz").Returns(unpacker);
    }

    [TestMethod]
    public void CacheDirectoryIsCreated()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        directoryWrapper.Exists(cache).Returns(false);
        var sut = CreateSutWithSubstitutes();
        var result = sut.IsJreCached(home, new("jre", "sha", "java"));
        result.Should().Be(new CacheMiss());
        directoryWrapper.Received(1).CreateDirectory(cache);
    }

    [TestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public void CacheHomeCreationFails(Type exceptionType)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        directoryWrapper.When(x => x.CreateDirectory(cache)).Throw((Exception)Activator.CreateInstance(exceptionType));

        var sut = CreateSutWithSubstitutes();
        var result = sut.IsJreCached(home, new("jre", "sha", "java"));
        result.Should().Be(new CacheFailure($"The file cache directory in '{cache}' could not be created."));
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
        var result = sut.IsJreCached(home, new("filename.tar.gz", "sha", "jdk/bin/java"));
        result.Should().Be(new CacheMiss());
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
        var result = sut.IsJreCached(home, new("filename.tar.gz", "sha", "jdk/bin/java"));
        result.Should().Be(new CacheFailure(
            $"The java executable in the Java runtime environment cache could not be found at the expected location '{Path.Combine(expectedExtractedPath, "jdk/bin/java")}'."));
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
        var result = sut.IsJreCached(home, new("filename.tar.gz", "sha", "jdk/bin/java"));
        result.Should().Be(new CacheHit(expectedExtractedJavaExe));
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_CacheDirectoryCanNotBeCreated(Type exception)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(false);
        directoryWrapper.When(x => x.CreateDirectory(cache)).Throw((Exception)Activator.CreateInstance(exception));

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be($"The file cache directory in '{sha}' could not be created.");
    }

    [TestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_ShaDirectoryCanNotBeCreated(Type exception)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.When(x => x.CreateDirectory(sha)).Throw((Exception)Activator.CreateInstance(exception));

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be($"The file cache directory in '{sha}' could not be created.");
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public async Task Download_DownloadFileExists_ChecksumInvalid()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(true);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");
        testLogger.DebugMessages.Should().BeEquivalentTo(
            $"The Java Runtime Environment was already downloaded from the server and stored at '{Path.Combine(sha, "filename.tar.gz")}'.",
            "The checksum of the downloaded file is '' and the expected checksum is 'sha256'.",
            $"Deleting file '{Path.Combine(sha, "filename.tar.gz")}'.");
    }

    [TestMethod]
    public async Task Download_DownloadFileExists_ChecksumValid()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        fileWrapper.Exists(Path.Combine(sha, "filename.tar.gz")).Returns(true);
        var fileContent = new MemoryStream();
        fileWrapper.Open(Path.Combine(sha, "filename.tar.gz")).Returns(fileContent);
        checksum.ComputeHash(fileContent).Returns("sha256");
        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        testLogger.DebugMessages.Should().BeEquivalentTo(
            $"The Java Runtime Environment was already downloaded from the server and stored at '{Path.Combine(sha, "filename.tar.gz")}'.",
            "The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            $"Starting extracting the Java runtime environment from archive '{Path.Combine(sha, "filename.tar.gz")}' to folder '{sha}'.",
            $"The extraction of the downloaded Java runtime environment failed with error 'The java executable in the extracted Java runtime environment was expected to be at '{Path.Combine(sha, "javaPath")}' but couldn't be found.'.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Success()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd");
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        var downloadContentArray = new byte[] { 1, 2, 3 };
        var fileContentArray = new byte[3];
        var fileContentStream = new MemoryStream(fileContentArray, writable: true);
        fileWrapper.Create(Path.Combine(sha, "xFirst.rnd")).Returns(fileContentStream);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream(downloadContentArray)));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the exception "
            + "'The checksum of the downloaded Java runtime environment does not match the expected checksum.'.");
        fileWrapper.Received(1).Create(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.DidNotReceive().Move(Path.Combine(sha, "xFirst.rnd"), Path.Combine(sha, "filename.tar.gz"));
        fileContentArray.Should().BeEquivalentTo(downloadContentArray);
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after success.");
        testLogger.DebugMessages.Should().BeEquivalentTo(
            "Starting the Java Runtime Environment download.",
            "The checksum of the downloaded file is '' and the expected checksum is 'sha256'.",
            $"Deleting file '{Path.Combine(sha, "xFirst.rnd")}'.",
            "The download of the Java runtime environment from the server failed with the exception 'The checksum of the downloaded Java runtime environment does not match the expected checksum.'.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Success_WithTestFiles()
    {
        var home = Path.GetTempPath();
        var sha = Path.GetRandomFileName();
        var cache = Path.Combine(home, "cache");
        var jre = Path.Combine(cache, sha);
        var file = Path.Combine(jre, "filename.tar.gz");
        var directoryWrapperIO = DirectoryWrapper.Instance; // Do real I/O operations in this test and only fake the download.
        var fileWrapperIO = FileWrapper.Instance;
        var fileCache = new FileCache(directoryWrapperIO, fileWrapperIO);
        var downloadContentArray = new byte[] { 1, 2, 3 };

        var sut = new JreCache(testLogger, fileCache, directoryWrapperIO, fileWrapperIO, checksum, unpackerFactory, filePermissionsWrapper);
        try
        {
            var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", sha, "javaPath"), () => Task.FromResult<Stream>(new MemoryStream(downloadContentArray)));
            result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the exception "
                + "'The checksum of the downloaded Java runtime environment does not match the expected checksum.'.");
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
        var directoryWrapperIO = DirectoryWrapper.Instance; // Do real I/O operations in this test and only fake the download.
        var fileWrapperIO = FileWrapper.Instance;
        var fileCache = new FileCache(directoryWrapperIO, fileWrapperIO);

        var sut = new JreCache(testLogger, fileCache, directoryWrapperIO, fileWrapperIO, checksum, unpackerFactory, filePermissionsWrapper);
        try
        {
            var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", sha, "javaPath"), () => throw new InvalidOperationException("Download failure simulation."));
            result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be(
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

    [TestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_DownloadFileNew_Failure_FileCreate(Type exceptionType)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd");
        fileWrapper.Exists(Path.Combine(sha, "filename.tar.gz")).Returns(false);
        var exception = (Exception)Activator.CreateInstance(exceptionType);
        fileWrapper.Create(Path.Combine(sha, "xFirst.rnd")).Throws(exception);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be(
            $"The download of the Java runtime environment from the server failed with the exception '{exception.Message}'.");
        fileWrapper.Received(1).Create(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.DidNotReceive().Move(Path.Combine(sha, "xFirst.rnd"), Path.Combine(sha, "filename.tar.gz"));
        fileWrapper.DidNotReceive().Delete(Path.Combine(sha, "xFirst.rnd"));
    }

    [TestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_DownloadFileNew_Failure_TempFileDeleteOnFailure(Type exceptionType)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd");
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        fileWrapper.Create(Path.Combine(sha, "xFirst.rnd")).Returns(new MemoryStream());
        fileWrapper.When(x => x.Delete(Path.Combine(sha, "xFirst.rnd"))).Do(x => throw ((Exception)Activator.CreateInstance(exceptionType)));

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new InvalidOperationException("Download failure simulation."));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the "
            + "exception 'Download failure simulation.'."); // The exception from the failed temp file delete is not visible to the user.
        fileWrapper.Received(1).Create(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.Received(1).Delete(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.DidNotReceive().Move(Path.Combine(sha, "xFirst.rnd"), Path.Combine(sha, "filename.tar.gz"));
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Failure_TempFileStreamCloseOnFailure()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd");
        fileWrapper.Exists($@"{sha}\filename.tar.gz").Returns(false);
        var fileStream = Substitute.For<Stream>();
        fileStream.When(x => x.Close()).Throw(x => new ObjectDisposedException("stream"));
        fileWrapper.Create(Path.Combine(sha, "xFirst.rnd")).Returns(fileStream);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new InvalidOperationException("Download failure simulation."));
        result.Should().BeOfType<CacheFailure>().Which.Message.Replace(Environment.NewLine, string.Empty).Should().Be("""
            The download of the Java runtime environment from the server failed with the exception 'Cannot access a disposed object.Object name: 'stream'.'.
            """); // This should actually read "Download failure simulation." because the ObjectDisposedException is actually swallowed.
        result.Should().BeOfType<CacheFailure>().Which.Message.Replace(Environment.NewLine, string.Empty).Should().Be("""
            The download of the Java runtime environment from the server failed with the exception 'Cannot access a disposed object.Object name: 'stream'.'.
            """); // This should actually read "Download failure simulation." because the ObjectDisposedException is actually swallowed.
                  // I assume this is either
                  // * a bug in NSubstitute, or
                  // * because of the way async stacks are handled (I tested with a dedicated project but it didn't reproduced there), or
                  // * maybe something like this: https://github.com/dotnet/roslyn/issues/72177
                  // This is such an corner case, that the misleading message isn't really a problem.
        fileWrapper.Received(1).Create(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.Received(1).Delete(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.DidNotReceive().Move(Path.Combine(sha, "xFirst.rnd"), Path.Combine(sha, "filename.tar.gz"));
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Failure_Download()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd");
        fileWrapper.Exists(Path.Combine(sha, "filename.tar.gz")).Returns(false);
        var fileContentStream = new MemoryStream();
        fileWrapper.Create(Path.Combine(sha, "xFirst.rnd")).Returns(fileContentStream);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new InvalidOperationException("Download failure simulation."));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the exception 'Download failure simulation.'.");
        fileWrapper.Received(1).Create(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.Received(1).Delete(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.DidNotReceive().Move(Path.Combine(sha, "xFirst.rnd"), Path.Combine(sha, "filename.tar.gz"));
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after failure.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_Download_NullStream()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd");
        fileWrapper.Exists(Path.Combine(sha, "filename.tar.gz")).Returns(false);
        var fileContentStream = new MemoryStream();
        fileWrapper.Create(Path.Combine(sha, "xFirst.rnd")).Returns(fileContentStream);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(null));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be(
            "The download of the Java runtime environment from the server failed with the exception 'The download stream is null. The server likely returned an error status code.'.");
        fileWrapper.Received(1).Create(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.Received(1).Delete(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.DidNotReceive().Move(Path.Combine(sha, "xFirst.rnd"), Path.Combine(sha, "filename.tar.gz"));
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after failure.");
    }

    [TestMethod]
    [DynamicData(nameof(DirectoryAndFileCreateAndMoveExceptions))]
    public async Task Download_DownloadFileNew_Failure_Move(Type exceptionType)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, "filename.tar.gz");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd");
        fileWrapper.Exists(file).Returns(false);
        var fileContentArray = new byte[3];
        var fileContentStream = new MemoryStream(fileContentArray);
        var computeHashStream = new MemoryStream();
        fileWrapper.Create(Path.Combine(sha, "xFirst.rnd")).Returns(fileContentStream);
        fileWrapper.Open(Path.Combine(sha, "xFirst.rnd")).Returns(computeHashStream);
        var exception = (Exception)Activator.CreateInstance(exceptionType);
        fileWrapper.When(x => x.Move(Path.Combine(sha, "xFirst.rnd"), file)).Throw(exception);
        checksum.ComputeHash(computeHashStream).Returns("sha256");
        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be($"The download of the Java runtime environment from the server failed with the exception '{exception.Message}'.");
        fileWrapper.Received(1).Create(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.Received(1).Move(Path.Combine(sha, "xFirst.rnd"), file);
        fileWrapper.Received(1).Delete(Path.Combine(sha, "xFirst.rnd"));
        fileContentArray.Should().BeEquivalentTo([1, 2, 3]);
        var streamAccess = () => fileContentStream.Position;
        streamAccess.Should().Throw<ObjectDisposedException>("FileStream should be closed after failure.");
    }

    [TestMethod]
    public async Task Download_DownloadFileNew_DownloadFailed_But_FileExists()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, "filename.tar.gz");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        // Before the download, the fileWrapper.Exists returns false.
        // Then the download fails because e.g. another scanner created the file.
        // The second call to fileWrapper.Exists returns true and therefore and we want to continue with provisioning.
        fileWrapper.Exists(file).Returns(false, true);
        fileWrapper.When(x => x.Create(Arg.Any<string>())).Throw<IOException>(); // Fail the download somehow.

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => throw new NotSupportedException("Unreachable"));
        // The download failed, but we still progress with the provisioning because somehow magically the file is there anyway.
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The checksum of the downloaded Java runtime environment does not match the expected checksum.");
        testLogger.AssertDebugLogged(@"Starting the Java Runtime Environment download.");
        testLogger.AssertDebugLogged(@"The download of the Java runtime environment from the server failed with the exception 'I/O error occurred.'.");
        testLogger.AssertDebugLogged(@"The Java Runtime Environment archive was found after the download failed. Another scanner did the download in the parallel.");
    }

    [TestMethod]
    [DataRow("sha256", "sha256")]
    [DataRow("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [DataRow("b5dffd0be08c464d9c3903e2947508c1a5c21804ea1cff5556991a2a47d617d8", "B5DFFD0BE08C464D9C3903E2947508C1A5C21804EA1CFF5556991A2A47D617D8")]
    public async Task Checksum_DownloadFilesChecksumFitsExpectation(string fileHashValue, string expectedHashValue)
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, expectedHashValue);
        var file = Path.Combine(sha, "filename.tar.gz");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd");
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Path.Combine(sha, "xFirst.rnd")).Returns(new MemoryStream()); // This is the temp file creation.
        var fileStream = new MemoryStream();
        fileWrapper.Open(Path.Combine(sha, "xFirst.rnd")).Returns(fileStream);
        checksum.ComputeHash(fileStream).Returns(fileHashValue);

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", expectedHashValue, "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        testLogger.DebugMessages.Should().BeEquivalentTo(
            "Starting the Java Runtime Environment download.",
            $"The checksum of the downloaded file is '{fileHashValue}' and the expected checksum is '{expectedHashValue}'.",
            $"Starting extracting the Java runtime environment from archive '{Path.Combine(home, "cache", expectedHashValue, "filename.tar.gz")}' " +
                $"to folder '{Path.Combine(home, "cache", expectedHashValue, "xSecond.rnd")}'.",
            "The extraction of the downloaded Java runtime environment failed with error 'The java executable in the extracted Java runtime environment " +
                $"was expected to be at '{Path.Combine(home, "cache", expectedHashValue, "xSecond.rnd", "javaPath")}' but couldn't be found.'.");
        fileWrapper.Received(1).Exists(file);
        fileWrapper.Received(1).Create(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.Received(1).Move(Path.Combine(sha, "xFirst.rnd"), file);
        fileWrapper.Received(1).Open(Path.Combine(sha, "xFirst.rnd")); // For the checksum.
        fileWrapper.Received(1).Open(file); // For the unpacking.
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
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, expectedHashValue);
        var file = Path.Combine(sha, "filename.tar.gz");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd");
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Path.Combine(sha, "xFirst.rnd")).Returns(new MemoryStream());
        fileWrapper.When(x => x.Delete(Path.Combine(sha, "xFirst.rnd"))).Do(x => throw new FileNotFoundException());
        var fileStream = new MemoryStream();
        fileWrapper.Open(Path.Combine(sha, "xFirst.rnd")).Returns(fileStream);
        checksum.ComputeHash(fileStream).Returns(fileHashValue);
        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", expectedHashValue, "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the exception "
            + "'The checksum of the downloaded Java runtime environment does not match the expected checksum.'.");
        testLogger.DebugMessages.Should().BeEquivalentTo(
            "Starting the Java Runtime Environment download.",
            $"The checksum of the downloaded file is '{fileHashValue}' and the expected checksum is '{expectedHashValue}'.",
            @$"Deleting file '{Path.Combine(cache, expectedHashValue, "xFirst.rnd")}'.",
            @$"Failed to delete file '{Path.Combine(cache, expectedHashValue, "xFirst.rnd")}'. Unable to find the specified file.",
            "The download of the Java runtime environment from the server failed with the exception 'The checksum of the downloaded Java runtime environment does not match the expected checksum.'.");
        fileWrapper.Received(2).Exists(file); // One before the download and one after the failed download.
        fileWrapper.Received(1).Create(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.Received(1).Open(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.Received(1).Delete(Path.Combine(sha, "xFirst.rnd"));
        checksum.Received(1).ComputeHash(fileStream);
    }

    [TestMethod]
    public async Task Checksum_DownloadFile_ComputationFails()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, "filename.tar.gz");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd");
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Path.Combine(sha, "xFirst.rnd")).Returns(new MemoryStream());
        var fileStream = new MemoryStream();
        fileWrapper.Open(Path.Combine(sha, "xFirst.rnd")).Returns(fileStream);
        checksum.ComputeHash(fileStream).Throws<InvalidOperationException>();

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the exception "
            + "'The checksum of the downloaded Java runtime environment does not match the expected checksum.'.");
        testLogger.AssertDebugLogged($"The calculation of the checksum of the file '{Path.Combine(sha, "xFirst.rnd")}' failed with message "
            + "'Operation is not valid due to the current state of the object.'.");
        fileWrapper.Received(2).Exists(file); // One before the download and one after the failed download.
        fileWrapper.Received(1).Create(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.DidNotReceive().Move(Path.Combine(sha, "xFirst.rnd"), file);
        fileWrapper.DidNotReceive().Open(file);
        checksum.Received(1).ComputeHash(fileStream);
    }

    [TestMethod]
    public async Task Checksum_DownloadFile_FileOpenFails()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, "filename.tar.gz");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd");
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Path.Combine(sha, "xFirst.rnd")).Returns(new MemoryStream());
        fileWrapper.Open(Path.Combine(sha, "xFirst.rnd")).Throws<IOException>();

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The download of the Java runtime environment from the server failed with the exception "
            + "'The checksum of the downloaded Java runtime environment does not match the expected checksum.'.");
        testLogger.AssertDebugLogged(@$"The calculation of the checksum of the file '{Path.Combine(sha, "xFirst.rnd")}' failed with message 'I/O error occurred.'.");
        fileWrapper.Received(2).Exists(file); // One before the download and one after the failed download.
        fileWrapper.Received(1).Create(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.Received(1).Open(Path.Combine(sha, "xFirst.rnd"));
        fileWrapper.DidNotReceive().Open(file);
        checksum.DidNotReceive().ComputeHash(Arg.Any<Stream>());
    }

    [TestMethod]
    public async Task UnpackerFactory_Success()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, "filename.tar.gz");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd");
        fileWrapper.Exists(file).Returns(false);
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        checksum.ComputeHash(Arg.Any<Stream>()).Returns("sha256");
        unpackerFactory.Create(testLogger, directoryWrapper, fileWrapper, filePermissionsWrapper, "filename.tar.gz").Returns(Substitute.For<IUnpacker>());

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be(@"The downloaded Java runtime environment could not be extracted.");
        fileWrapper.Received(1).Exists(file);
        fileWrapper.Received(1).Create(Arg.Any<string>());
        fileWrapper.Received(1).Open(file); // For the unpacking.
        checksum.Received(1).ComputeHash(Arg.Any<Stream>());
        unpackerFactory.Received(1).Create(testLogger, directoryWrapper, fileWrapper, filePermissionsWrapper, "filename.tar.gz");
        testLogger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the Java Runtime Environment download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            // The unpackerFactory returned an unpacker and it was called. But the test setup is incomplete and therefore fails later:
            @$"Starting extracting the Java runtime environment from archive '{file}' to folder '{Path.Combine(sha, "xSecond.rnd")}'.",
            @$"The extraction of the downloaded Java runtime environment failed with error 'The java executable in the extracted Java runtime environment was expected to be at '{Path.Combine(sha, "xSecond.rnd", "javaPath")}' but couldn't be found.'.");
    }

    [TestMethod]
    public async Task UnpackerFactory_ReturnsNull()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        unpackerFactory.Create(testLogger, directoryWrapper, fileWrapper, filePermissionsWrapper, "filename.tar.gz").ReturnsNull();

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The archive format of the JRE archive `filename.tar.gz` is not supported.");
        fileWrapper.DidNotReceiveWithAnyArgs().Exists(null);
        fileWrapper.DidNotReceiveWithAnyArgs().Create(null);
        fileWrapper.DidNotReceiveWithAnyArgs().Open(null);
        checksum.DidNotReceiveWithAnyArgs().ComputeHash(null);
        unpackerFactory.Received(1).Create(testLogger, directoryWrapper, fileWrapper, filePermissionsWrapper, "filename.tar.gz");
        testLogger.DebugMessages.Should().BeEmpty();
    }

    [TestMethod]
    public async Task UnpackerFactory_UnsupportedFormat()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        unpackerFactory.Create(testLogger, directoryWrapper, fileWrapper, filePermissionsWrapper, "filename.tar.gz").ReturnsNull();

        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The archive format of the JRE archive `filename.tar.gz` is not supported.");
        fileWrapper.DidNotReceiveWithAnyArgs().Exists(null);
        fileWrapper.DidNotReceiveWithAnyArgs().Create(null);
        fileWrapper.DidNotReceiveWithAnyArgs().Open(null);
        checksum.DidNotReceiveWithAnyArgs().ComputeHash(null);
        unpackerFactory.Received(1).Create(testLogger, directoryWrapper, fileWrapper, filePermissionsWrapper, "filename.tar.gz");
    }

    [TestMethod]
    public async Task Unpack_Success()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, "filename.tar.gz");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd"); // First for the download file, second for the extraction temp folder.
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var tempFileStream = new MemoryStream();
        fileWrapper.Open(Path.Combine(sha, "xFirst.rnd")).Returns(tempFileStream);
        var archiveFileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(archiveFileStream);
        checksum.ComputeHash(tempFileStream).Returns("sha256");
        fileWrapper.Exists(Path.Combine(sha, "xSecond.rnd", "javaPath")).Returns(true);
        var sut = CreateSutWithSubstitutes();

        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<CacheHit>().Which.FilePath.Should().Be(Path.Combine(file + "_extracted", "javaPath"));
        directoryWrapper.Received(2).GetRandomFileName();
        directoryWrapper.Received(1).Move(Path.Combine(sha, "xSecond.rnd"), file + "_extracted");
        unpacker.Received(1).Unpack(archiveFileStream, Path.Combine(sha, "xSecond.rnd"));
        testLogger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the Java Runtime Environment download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive '{file}' to folder '{Path.Combine(sha, "xSecond.rnd")}'.",
            @$"Moving extracted Java runtime environment from '{Path.Combine(sha, "xSecond.rnd")}' to '{file}_extracted'.",
            $"The Java runtime environment was successfully added to '{file}_extracted'.");
    }

    [TestMethod]
    public async Task Unpack_Failure_Unpack()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, "filename.tar.gz");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd"); // First for the download file, second for the extraction temp folder.
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var tempFileStream = new MemoryStream();
        fileWrapper.Open(Path.Combine(sha, "xFirst.rnd")).Returns(tempFileStream);
        var archiveFileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(archiveFileStream);
        checksum.ComputeHash(tempFileStream).Returns("sha256");
        unpacker.When(x => x.Unpack(archiveFileStream, Path.Combine(sha, "xSecond.rnd"))).Throw(new IOException("Unpack failure"));
        var sut = CreateSutWithSubstitutes();

        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        directoryWrapper.Received(2).GetRandomFileName();
        directoryWrapper.DidNotReceiveWithAnyArgs().Move(null, null);
        directoryWrapper.Received(1).Delete(Path.Combine(sha, "xSecond.rnd"), true);
        testLogger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the Java Runtime Environment download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive '{file}' to folder '{Path.Combine(sha, "xSecond.rnd")}'.",
            @$"The extraction of the downloaded Java runtime environment failed with error 'Unpack failure'.");
    }

    [TestMethod]
    public async Task Unpack_Failure_Move()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, "filename.tar.gz");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd"); // First for the download file, second for the extraction temp folder.
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var tempFileStream = new MemoryStream();
        fileWrapper.Open(Path.Combine(sha, "xFirst.rnd")).Returns(tempFileStream);
        var archiveFileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(archiveFileStream);
        checksum.ComputeHash(tempFileStream).Returns("sha256");
        fileWrapper.Exists(Path.Combine(sha, "xSecond.rnd", "javaPath")).Returns(true);
        directoryWrapper.When(x => x.Move(Path.Combine(sha, "xSecond.rnd"), file + "_extracted")).Throw<IOException>();
        var sut = CreateSutWithSubstitutes();

        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        directoryWrapper.Received(2).GetRandomFileName();
        directoryWrapper.Received(1).Move(Path.Combine(sha, "xSecond.rnd"), file + "_extracted");
        directoryWrapper.Received(1).Delete(Path.Combine(sha, "xSecond.rnd"), true);
        testLogger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the Java Runtime Environment download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive '{file}' to folder '{Path.Combine(sha, "xSecond.rnd")}'.",
            @$"Moving extracted Java runtime environment from '{Path.Combine(sha, "xSecond.rnd")}' to '{file}_extracted'.",
            @"The extraction of the downloaded Java runtime environment failed with error 'I/O error occurred.'.");
    }

    [TestMethod]
    public async Task Unpack_Failure_JavaExeNotFound()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, "filename.tar.gz");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd"); // First for the download file, second for the extraction temp folder.
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var tempFileStream = new MemoryStream();
        fileWrapper.Open(Path.Combine(sha, "xFirst.rnd")).Returns(tempFileStream);
        var archiveFileStream = new MemoryStream();
        fileWrapper.Open(file).Returns(archiveFileStream);
        checksum.ComputeHash(tempFileStream).Returns("sha256");
        fileWrapper.Exists(Path.Combine("sha", "xSecond.rnd", "javaPath")).Returns(false);
        var sut = CreateSutWithSubstitutes();

        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be("The downloaded Java runtime environment could not be extracted.");
        directoryWrapper.Received(2).GetRandomFileName();
        fileWrapper.Received(1).Exists(Path.Combine(sha, "xSecond.rnd", "javaPath"));
        directoryWrapper.Received(1).Delete(Path.Combine(sha, "xSecond.rnd"), true);
        testLogger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the Java Runtime Environment download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive '{file}' to folder '{Path.Combine(sha, "xSecond.rnd")}'.",
            @$"The extraction of the downloaded Java runtime environment failed with error 'The java executable in the extracted Java runtime environment was expected to be at '{Path.Combine(sha, "xSecond.rnd", "javaPath")}' but couldn't be found.'.");
    }

    [TestMethod]
    public async Task Unpack_Failure_ErrorInCleanUpOfTempDirectory()
    {
        var home = @"C:\Users\user\.sonar";
        var cache = Path.Combine(home, "cache");
        var sha = Path.Combine(cache, "sha256");
        var file = Path.Combine(sha, "filename.tar.gz");
        directoryWrapper.Exists(cache).Returns(true);
        directoryWrapper.Exists(sha).Returns(true);
        directoryWrapper.GetRandomFileName().Returns("xFirst.rnd", "xSecond.rnd"); // First for the download file, second for the extraction temp folder.
        fileWrapper.Create(Arg.Any<string>()).Returns(new MemoryStream());
        var tempFileStream = new MemoryStream();
        fileWrapper.Open(Path.Combine(sha, "xFirst.rnd")).Returns(tempFileStream);
        fileWrapper.Open(file).Returns(new MemoryStream());
        checksum.ComputeHash(tempFileStream).Returns("sha256");
        fileWrapper.Exists(Path.Combine(sha, "xSecond.rnd", "javaPath")).Returns(true);
        directoryWrapper.When(x => x.Move(Path.Combine(sha, "xSecond.rnd"), file + "_extracted")).Throw(new IOException("Move failure"));
        directoryWrapper.When(x => x.Delete(Path.Combine(sha, "xSecond.rnd"), true)).Throw(new IOException("Folder cleanup failure"));
        var sut = CreateSutWithSubstitutes();
        var result = await sut.DownloadJreAsync(home, new("filename.tar.gz", "sha256", "javaPath"), () => Task.FromResult<Stream>(new MemoryStream()));
        directoryWrapper.Received(2).GetRandomFileName();
        directoryWrapper.Received(1).Move(Path.Combine(sha, "xSecond.rnd"), file + "_extracted");
        directoryWrapper.Received(1).Delete(Path.Combine(sha, "xSecond.rnd"), true);
        testLogger.DebugMessages.Should().BeEquivalentTo(
            @"Starting the Java Runtime Environment download.",
            @"The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            @$"Starting extracting the Java runtime environment from archive '{file}' to folder '{Path.Combine(sha, "xSecond.rnd")}'.",
            @$"Moving extracted Java runtime environment from '{Path.Combine(sha, "xSecond.rnd")}' to '{file}_extracted'.",
            @"The extraction of the downloaded Java runtime environment failed with error 'Move failure'.",
            @$"The cleanup of the temporary folder for the Java runtime environment extraction at '{Path.Combine(sha, "xSecond.rnd")}' failed with message 'Folder cleanup failure'.");
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
        var fileCache = new FileCache(DirectoryWrapper.Instance, FileWrapper.Instance);
        var sut = new JreCache(testLogger, fileCache, DirectoryWrapper.Instance, FileWrapper.Instance, new ChecksumSha256(), UnpackerFactory.Instance, filePermissionsWrapper);

        try
        {
            var result = await sut.DownloadJreAsync(home, jreDescriptor, () => Task.FromResult<Stream>(new MemoryStream(zipContent)));
            result.Should().BeOfType<CacheHit>().Which.FilePath.Should().Be(
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
            testLogger.AssertSingleInfoMessageExists("""
                The JRE provisioning is a time consuming operation.
                JRE provisioned: OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.zip.
                If you already have a compatible java version installed, please add either the parameter "/d:sonar.scanner.skipJreProvisioning=true" or "/d:sonar.scanner.javaExePath=<PATH>".
                """);
            testLogger.DebugMessages.Should().SatisfyRespectively(
                x => x.Should().Be(@$"Starting the Java Runtime Environment download."),
                x => x.Should().Be(@$"The checksum of the downloaded file is '{sha}' and the expected checksum is '{sha}'."),
                x => x.Should().Match(@$"Starting extracting the Java runtime environment from archive '{Path.Combine(cache, sha, file)}' to folder '{Path.Combine(cache, sha, "*")}'."),
                x => x.Should().Match(@$"Moving extracted Java runtime environment from '{Path.Combine(cache, sha, "*")}' to '{Path.Combine(cache, sha, "OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.zip_extracted")}'."),
                x => x.Should().Be(@$"The Java runtime environment was successfully added to '{Path.Combine(cache, sha, "OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.zip_extracted")}'."));
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
        var fileCache = new FileCache(DirectoryWrapper.Instance, FileWrapper.Instance);
        var sut = new JreCache(testLogger, fileCache, DirectoryWrapper.Instance, FileWrapper.Instance, new ChecksumSha256(), UnpackerFactory.Instance, filePermissionsWrapper);
        try
        {
            var result = await sut.DownloadJreAsync(home, jreDescriptor, () => Task.FromResult<Stream>(new MemoryStream(tarContent)));

            result.Should().BeOfType<CacheHit>().Which.FilePath.Should().Be(
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
            testLogger.AssertSingleInfoMessageExists("""
                The JRE provisioning is a time consuming operation.
                JRE provisioned: OpenJDK17U-jre_x64_windows_hotspot_17.0.11_9.tar.gz.
                If you already have a compatible java version installed, please add either the parameter "/d:sonar.scanner.skipJreProvisioning=true" or "/d:sonar.scanner.javaExePath=<PATH>".
                """);
            testLogger.DebugMessages.Should().SatisfyRespectively(
                x => x.Should().Be("Starting the Java Runtime Environment download."),
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

    private JreCache CreateSutWithSubstitutes() =>
        new JreCache(testLogger, fileCache, directoryWrapper, fileWrapper, checksum, unpackerFactory, filePermissionsWrapper);
}
