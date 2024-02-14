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

public class EnvironmentBasedPlatformHelper : IPlatformHelper
{
    private readonly Lazy<IPlatformHelper.OS> operatingSystem = new(CurrentOperatingSystem);

    public IPlatformHelper.OS OperatingSystem => operatingSystem.Value;
    public static IPlatformHelper Instance { get; } = new EnvironmentBasedPlatformHelper();

    private EnvironmentBasedPlatformHelper()
    {
    }

    public string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option) => Environment.GetFolderPath(folder, option);
    public bool DirectoryExists(string path) => Directory.Exists(path);

    [ExcludeFromCodeCoverage]
    private static IPlatformHelper.OS CurrentOperatingSystem()
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            return IPlatformHelper.OS.Windows;
        }
        else if (IsMacOs())
        {
            return IPlatformHelper.OS.MacOSX;
        }
        // Note: the Check for Mac Os must preceed the check for Unix, because Environment.OSVersion.Platform returns PlatformID.Unix on Mac Os
        else if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            return IPlatformHelper.OS.Unix;
        }
        else
        {
            return IPlatformHelper.OS.Unknown;
        }
    }

    // RuntimeInformation.IsOSPlatform is not suported in .NET Framework 4.6.2, it's only available from 4.7.1
    // SystemVersion.plist exists on Mac Os (and iOS) for a very long time (at least from 2002), so it's safe to check it, even though it's not a pretty solution.
    // TODO: once we drop support for .NET Framework 4.6.2 remove the call to File.Exists and use RuntimeInformation.IsOSPlatform instead of the Environment.OSVersion.Platform property
    private static bool IsMacOs() =>
#if NETSTANDARD1_1_OR_GREATER || NETCOREAPP1_0_OR_GREATER
        System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
#else
        File.Exists(@"/System/Library/CoreServices/SystemVersion.plist");
#endif
}
