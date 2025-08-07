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

namespace SonarScanner.MSBuild.PreProcessor.Caching.Test;

[TestClass]
public class FileCacheTests
{
    private readonly string sonarUserHome;
    private readonly string sonarUserHomeCache;
    private readonly IDirectoryWrapper directoryWrapper;
    private readonly IFileWrapper fileWrapper;
    private readonly FileCache fileCache;

    public FileCacheTests()
    {
        sonarUserHome = Path.Combine("home", ".sonar");
        sonarUserHomeCache = Path.Combine(sonarUserHome, "cache");
        directoryWrapper = Substitute.For<IDirectoryWrapper>();
        fileWrapper = Substitute.For<IFileWrapper>();
        fileCache = new FileCache(directoryWrapper, fileWrapper);
    }

    [TestMethod]
    public void EnsureCacheRoot_CreatesDirectory_IfNotExists()
    {
        directoryWrapper.Exists(sonarUserHomeCache).Returns(false);

        var result = fileCache.EnsureCacheRoot(sonarUserHome, out var cacheRoot);

        result.Should().BeTrue();
        cacheRoot.Should().Be(sonarUserHomeCache);
        directoryWrapper.Received(1).Exists(sonarUserHomeCache);
        directoryWrapper.Received(1).CreateDirectory(sonarUserHomeCache);
    }

    [TestMethod]
    public void EnsureCacheRoot_DoesNotCreateDirectory_IfExists()
    {
        directoryWrapper.Exists(sonarUserHomeCache).Returns(true);

        var result = fileCache.EnsureCacheRoot(sonarUserHome, out var cacheRoot);

        result.Should().BeTrue();
        cacheRoot.Should().Be(sonarUserHomeCache);
        directoryWrapper.Received(1).Exists(sonarUserHomeCache);
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureCacheRoot_WhenCreateDirectoryThrows_FalseIsReturned()
    {
        directoryWrapper.Exists(sonarUserHomeCache).Returns(false);
        directoryWrapper
            .When(x => x.CreateDirectory(sonarUserHomeCache))
            .Do(_ => throw new IOException("Disk full"));

        var result = fileCache.EnsureCacheRoot(sonarUserHome, out var path);

        result.Should().BeFalse();
        path.Should().BeNull();

        directoryWrapper.Received(1).Exists(sonarUserHomeCache);
        directoryWrapper.Received(1).CreateDirectory(sonarUserHomeCache);
    }

    [TestMethod]
    public void EnsureDirectoryExists_CreatesDirectory_IfNotExists()
    {
        var dir = "some/dir";
        directoryWrapper.Exists(dir).Returns(false);

        var result = fileCache.EnsureDirectoryExists(dir);

        result.Should().Be(dir);
        directoryWrapper.Received(1).Exists(dir);
        directoryWrapper.Received(1).CreateDirectory(dir);
    }

    [TestMethod]
    public void EnsureDirectoryExists_DoesNotCreateDirectory_IfExists()
    {
        var dir = "some/dir";
        directoryWrapper.Exists(dir).Returns(true);

        var result = fileCache.EnsureDirectoryExists(dir);

        result.Should().Be(dir);
        directoryWrapper.Received(1).Exists(dir);
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public void CacheRoot_ReturnsExpectedPath()
    {
        var result = fileCache.CacheRoot(sonarUserHome);

        result.Should().Be(sonarUserHomeCache);
    }

    [TestMethod]
    public void IsFileCached_CacheHit()
    {
        var fileDescriptor = new FileDescriptor("somefile.jar", "sha256");
        var file = Path.Combine(sonarUserHomeCache, fileDescriptor.Sha256, fileDescriptor.Filename);
        fileWrapper.Exists(file).Returns(true);
        var result = fileCache.IsFileCached(sonarUserHome, fileDescriptor);

        result.Should().BeOfType<CacheHit>().Which.FilePath.Should().Be(file);
        fileWrapper.Received(1).Exists(file);
    }

    [TestMethod]
    public void IsFileCached_CacheMiss_FileNotExists()
    {
        var fileDescriptor = new FileDescriptor("somefile.jar", "sha256");
        var file = Path.Combine(sonarUserHomeCache, fileDescriptor.Sha256, fileDescriptor.Filename);
        fileWrapper.Exists(file).Returns(false);

        var result = fileCache.IsFileCached(sonarUserHome, fileDescriptor);

        result.Should().BeOfType<CacheMiss>();
        fileWrapper.Received(1).Exists(file);
    }

    [TestMethod]
    public void IsFileCached_CacheMiss_Failure()
    {
        var fileDescriptor = new FileDescriptor("somefile.jar", "sha256");
        directoryWrapper.Exists(sonarUserHomeCache).Returns(false);
        directoryWrapper.When(x => x.CreateDirectory(sonarUserHomeCache)).Do(_ => throw new IOException("Disk full"));

        var result = fileCache.IsFileCached(sonarUserHome, fileDescriptor);

        result.Should().BeOfType<CacheFailure>().Which.Message.Should().Be($"The file cache directory in '{sonarUserHomeCache}' could not be created.");
    }
}
