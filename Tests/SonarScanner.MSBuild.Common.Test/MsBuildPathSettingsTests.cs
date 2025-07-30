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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class MsBuildPathSettingsTests
{
    private static readonly Func<string, bool> DirectoryAlwaysExists = _ => true;

    [DataRow(PlatformOS.Windows)]
    [DataRow(PlatformOS.Linux)]
    [DataRow(PlatformOS.MacOSX)]
    [TestMethod]
    public void GetImportBeforePaths_AppData_Is_NullOrEmpty(PlatformOS platformOS)
    {
        var action = new Action(() => MsBuildPathSettings(string.Empty, platformOS, DirectoryAlwaysExists).GetImportBeforePaths());
        action.Should().ThrowExactly<IOException>().WithMessage("Cannot find local application data directory.");

        action = () => MsBuildPathSettings(path: null, platformOS, DirectoryAlwaysExists).GetImportBeforePaths();
        action.Should().ThrowExactly<IOException>().WithMessage("Cannot find local application data directory.");
    }

    [TestMethod]
    public void GetImportBeforePaths_NonWindows_User_Account() =>
        MsBuildPathSettings(
            PlatformOS.Linux,
            [(Environment.SpecialFolder.LocalApplicationData, Path.Combine(TestUtils.DriveRoot(), "app data")),
            (Environment.SpecialFolder.UserProfile, Path.Combine(TestUtils.DriveRoot(), "user profile"))])
            .GetImportBeforePaths()
            .Should().BeEquivalentTo(
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "user profile", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "user profile", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"));

    [TestMethod]
    public void GetImportBeforePaths_NonWindows_User_Account_UserProfile_Missing()
    {
        var action = new Action(() => MsBuildPathSettings(
                                        PlatformOS.Linux,
                                        [(Environment.SpecialFolder.LocalApplicationData, Path.Combine(TestUtils.DriveRoot(), "app data")),
                                        (Environment.SpecialFolder.UserProfile, string.Empty)]).GetImportBeforePaths());
        action.Should().ThrowExactly<IOException>().WithMessage("Cannot find user profile directory.");
    }

    [TestMethod]
    public void GetImportBeforePaths_Windows_User_Account() =>
        MsBuildPathSettings(
            PlatformOS.Windows,
            [(Environment.SpecialFolder.LocalApplicationData, Path.Combine(TestUtils.DriveRoot(), "app data")),
            (Environment.SpecialFolder.System, Path.Combine(TestUtils.DriveRoot(), "windows", "system32")),
            (Environment.SpecialFolder.SystemX86, Path.Combine(TestUtils.DriveRoot(), "windows", "systemWOW64"))])
            .GetImportBeforePaths()
            .Should().BeEquivalentTo(
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"));

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void GetImportBeforePaths_Windows_System_Account() =>
        MsBuildPathSettings(PlatformOS.Windows).GetImportBeforePaths().Should().BeEquivalentTo(
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"));

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void GetImportBeforePaths_Windows_System_Account_WOW64_Missing() =>
        MsBuildPathSettings(PlatformOS.Windows, directoryExistsFunc: x => !x.Contains("WOW64")) // paths with wow64 do not exist, others exist
            .GetImportBeforePaths()
            .Should().BeEquivalentTo(
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "Sysnative", "app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"));

    [TestMethod]
    public void GetImportBeforePaths_Windows_System_Account_Sysnative_Missing() =>
        MsBuildPathSettings(PlatformOS.Windows, directoryExistsFunc: x => !x.Contains("Sysnative")) // paths with Sysnative do not exist, others exist
            .GetImportBeforePaths()
            .Should().BeEquivalentTo(
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64", "app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"));

    [TestMethod]
    [DataRow("/Users/runner/.local/share", DisplayName = "NET 7.0 and earlier")]
    [DataRow("/Users/runner/Library/Application Support", DisplayName = "NET 8.0")]
    [DataRow("/Users/runner/Something/Different", DisplayName = "Future value")]
    public void GetImportBeforePaths_MacOSX_ReturnsBothOldAndNewLocations(string localApplicationData) =>
        MsBuildPathSettings(
            PlatformOS.MacOSX,
            [(Environment.SpecialFolder.LocalApplicationData, localApplicationData), (Environment.SpecialFolder.UserProfile, "/Users/runner")])
            .GetImportBeforePaths()
            .Should().Contain(
            [
            Path.Combine("/Users/runner", ".local", "share", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("/Users/runner", "Library", "Application Support", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore")
            ]);

    [TestMethod]
    public void GetGlobalTargetsPaths_WhenProgramFilesIsEmptyOrNull_Returns_Empty()
    {
        var testSubject1 = new MsBuildPathSettings(CreateOsProvider(PlatformOS.Windows, (folder, _) => folder == Environment.SpecialFolder.ProgramFiles ? null : "f"));
        var testSubject2 = new MsBuildPathSettings(CreateOsProvider(PlatformOS.Windows, (folder, _) => folder == Environment.SpecialFolder.ProgramFiles ? string.Empty : "f"));
        testSubject1.GetGlobalTargetsPaths().Should().BeEmpty();
        testSubject2.GetGlobalTargetsPaths().Should().BeEmpty();
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void GetGlobalTargetsPaths_Windows_WhenProgramFilesNotEmpty_ReturnsExpectedPaths() =>
        MsBuildPathSettings(PlatformOS.Windows, [(Environment.SpecialFolder.ProgramFiles, Path.Combine(TestUtils.DriveRoot(), "Program"))])
            .GetGlobalTargetsPaths()
            .Should().BeEquivalentTo(
            Path.Combine(TestUtils.DriveRoot(), "Program", "MSBuild", "14.0", "Microsoft.Common.Targets", "ImportBefore"),
            Path.Combine(TestUtils.DriveRoot(), "Program", "MSBuild", "15.0", "Microsoft.Common.Targets", "ImportBefore"));

    private static MsBuildPathSettings MsBuildPathSettings(
        PlatformOS os,
        (Environment.SpecialFolder, string)[] paths = null,
        Func<string, bool> directoryExistsFunc = null)
    {
        paths ??= [(Environment.SpecialFolder.LocalApplicationData, Path.Combine(TestUtils.DriveRoot(), "windows", "system32", "app data")),
            (Environment.SpecialFolder.System, Path.Combine(TestUtils.DriveRoot(), "windows", "system32")),
            (Environment.SpecialFolder.SystemX86, Path.Combine(TestUtils.DriveRoot(), "windows", "sysWOW64"))];
        directoryExistsFunc ??= DirectoryAlwaysExists;

        return new(CreateOsProvider(os, (folder, _) => paths.First(p => p.Item1 == folder).Item2, directoryExistsFunc));
    }

    private static MsBuildPathSettings MsBuildPathSettings(
        string path,
        PlatformOS os,
        Func<string, bool> directoryExistsFunc) =>
            new(CreateOsProvider(os, (_, _) => path, directoryExistsFunc));

    private static IOperatingSystemProvider CreateOsProvider(
        PlatformOS os,
        Func<Environment.SpecialFolder, Environment.SpecialFolderOption, string> getFolderPath,
        Func<string, bool> directoryExists = null,
        bool? isUnix = null)
    {
        var provider = Substitute.For<IOperatingSystemProvider>();
        provider.OperatingSystem().Returns(os);
        provider.DirectoryExists(Arg.Any<string>()).Returns(x => directoryExists is null || directoryExists(x.Arg<string>()));
        provider.GetFolderPath(Arg.Any<Environment.SpecialFolder>(), Arg.Any<Environment.SpecialFolderOption>())
            .Returns(x => getFolderPath(x.Arg<Environment.SpecialFolder>(), x.Arg<Environment.SpecialFolderOption>()));
        provider.IsUnix().Returns(isUnix ?? (os == PlatformOS.Linux || os == PlatformOS.MacOSX || os == PlatformOS.Alpine));
        return provider;
    }
}
