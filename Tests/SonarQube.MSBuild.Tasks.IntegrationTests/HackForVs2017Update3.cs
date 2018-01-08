/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using Microsoft.Win32;

namespace SonarQube.MSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// Fix for:
    /// Microsoft.Build.Exceptions.InvalidProjectFileException: 'The tools version "15.0" is unrecognized.
    /// Available tools versions are "12.0", "14.0", "2.0", "3.5", "4.0".'
    ///
    /// Coming from this thread: https://github.com/Microsoft/msbuild/issues/2369#issuecomment-331884835
    /// </summary>
    public static class HackForVs2017Update3
    {
        public static void Enable()
        {
            var registryKey = $@"SOFTWARE{(Environment.Is64BitProcess ? @"\Wow6432Node" : string.Empty)}\Microsoft\VisualStudio\SxS\VS7";
            using (var subKey = Registry.LocalMachine.OpenSubKey(registryKey))
            {
                var visualStudioPath = subKey?.GetValue("15.0") as string;
                if (!string.IsNullOrEmpty(visualStudioPath))
                {
                    Environment.SetEnvironmentVariable("VSINSTALLDIR", visualStudioPath);
                    Environment.SetEnvironmentVariable("VisualStudioVersion", @"15.0");
                }
            }
        }
    }
}
