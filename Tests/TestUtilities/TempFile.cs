/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

namespace TestUtilities;

public sealed class TempFile : IDisposable
{
    public string FileName { get; }

    public TempFile() : this(null, (Action<FileStream>)null) { }

    public TempFile(string extension) : this(extension, (Action<FileStream>)null) { }

    public TempFile(Action<FileStream> writeToFile) : this(null, writeToFile) { }

    public TempFile(string extension, Action<FileStream> writeToFile)
    {
        FileName = TempFileName(extension);
        if (writeToFile is not null)
        {
            using var stream = new FileStream(FileName, FileMode.Create);
            writeToFile(stream);
        }
    }

    public TempFile(string extension, Action<string> writeToFile)
    {
        FileName = TempFileName(extension);
        if (writeToFile is not null)
        {
            writeToFile(FileName);
        }
    }

    public void Dispose()
    {
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
    }

    private static string TempFileName(string extension) => $"{Path.GetRandomFileName()}{(string.IsNullOrWhiteSpace(extension) ? string.Empty : $".{extension}")}";
}
