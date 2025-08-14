﻿/*
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

namespace SonarScanner.MSBuild.PreProcessor.Caching;

public interface IFileCache
{
    string CacheRoot { get; }
    string EnsureCacheRoot();
    string EnsureDirectoryExists(string directory);
    CacheResult IsFileCached(FileDescriptor fileDescriptor);
    string FileRootPath(FileDescriptor jreDescriptor);
    string EnsureDownloadDirectory(FileDescriptor fileDescriptor);
    bool ValidateChecksum(string downloadTarget, string sha256);
    Task<string> EnsureFileDownload(string jreDownloadPath, string downloadTarget, FileDescriptor descriptor, Func<Task<Stream>> download);
    void TryDeleteFile(string tempFile);
}
