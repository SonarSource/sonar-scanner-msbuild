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

namespace SonarScanner.MSBuild.Common;

public class MsBuildPathSettings : IMsBuildPathsSettings
{
    /// <summary>
    /// Not supported versions of MSBuild are listed too, to allow us to throw
    /// errors from the Integration targets in case we detect we are running under
    /// not supported MSBuild.
    /// </summary>
    /// <remarks>
    /// From MSBuild 16.0 onwards, there will no longer be a version-specific folder. Instead,
    /// all versions of MSBuild use "Current".
    /// This means that if we ever need to provide version-specific behavior in the ImportBefore
    /// targets, we will need to put all the behaviors in a single file, and use the
    /// property $(MSBuildAssemblyVersion) to determine version of MSBuild is executing.
    /// See the following tickets for more info:
    /// * https://github.com/SonarSource/sonar-scanner-msbuild/issues/676
    /// * https://github.com/Microsoft/msbuild/issues/3778
    /// * https://github.com/Microsoft/msbuild/issues/4149 (closed as "Won't fix").
    /// </remarks>
    private readonly string[] msBuildVersions = ["4.0", "10.0", "11.0", "12.0", "14.0", "15.0", "Current"];

    private readonly OperatingSystemProvider operatingSystemProvider;

    public MsBuildPathSettings(OperatingSystemProvider operatingSystemProvider) =>
        this.operatingSystemProvider = operatingSystemProvider;

    public IEnumerable<string> ImportBeforePaths()
    {
        var msBuildUserExtensionsPaths = LocalApplicationDataPaths()
            .Distinct()
            .SelectMany(x => msBuildVersions.Select(msBuildVersion => MsBuildImportBeforePath(x, msBuildVersion)))
            .ToList();

        if (msBuildUserExtensionsPaths.Count == 0)
        {
            throw new IOException("Cannot find local application data directory.");
        }

        msBuildUserExtensionsPaths.AddRange(DotnetImportBeforePathsLinuxMac());

        return msBuildUserExtensionsPaths;
    }

    public IEnumerable<string> GlobalTargetsPaths()
    {
        var programFiles = operatingSystemProvider.FolderPath(Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolderOption.None);

        if (string.IsNullOrWhiteSpace(programFiles))
        {
            return [];
        }

        return
        [
            // Up to v15, global targets are dropped under Program Files (x86)\MSBuild.
            // This doesn't appear to be the case for later versions.
            Path.Combine(programFiles, "MSBuild", "14.0", "Microsoft.Common.Targets", "ImportBefore"),
            Path.Combine(programFiles, "MSBuild", "15.0", "Microsoft.Common.Targets", "ImportBefore")
        ];
    }

    private IEnumerable<string> DotnetImportBeforePathsLinuxMac()
    {
        if (operatingSystemProvider.OperatingSystem() == PlatformOS.Windows)
        {
            return [];
        }

        // We don't need to create the paths here - the ITargetsInstaller will do it.
        // Also, see bug #681: Environment.SpecialFolderOption.Create fails on some versions of NET Core on Linux
        var userProfilePath = operatingSystemProvider.FolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);

        if (string.IsNullOrEmpty(userProfilePath))
        {
            throw new IOException("Cannot find user profile directory.");
        }

        // VAL: the comment below seems wrong. The path below does not work on Ubuntu 16.04 and
        // .NET Core SDK 2.1.500: I copied a target file that prints a high importance message and ran
        // "dotnet build"; no message in output. It might work on different OS or SDK, hence I am
        // keeping this method, but we need to do a more robust test in order to delete the code.
        // NOTE: the message is printed if the target file is copied in supported extension points.
        // The supported extension points are documented in the article below:
        // https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2017
        // MSBuildExtensionsPath --> in Program Files
        // MSBuildUserExtensionsPath --> in Local AppData

        // "dotnet build" and "dotnet msbuild" on non-Windows use a different path for import before
        return
        [
            // Older versions are not supported on non-Windows OS
            MsBuildImportBeforePath(userProfilePath, "15.0"),
            MsBuildImportBeforePath(userProfilePath, "Current")
        ];
    }

    /// <summary>
    /// Returns the local AppData path for the current user. This method will return multiple paths if running as Local System.
    /// </summary>
    private IEnumerable<string> LocalApplicationDataPaths()
    {
        var localAppData = operatingSystemProvider.FolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);

        // Return empty enumerable when Local AppData is empty. In this case an exception should be thrown at the call site.
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            yield break;
        }

        yield return localAppData;

        if (operatingSystemProvider.OperatingSystem() == PlatformOS.MacOSX)
        {
            // Target files need to be placed under LocalApplicationData, to be picked up by MSBuild.
            // Due to the breaking change of GetFolderPath on MacOSX in .NET8, we need to make sure we copy the targets file
            // both to the old and to the new location, because we don't know what runtime the build will be run on, and that
            // may differ from the runtime of the scanner.
            // See https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/8.0/getfolderpath-unix#macos
            var userProfile = operatingSystemProvider.FolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
            yield return Path.Combine(userProfile, ".local", "share");                // LocalApplicationData on .Net 7 and earlier
            yield return Path.Combine(userProfile, "Library", "Application Support"); // LocalApplicationData on .Net 8 and later
        }
        else if (operatingSystemProvider.OperatingSystem() == PlatformOS.Windows)
        {
            // The code below is Windows-specific, no need to be executed on non-Windows platforms.
            // When running under Local System account on a 64bit OS, the local application data folder
            // is inside %windir%\system32
            // When a process copies a file in this location, the OS will automatically redirect it to:
            // for 32bit processes - %windir%\sysWOW64\...
            // for 64bit processes - %windir%\system32\...
            // Nice explanation could be found here:
            // https://www.howtogeek.com/326509/whats-the-difference-between-the-system32-and-syswow64-folders-in-windows/
            // If a 32bit process needs to copy files to %windir%\system32, it should use %windir%\Sysnative
            // to avoid the redirection:
            // https://docs.microsoft.com/en-us/windows/desktop/WinProg64/file-system-redirector
            // We need to copy the ImportBefore.targets in both locations to ensure that both the 32bit and 64bit versions
            // of MSBuild will be able to pick them up.
            var systemPath = operatingSystemProvider.FolderPath(
                Environment.SpecialFolder.System,
                Environment.SpecialFolderOption.None); // %windir%\System32
            if (!string.IsNullOrWhiteSpace(systemPath)
                && localAppData.StartsWith(systemPath, StringComparison.OrdinalIgnoreCase))
            {
                // We are under %windir%\System32 => we are running as System Account
                var systemX86Path = operatingSystemProvider.FolderPath(
                    Environment.SpecialFolder.SystemX86,
                    Environment.SpecialFolderOption.None); // %windir%\SysWOW64 (or System32 on 32bit windows)
                var localAppDataX86 = localAppData.ReplaceCaseInsensitive(systemPath, systemX86Path);

                if (operatingSystemProvider.DirectoryExists(localAppDataX86))
                {
                    yield return localAppDataX86;
                }

                var sysNativePath = Path.Combine(Path.GetDirectoryName(systemPath), "Sysnative"); // %windir%\Sysnative
                var localAppDataX64 = localAppData.ReplaceCaseInsensitive(systemPath, sysNativePath);
                if (operatingSystemProvider.DirectoryExists(localAppDataX64))
                {
                    yield return localAppDataX64;
                }
            }
        }
    }

    private static string MsBuildImportBeforePath(string basePath, string msBuildVersion) =>
        Path.Combine(basePath, "Microsoft", "MSBuild", msBuildVersion, "Microsoft.Common.targets", "ImportBefore");
}
