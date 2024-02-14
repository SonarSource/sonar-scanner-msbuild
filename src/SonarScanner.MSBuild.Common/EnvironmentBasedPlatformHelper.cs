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
using System.IO;

namespace SonarScanner.MSBuild.Common;

public class EnvironmentBasedPlatformHelper : IPlatformHelper
{
    private bool? isMacOs;
    public static IPlatformHelper Instance { get; } = new EnvironmentBasedPlatformHelper();

    private EnvironmentBasedPlatformHelper()
    {
    }

    public string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option) => Environment.GetFolderPath(folder, option);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool IsWindows() => Environment.OSVersion.Platform == PlatformID.Win32NT;

    // There's a more elegant way to obtain which operating system the app is running on. Unfortunately it's not suported in .NET Framework 4.6.2, only from 4.7.1
    // SystemVersion.plist exists on Mac Os (and iOS) for a very long time (at least from 2002), so it's safe to check it, even though it's not a pretty solution.
    // TODO: once we drop support for .NET Framework 4.6.2 replace this call with System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
    public bool IsMacOs() => isMacOs ??= File.Exists(@"/System/Library/CoreServices/SystemVersion.plist");
}
