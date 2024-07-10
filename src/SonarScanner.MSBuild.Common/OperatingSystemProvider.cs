/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SonarScanner.MSBuild.Common;

public sealed class OperatingSystemProvider : IOperatingSystemProvider
{
    private readonly IFileWrapper fileWrapper;
    private readonly ILogger logger;
    private readonly Lazy<PlatformOS> operatingSystem;

    public OperatingSystemProvider(IFileWrapper fileWrapper, ILogger logger)
    {
        this.fileWrapper = fileWrapper;
        this.logger = logger;
        operatingSystem = new Lazy<PlatformOS>(OperatingSystemCore);
    }

    public string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option) => Environment.GetFolderPath(folder, option);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public PlatformOS OperatingSystem() => operatingSystem.Value;

    public bool IsAlpine() =>
        IsAlpineRelease("/etc/os-release")
        || IsAlpineRelease("/usr/lib/os-release");

    // Not stable testable
    [ExcludeFromCodeCoverage]
    public bool IsUnix() =>
        OperatingSystem() is PlatformOS.Linux or PlatformOS.Alpine;

    // Not stable testable, manual testing was done by running the scanner on Windows, Mac OS X and Linux.
    [ExcludeFromCodeCoverage]
    private PlatformOS OperatingSystemCore()
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            return PlatformOS.Windows;
        }
        else if (IsMacOSX())
        {
            return PlatformOS.MacOSX;
        }
        // Note: the Check for Mac OS X must precede the check for Unix, because Environment.OSVersion.Platform returns PlatformID.Unix on Mac OS X
        else if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            return IsAlpine() ? PlatformOS.Alpine : PlatformOS.Linux;
        }
        else
        {
            return PlatformOS.Unknown;
        }
    }

    // RuntimeInformation.IsOSPlatform is not supported in .NET Framework 4.6.2, it's only available from 4.7.1
    // SystemVersion.plist exists on Mac OS X (and iOS) at least since 2002, so it's safe to check it, even though it's not a robust, future-proof solution.
    // See: https://stackoverflow.com/a/38795621
    // TODO: once we drop support for .NET Framework 4.6.2 remove the call to File.Exists and use RuntimeInformation.IsOSPlatform instead of the Environment.OSVersion.Platform property
    private static bool IsMacOSX() =>
#if NETSTANDARD1_1_OR_GREATER || NETCOREAPP1_0_OR_GREATER
        System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
#else
        File.Exists("/System/Library/CoreServices/SystemVersion.plist");
#endif

    // See: https://www.freedesktop.org/software/systemd/man/latest/os-release.html
    // Examples: "ID=alpine", "ID=fedora", "ID=debian".
    private bool IsAlpineRelease(string releaseInfoFilePath)
    {
        if (!fileWrapper.Exists(releaseInfoFilePath))
        {
            return false;
        }

        try
        {
            return fileWrapper.ReadAllText(releaseInfoFilePath).Contains("ID=alpine");
        }
        catch (Exception exception)
        {
            logger.LogWarning(Resources.WARN_FailedToReadFile, exception.Message);
            return false;
        }
    }
}
