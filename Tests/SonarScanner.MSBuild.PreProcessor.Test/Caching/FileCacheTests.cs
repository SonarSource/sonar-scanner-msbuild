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
    private const string SonarUserHome = "some/home/path";
    private readonly IDirectoryWrapper directoryWrapper;
    private readonly IFileWrapper fileWrapper;

    public FileCacheTests()
    {
        directoryWrapper = Substitute.For<IDirectoryWrapper>();
        fileWrapper = Substitute.For<IFileWrapper>();
    }

    [TestMethod]
    public void EnsureCacheRoot_CreatesDirectory_IfNotExists()
    {
        var fileCache = new FileCache(directoryWrapper, fileWrapper);
        var expectedCacheRoot = Path.Combine(SonarUserHome, ".sonar", "cache");
        directoryWrapper.Exists(expectedCacheRoot).Returns(false);

        var result = fileCache.EnsureCacheRoot(SonarUserHome, out var cacheRoot);

        result.Should().BeTrue();
        cacheRoot.Should().Be(expectedCacheRoot);
        directoryWrapper.Received(1).Exists(expectedCacheRoot);
        directoryWrapper.Received(1).CreateDirectory(expectedCacheRoot);
    }

    [TestMethod]
    public void EnsureCacheRoot_DoesNotCreateDirectory_IfExists()
    {
        var fileCache = new FileCache(directoryWrapper, fileWrapper);
        var expectedCacheRoot = Path.Combine(SonarUserHome, ".sonar", "cache");
        directoryWrapper.Exists(expectedCacheRoot).Returns(true);

        var result = fileCache.EnsureCacheRoot(SonarUserHome, out var cacheRoot);

        result.Should().BeTrue();
        cacheRoot.Should().Be(expectedCacheRoot);
        directoryWrapper.Received(1).Exists(expectedCacheRoot);
        directoryWrapper.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureDirectoryExists_CreatesDirectory_IfNotExists()
    {
        var fileCache = new FileCache(directoryWrapper, fileWrapper);
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
        var fileCache = new FileCache(directoryWrapper, fileWrapper);
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
        var fileCache = new FileCache(directoryWrapper, fileWrapper);
        var expected = Path.Combine(SonarUserHome, ".sonar", "cache");

        var result = fileCache.CacheRoot(SonarUserHome);

        result.Should().Be(expected);
    }
}
