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
using System.IO.Compression;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.JreCaching;

public class ZipUnpack(IDirectoryWrapper directoryWrapper, IFileWrapper fileWrapper) : IUnpack
{
    public void Unpack(Stream archive, string destinationDirectory)
    {
        if (!directoryWrapper.Exists(destinationDirectory))
        {
            directoryWrapper.CreateDirectory(destinationDirectory);
        }
        using var zipArchive = new ZipArchive(archive, ZipArchiveMode.Read);
        // This is the much simpler version, but is not as good testable because it writes to disk directly.
        // zipArchive.ExtractToDirectory(destinationDirectory);
        foreach (var entry in zipArchive.Entries)
        {
            // We need to make sure, that we sanitize entry.FullName
            // https://github.com/dotnet/runtime/blob/2e585aad5fb0a3c55a7e5f80af9e24f87fa9cfb4/src/libraries/System.IO.Compression.ZipFile/src/System/IO/Compression/ZipFileExtensions.ZipArchiveEntry.Extract.cs#L117-L120
            var entryDestination = Path.Combine(destinationDirectory, entry.FullName);
            if (entry.FullName.EndsWith("/"))
            {
                directoryWrapper.CreateDirectory(entryDestination);
            }
            else
            {
                directoryWrapper.CreateDirectory(Path.GetDirectoryName(entryDestination));
                using var destination = fileWrapper.Create(entryDestination);
                using var stream = entry.Open();
                stream.CopyTo(destination);
                // Do we want to support file permission setting for zip as well? The spec only mentions: "File permissions must be preserved when applicable (tar.gz + Unix for example)."
                // in .Net it is implemented like so:
                // https://github.com/dotnet/runtime/blob/2e585aad5fb0a3c55a7e5f80af9e24f87fa9cfb4/src/libraries/System.IO.Compression.ZipFile/src/System/IO/Compression/ZipFileExtensions.ZipArchiveEntry.Extract.cs#L81-L95
            }
        }
    }
}
