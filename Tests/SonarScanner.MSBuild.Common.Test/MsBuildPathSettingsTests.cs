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

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class MsBuildPathSettingsTests
{
    private static readonly Func<string, bool> DirectoryAlwaysExists = _ => true;

    [TestMethod]
    public void GetImportBeforePaths_NonWindows_AppData_Is_NullOrEmpty()
    {
        Action action;

        action = new Action(() => MsBuildPathSettings(string.Empty, PlatformOS.Linux, DirectoryAlwaysExists).GetImportBeforePaths());
        action.Should().ThrowExactly<IOException>().WithMessage("Cannot find local application data directory.");

        action = new Action(() => MsBuildPathSettings(path: null, PlatformOS.Linux, DirectoryAlwaysExists).GetImportBeforePaths());
        action.Should().ThrowExactly<IOException>().WithMessage("Cannot find local application data directory.");
    }

    [TestMethod]
    public void GetImportBeforePaths_NonWindows_User_Account()
    {
        var paths = new[]
        {
            (Environment.SpecialFolder.LocalApplicationData, "c:\\app data"),
            (Environment.SpecialFolder.UserProfile, "c:\\user profile"),
        };

        var result = MsBuildPathSettings(paths, PlatformOS.Linux, DirectoryAlwaysExists).GetImportBeforePaths();

        result.Should().BeEquivalentTo(
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\user profile", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\user profile", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"));
    }

    [TestMethod]
    public void GetImportBeforePaths_NonWindows_User_Account_UserProfile_Missing()
    {
        var paths = new[]
        {
            (Environment.SpecialFolder.LocalApplicationData, "c:\\app data"),
            (Environment.SpecialFolder.UserProfile, string.Empty),
        };

        var action = new Action(() => MsBuildPathSettings(paths, PlatformOS.Linux, DirectoryAlwaysExists).GetImportBeforePaths());

        action.Should().ThrowExactly<IOException>().WithMessage("Cannot find user profile directory.");
    }

    [TestMethod]
    public void GetImportBeforePaths_Windows_AppData_Is_NullOrEmpty()
    {
        Action action;

        action = new Action(() => MsBuildPathSettings(string.Empty, PlatformOS.Windows, DirectoryAlwaysExists).GetImportBeforePaths());
        action.Should().ThrowExactly<IOException>().WithMessage("Cannot find local application data directory.");

        action = new Action(() => MsBuildPathSettings(path: null, PlatformOS.Windows, DirectoryAlwaysExists).GetImportBeforePaths());
        action.Should().ThrowExactly<IOException>().WithMessage("Cannot find local application data directory.");
    }

    [TestMethod]
    public void GetImportBeforePaths_Windows_User_Account()
    {
        var paths = new[]
        {
            (Environment.SpecialFolder.LocalApplicationData, "c:\\app data"),
            (Environment.SpecialFolder.System, "c:\\windows\\system32"),
            (Environment.SpecialFolder.SystemX86, "c:\\windows\\systemWOW64"),
        };

        var settings = MsBuildPathSettings(paths, PlatformOS.Windows, DirectoryAlwaysExists);

        var result = settings.GetImportBeforePaths();

        result.Should().BeEquivalentTo(
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "10.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "11.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("c:\\app data", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"));
    }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void GetImportBeforePaths_Windows_System_Account()
    {
        var paths = new[]
        {
            (Environment.SpecialFolder.LocalApplicationData, "c:\\windows\\system32\\app data"),
            (Environment.SpecialFolder.System, "c:\\windows\\system32"),
            (Environment.SpecialFolder.SystemX86, "c:\\windows\\sysWOW64"),
        };

        var settings = MsBuildPathSettings(paths, PlatformOS.Windows, DirectoryAlwaysExists);

        var result = settings.GetImportBeforePaths();

        result.Should().BeEquivalentTo(
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
    }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void GetImportBeforePaths_Windows_System_Account_WOW64_Missing()
    {
        var paths = new[]
        {
            (Environment.SpecialFolder.LocalApplicationData, "c:\\windows\\system32\\app data"),
            (Environment.SpecialFolder.System, "c:\\windows\\system32"),
            (Environment.SpecialFolder.SystemX86, "c:\\windows\\sysWOW64"),
        };

        var settings = MsBuildPathSettings(paths, PlatformOS.Windows, path => !path.Contains("WOW64")); // paths with wow64 do not exist, others exist

        var result = settings.GetImportBeforePaths();

        result.Should().BeEquivalentTo(
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
    }

    [TestMethod]
    public void GetImportBeforePaths_Windows_System_Account_Sysnative_Missing()
    {
        var paths = new[]
        {
            (Environment.SpecialFolder.LocalApplicationData, "c:\\windows\\system32\\app data"),
            (Environment.SpecialFolder.System, "c:\\windows\\system32"),
            (Environment.SpecialFolder.SystemX86, "c:\\windows\\sysWOW64"),
        };

        var settings = MsBuildPathSettings(paths, PlatformOS.Windows, path => !path.Contains("Sysnative")); // paths with Sysnative do not exist, others exist

        var result = settings.GetImportBeforePaths();

        result.Should().BeEquivalentTo(
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
    }

    [DataTestMethod]
    [DataRow("/Users/runner/.local/share", DisplayName = "NET 7.0 and earlier")]
    [DataRow("/Users/runner/Library/Application Support", DisplayName = "NET 8.0")]
    [DataRow("/Users/runner/Something/Different", DisplayName = "Future value")]
    public void GetImportBeforePaths_MacOSX_ReturnsBothOldAndNewLocations(string localApplicationData)
    {
        var paths = new[]
        {
            (Environment.SpecialFolder.LocalApplicationData, localApplicationData),
            (Environment.SpecialFolder.UserProfile, "/Users/runner"),
        };

        var result = MsBuildPathSettings(paths, PlatformOS.MacOSX, DirectoryAlwaysExists).GetImportBeforePaths();

        result.Should().Contain(new[]
        {
            Path.Combine("/Users/runner", ".local", "share", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore"),
            Path.Combine("/Users/runner", "Library", "Application Support", "Microsoft", "MSBuild", "Current", "Microsoft.Common.targets", "ImportBefore")
        });
    }

    [TestMethod]
    public void GetGlobalTargetsPaths_WhenProgramFilesIsEmptyOrNull_Returns_Empty()
    {
        // Arrange
        var testSubject1 = new MsBuildPathSettings(new OperatingSystemProvider((x, y) => x == Environment.SpecialFolder.ProgramFiles ? null : "foo", PlatformOS.Windows, DirectoryAlwaysExists));
        var testSubject2 = new MsBuildPathSettings(new OperatingSystemProvider((x, y) => x == Environment.SpecialFolder.ProgramFiles ? string.Empty : "foo", PlatformOS.Windows, DirectoryAlwaysExists));

        // Act
        testSubject1.GetGlobalTargetsPaths().Should().BeEmpty();
        testSubject2.GetGlobalTargetsPaths().Should().BeEmpty();
    }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void GetGlobalTargetsPaths_WhenProgramFilesNotEmpty_ReturnsExpectedPaths()
    {
        var paths = new[]
        {
            (Environment.SpecialFolder.ProgramFiles, "C:\\Program")
        };

        // Arrange
        var settings = MsBuildPathSettings(paths, PlatformOS.Windows, DirectoryAlwaysExists);

        // Act
        var result = settings.GetGlobalTargetsPaths().ToList();

        // Assert
        result.Should().BeEquivalentTo(
            "C:\\Program\\MSBuild\\14.0\\Microsoft.Common.Targets\\ImportBefore",
            "C:\\Program\\MSBuild\\15.0\\Microsoft.Common.Targets\\ImportBefore");
    }

    private MsBuildPathSettings MsBuildPathSettings(
        (Environment.SpecialFolder, string)[] paths,
        PlatformOS os,
        Func<string, bool> directoryExistsFunc) =>
        new(new OperatingSystemProvider((folder, option) =>
            {
                // Bug #681 - the Create option doesn't work on some NET Core versions on Linux
                option.Should().NotBe(Environment.SpecialFolderOption.Create);
                return paths.First(p => p.Item1 == folder).Item2;
            },
            os, directoryExistsFunc));

    private MsBuildPathSettings MsBuildPathSettings(
        string path,
        PlatformOS os,
        Func<string, bool> directoryExistsFunc) =>
        new(new OperatingSystemProvider((_, _) => path, os, directoryExistsFunc));

    private sealed class OperatingSystemProvider(
        Func<Environment.SpecialFolder, Environment.SpecialFolderOption, string> pathFunc,
        PlatformOS os,
        Func<string, bool> directoryExistsFunc) : IOperatingSystemProvider
    {
        public PlatformOS OperatingSystem() => os;

        public bool DirectoryExists(string path) => directoryExistsFunc(path);

        public string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option) => pathFunc(folder, option);

        public bool IsUnix() => throw new NotImplementedException();
    }
}
