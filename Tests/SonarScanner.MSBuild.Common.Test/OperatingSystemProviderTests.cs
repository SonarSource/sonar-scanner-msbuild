/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class OperatingSystemProviderTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void FolderPath_WithUserProfile()
    {
        var sut = new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>());
        sut.FolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.None)
            .Should().Be(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.None));
    }

    [TestMethod]
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    public void OperatingSystem_Windows() =>
        new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()).OperatingSystem().Should().Be(PlatformOS.Windows);

    [TestMethod]
    [TestCategory(TestCategories.NoWindows)]
    [TestCategory(TestCategories.NoLinux)]
    public void OperatingSystem_MacOS() =>
        new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()).OperatingSystem().Should().Be(PlatformOS.MacOSX);

    [TestMethod]
    [TestCategory(TestCategories.NoWindows)]
    [TestCategory(TestCategories.NoMacOS)]
    public void OperatingSystem_Linux() =>
        new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()).OperatingSystem().Should().Be(PlatformOS.Linux);

    [TestMethod]
    [TestCategory(TestCategories.NoWindows)]
    [TestCategory(TestCategories.NoMacOS)]
    [DataRow("/etc/os-release", "alpine", true)]
    [DataRow("/etc/os-release", "ubuntu", false)]
    [DataRow("/etc/os-release", "\"rocky\"", false)] // https://github.com/which-distro/os-release/blob/main/rocky/8.6
    [DataRow("/usr/lib/os-release", "alpine", true)]
    [DataRow("/usr/lib/os-release", "ubuntu", false)]
    [DataRow("/usr/lib/os-release", "\"rhel\"", false)] // https://github.com/chef/os_release/blob/main/redhat_8
    [DataRow("/etc/other-file", "alpine", false)]
    public void OperatingSystem_Alpine(string path, string id, bool isAlpine) =>
        new OperatingSystemProvider(FileWrapperWithOSFile(path, id), Substitute.For<ILogger>()).OperatingSystem().Should().Be(isAlpine ? PlatformOS.Alpine : PlatformOS.Linux);

    [TestMethod]
    [TestCategory(TestCategories.NoWindows)]
    [TestCategory(TestCategories.NoMacOS)]
    public void OperatingSystem_InvalidAccess_IsLinux()
    {
        var exception = new UnauthorizedAccessException();
        var logger = Substitute.For<ILogger>();
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists("/etc/os-release").Returns(true);
        fileWrapper.When(x => x.ReadAllText("/etc/os-release")).Do(_ => throw exception);
        var sut = new OperatingSystemProvider(fileWrapper, logger);

        sut.OperatingSystem().Should().Be(PlatformOS.Linux);
        logger.Received(1).LogWarning("Cannot detect the operating system. {0}", exception.Message);
    }

    [TestMethod]
    [TestCategory(TestCategories.NoLinux)]
    public void OperatingSystem_WindowsAndMacOS_AreNotAlpine() =>
        new OperatingSystemProvider(FileWrapperForAlpine(), Substitute.For<ILogger>()).OperatingSystem().Should().NotBe(PlatformOS.Alpine);

    [TestMethod]
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    public void IsUnix_OnWindows_IsFalse() =>
        new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()).IsUnix().Should().Be(false);

    [TestMethod]
    [TestCategory(TestCategories.NoWindows)]
    public void IsUnix_OnUnix_IsTrue() =>
        new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()).IsUnix().Should().Be(true);

    [TestMethod]
    [TestCategory(TestCategories.NoWindows)]
    [TestCategory(TestCategories.NoMacOS)]
    public void IsUnix_OnAlpine_IsTrue() =>
        new OperatingSystemProvider(FileWrapperForAlpine(), Substitute.For<ILogger>()).IsUnix().Should().Be(true);

#if NET

    [TestMethod]
    [TestCategory(TestCategories.NoWindows)]
    [TestCategory(TestCategories.NoMacOS)]
    public void SetPermission()
    {
        var filePath = TestUtils.CreateFile(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext), "testfile.txt", "content");
        var sut = new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>());   // SetPermission does not use fileWrapper
        var expectedMode = Convert.ToInt32("555", 8);

        File.GetUnixFileMode(filePath).Should().NotBe((UnixFileMode)expectedMode);
        sut.SetPermission(filePath, expectedMode);
        File.GetUnixFileMode(filePath).Should().Be((UnixFileMode)expectedMode);
    }

#endif

    private static IFileWrapper FileWrapperForAlpine() =>
        FileWrapperWithOSFile("/etc/os-release", "alpine");

    private static IFileWrapper FileWrapperWithOSFile(string path, string id)
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(path).Returns(true);
        fileWrapper.ReadAllText(path).Returns($"""
            NAME="Alpine Linux"
            ID={id}
            VERSION_ID=3.17.0
            PRETTY_NAME="Alpine Linux v3.17"
            HOME_URL="https://alpinelinux.org/"
            BUG_REPORT_URL="https://gitlab.alpinelinux.org/alpine/aports/-/issues"
            """);
        return fileWrapper;
    }
}
