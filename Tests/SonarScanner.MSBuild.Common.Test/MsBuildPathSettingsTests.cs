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
    [DataTestMethod]
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
            [(Environment.SpecialFolder.LocalApplicationData, "c:\\app data"),
            (Environment.SpecialFolder.UserProfile, "c:\\user profile")])
            .GetImportBeforePaths()
            .Should().BeEquivalentTo(
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\user profile", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\user profile", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"));

    [TestMethod]
    public void GetImportBeforePaths_NonWindows_User_Account_UserProfile_Missing()
    {
        var action = new Action(() => MsBuildPathSettings(
                                        PlatformOS.Linux,
                                        [(Environment.SpecialFolder.LocalApplicationData, "c:\\app data"), (Environment.SpecialFolder.UserProfile, string.Empty)]).GetImportBeforePaths());

        action.Should().ThrowExactly<IOException>().WithMessage("Cannot find user profile directory.");
    }

    [TestMethod]
    public void GetImportBeforePaths_Windows_User_Account() =>
        MsBuildPathSettings(
            PlatformOS.Windows,
            [(Environment.SpecialFolder.LocalApplicationData, "c:\\app data"),
            (Environment.SpecialFolder.System, "c:\\windows\\system32"),
            (Environment.SpecialFolder.SystemX86, "c:\\windows\\systemWOW64")])
            .GetImportBeforePaths()
            .Should().BeEquivalentTo(
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"));

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void GetImportBeforePaths_Windows_System_Account() =>
        MsBuildPathSettings(PlatformOS.Windows).GetImportBeforePaths().Should().BeEquivalentTo(
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"));

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void GetImportBeforePaths_Windows_System_Account_WOW64_Missing() =>
        MsBuildPathSettings(PlatformOS.Windows, directoryExistsFunc: x => !x.Contains("WOW64")) // paths with wow64 do not exist, others exist
            .GetImportBeforePaths()
            .Should().BeEquivalentTo(
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\Sysnative\\app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"));

    [TestMethod]
    public void GetImportBeforePaths_Windows_System_Account_Sysnative_Missing() =>
        MsBuildPathSettings(PlatformOS.Windows, directoryExistsFunc: x => !x.Contains("Sysnative")) // paths with Sysnative do not exist, others exist
            .GetImportBeforePaths()
            .Should().BeEquivalentTo(
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\system32\\app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\windows\\sysWOW64\\app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"));

    [DataTestMethod]
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
        var testSubject1 = new MsBuildPathSettings(new OperatingSystemProvider((x, _) => x == Environment.SpecialFolder.ProgramFiles ? null : "f", PlatformOS.Windows, DirectoryAlwaysExists));
        var testSubject2 = new MsBuildPathSettings(new OperatingSystemProvider((x, _) => x == Environment.SpecialFolder.ProgramFiles ? string.Empty : "f", PlatformOS.Windows, DirectoryAlwaysExists));

        testSubject1.GetGlobalTargetsPaths().Should().BeEmpty();
        testSubject2.GetGlobalTargetsPaths().Should().BeEmpty();
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void GetGlobalTargetsPaths_Windows_WhenProgramFilesNotEmpty_ReturnsExpectedPaths() =>
        MsBuildPathSettings(PlatformOS.Windows, [(Environment.SpecialFolder.ProgramFiles, "C:\\Program")])
            .GetGlobalTargetsPaths()
            .Should().BeEquivalentTo(
            "C:\\Program\\MSBuild\\14.0\\Microsoft.Common.Targets\\ImportBefore",
            "C:\\Program\\MSBuild\\15.0\\Microsoft.Common.Targets\\ImportBefore");

    private static MsBuildPathSettings MsBuildPathSettings(
        PlatformOS os,
        (Environment.SpecialFolder, string)[] paths = null,
        Func<string, bool> directoryExistsFunc = null)
    {

        paths ??= [(Environment.SpecialFolder.LocalApplicationData, "c:\\windows\\system32\\app data"),
            (Environment.SpecialFolder.System, "c:\\windows\\system32"),
            (Environment.SpecialFolder.SystemX86, "c:\\windows\\sysWOW64")];

        directoryExistsFunc ??= DirectoryAlwaysExists;

        return new(new OperatingSystemProvider(
            (folder, option) =>
            {
                // Bug #681 - the Create option doesn't work on some NET Core versions on Linux
                option.Should().NotBe(Environment.SpecialFolderOption.Create);
                return paths.First(p => p.Item1 == folder).Item2;
            },
            os,
            directoryExistsFunc));
    }

    private static MsBuildPathSettings MsBuildPathSettings(
        string path,
        PlatformOS os,
        Func<string, bool> directoryExistsFunc) =>
        new(new OperatingSystemProvider((_, _) => path, os, directoryExistsFunc));

    private sealed class OperatingSystemProvider : IOperatingSystemProvider
    {
        private readonly Func<Environment.SpecialFolder, Environment.SpecialFolderOption, string> pathFunc;
        private readonly PlatformOS os;
        private readonly Func<string, bool> directoryExistsFunc;

        public OperatingSystemProvider(
            Func<Environment.SpecialFolder, Environment.SpecialFolderOption, string> pathFunc,
            PlatformOS os,
            Func<string, bool> directoryExistsFunc)
        {
            this.pathFunc = pathFunc;
            this.os = os;
            this.directoryExistsFunc = directoryExistsFunc;
        }

        public PlatformOS OperatingSystem() => os;

        public bool DirectoryExists(string path) =>
            directoryExistsFunc(path);

        public string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option) =>
            pathFunc(folder, option);

        public bool IsUnix() => throw new NotSupportedException();
    }
}
