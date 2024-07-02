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

using System.IO;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.JreCaching;

internal class JreCache(IDirectoryWrapper directoryWrapper, IFileWrapper fileWrapper) : IJreCache
{
    public JreCacheResult IsJreCached(string sonarUserHome, JreDescriptor jreDescriptor)
    {
        if (EnsureDirectoryExists(sonarUserHome) is { } sonarUserHomeValidated
            && EnsureDirectoryExists(Path.Combine(sonarUserHomeValidated, "cache")) is { } cacheRootLocation)
        {
            var expectedExtractedPath = Path.Combine(cacheRootLocation, jreDescriptor.Sha256, $"{jreDescriptor.Filename}_extracted");
            var expectedExtractedJavaExe = Path.Combine(expectedExtractedPath, jreDescriptor.JavaPath);
            if (directoryWrapper.Exists(expectedExtractedPath))
            {
                return fileWrapper.Exists(expectedExtractedJavaExe)
                    ? new JreCacheHit(expectedExtractedJavaExe)
                    : new JreCacheFailure($"The java executable in the JRE cache could not be found at the expected location '{expectedExtractedJavaExe}'.");
            }
            else
            {
                return new JreCacheMiss();
            }
        }
        return new JreCacheFailure($"The JRE cache directory in '{Path.Combine(sonarUserHome, "cache")}' could not be created.");
    }

    private string EnsureDirectoryExists(string directory)
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
}
