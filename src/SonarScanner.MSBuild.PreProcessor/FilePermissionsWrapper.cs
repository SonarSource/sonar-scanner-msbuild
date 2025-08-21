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

using System.Diagnostics.CodeAnalysis;
using SonarScanner.MSBuild.PreProcessor.Interfaces;

namespace SonarScanner.MSBuild.PreProcessor;

public class FilePermissionsWrapper(OperatingSystemProvider operatingSystemProvider) : IFilePermissionsWrapper
{
    [ExcludeFromCodeCoverage] // We don't have *inx UT images at the time of writing. We tested the functionality manually.
    public void Set(string destinationPath, int mode)
    {
        if (operatingSystemProvider.IsUnix())
        {
            // https://github.com/Jackett/Jackett/blob/master/src/Jackett.Server/Services/FilePermissionService.cs#L27
            using var process = new Process
            {
                StartInfo = new()
                {
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "chmod",
                    Arguments = $"""{Convert.ToString(mode, 8)} "{Path.GetFullPath(destinationPath)}" """,
                }
            };
            process.Start();
            var stdError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(stdError);
            }
        }
    }
}
