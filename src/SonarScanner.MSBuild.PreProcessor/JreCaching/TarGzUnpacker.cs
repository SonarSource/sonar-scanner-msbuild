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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.JreCaching;

public class TarGzUnpacker(IDirectoryWrapper directoryWrapper, IFileWrapper fileWrapper, IOperatingSystemProvider operatingSystemProvider) : IUnpacker
{
    // ref https://github.com/icsharpcode/SharpZipLib/blob/ff2d7c30bdb2474d507f001bc555405e9f02a0bb/src/ICSharpCode.SharpZipLib/Tar/TarArchive.cs#L608
    public void Unpack(Stream archive, string destinationDirectory)
    {
        using var gzip = new GZipInputStream(archive);
        using var tarIn = new TarInputStream(gzip, null);

        var destinationFullPath = Path.GetFullPath(destinationDirectory).TrimEnd('/', '\\');
        while (tarIn.GetNextEntry() is { } entry)
        {
            if (entry.TarHeader.TypeFlag is not (TarHeader.LF_LINK or TarHeader.LF_SYMLINK))
            {
                ExtractEntry(tarIn, destinationFullPath, entry);
            }
        }
    }

    // ref https://github.com/icsharpcode/SharpZipLib/blob/ff2d7c30bdb2474d507f001bc555405e9f02a0bb/src/ICSharpCode.SharpZipLib/Tar/TarArchive.cs#L644
    private void ExtractEntry(TarInputStream tar, string destinationFullPath, TarEntry entry)
    {
        var name = entry.Name;
        if (Path.IsPathRooted(name))
        {
            // NOTE:
            // for UNC names...  \\machine\share\zoom\beet.txt gives \zoom\beet.txt
            name = name.Substring(Path.GetPathRoot(name).Length);
        }

        name = name.Replace('/', Path.DirectorySeparatorChar);
        var destinationFile = Path.Combine(destinationFullPath, name);
        var destinationFileDirectory = Path.GetDirectoryName(Path.GetFullPath(destinationFile)) ?? string.Empty;
        var isRootDir = entry.IsDirectory && entry.Name == string.Empty;

        if (!isRootDir && !destinationFileDirectory.StartsWith(destinationFullPath, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new InvalidNameException("Parent traversal in paths is not allowed");
        }

        if (entry.IsDirectory)
        {
            directoryWrapper.CreateDirectory(destinationFile);
        }
        else
        {
            directoryWrapper.CreateDirectory(destinationFileDirectory);
            using var outputStream = fileWrapper.Create(destinationFile);
            tar.CopyEntryContents(outputStream);
            SetPermissions(operatingSystemProvider, entry, destinationFile);
        }

        [ExcludeFromCodeCoverage]
        static void SetPermissions(IOperatingSystemProvider operatingSystemProvider, TarEntry source, string destination)
        {
            if (operatingSystemProvider.IsUnix())
            {
                if (operatingSystemProvider.OperatingSystem() is PlatformOS.Alpine)
                {
                    // https://github.com/Jackett/Jackett/blob/master/src/Jackett.Server/Services/FilePermissionService.cs#L27
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            FileName = "chmod",
                            Arguments = $"+arwx \"{destination}\""
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
                else
                {
                    _ = new Mono.Unix.UnixFileInfo(destination)
                    {
                        FileAccessPermissions = (Mono.Unix.FileAccessPermissions)source.TarHeader.Mode // set the same permissions as inside the archive
                    };
                }
            }
        }
    }
}
