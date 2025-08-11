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

public class FileCache : IFileCache
{
    private readonly IDirectoryWrapper directoryWrapper;
    private readonly IFileWrapper fileWrapper;
    private readonly string sonarUserHome;

    public FileCache(IDirectoryWrapper directoryWrapper, IFileWrapper fileWrapper, string sonarUserHome)
    {
        this.directoryWrapper = directoryWrapper;
        this.fileWrapper = fileWrapper;
        this.sonarUserHome = sonarUserHome;
    }

    public CacheResult IsFileCached(FileDescriptor fileDescriptor)
    {
        if (EnsureCacheRoot() is { } cacheRoot)
        {
            var cacheLocation = CacheLocation(cacheRoot, fileDescriptor);
            return fileWrapper.Exists(cacheLocation) // We do not check the SHA256 of the found file.
                ? new CacheHit(cacheLocation)
                : new CacheMiss();
        }
        return new CacheFailure(string.Format(Resources.ERR_CacheDirectoryCouldNotBeCreated, CacheRoot()));
    }

    public string EnsureCacheRoot(string sonarUserHome) =>
        EnsureDirectoryExists(CacheRoot(sonarUserHome));

    public string EnsureDirectoryExists(string directory)
    {
        try
        {
            if (!directoryWrapper.Exists(directory))
            {
                directoryWrapper.CreateDirectory(directory);
            }
            return directory;
        }
        catch
        {
            return null;
        }
    }

    public string CacheRoot() =>
        Path.Combine(sonarUserHome, "cache");

    private static string CacheLocation(string cacheRoot, FileDescriptor fileDescriptor) =>
        Path.Combine(cacheRoot, fileDescriptor.Sha256, fileDescriptor.Filename);
}
