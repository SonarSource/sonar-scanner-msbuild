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
        foreach (var entry in zipArchive.Entries)
        {
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
            }
        }
    }
}
