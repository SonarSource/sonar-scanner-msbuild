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
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.JreCaching;

internal class JreCache(IDirectoryWrapper directoryWrapper, IFileWrapper fileWrapper) : IJreCache
{
    public JreCacheResult IsJreCached(string sonarUserHome, JreDescriptor jreDescriptor)
    {
        if (EnsureCacheRoot(sonarUserHome, out var cacheRootLocation))
        {
            var extractedPath = Path.Combine(cacheRoot, jreDescriptor.Sha256, $"{jreDescriptor.Filename}_extracted");
            if (directoryWrapper.Exists(extractedPath))
            {
                var extractedJavaExe = Path.Combine(extractedPath, jreDescriptor.JavaPath);
                return fileWrapper.Exists(extractedJavaExe)
                    ? new JreCacheHit(extractedJavaExe)
                    : new JreCacheFailure($"The java executable in the JRE cache could not be found at the expected location '{extractedJavaExe}'.");
            }
            else
            {
                return new JreCacheMiss();
            }
        }
        return new JreCacheFailure($"The JRE cache directory in '{Path.Combine(sonarUserHome, "cache")}' could not be created.");
    }

    public async Task<JreCacheResult> DownloadJreAsync(string sonarUserHome, JreDescriptor jreDescriptor, Func<Task<Stream>> jreDownload)
    {
        if (EnsureCacheRoot(sonarUserHome, out var cacheRootLocation)
            && EnsureDirectoryExists(Path.Combine(cacheRootLocation, jreDescriptor.Sha256)) is { } shaPath)
        {
            var downloadTarget = Path.Combine(shaPath, jreDescriptor.Filename);
            return fileWrapper.Exists(downloadTarget)
                ? await UnpackJre(downloadTarget, jreDescriptor, cacheRootLocation)
                : await DownloadAndUnpackJre(downloadTarget, jreDescriptor, cacheRootLocation, jreDownload);
        }
        else
        {
            return new JreCacheFailure($"The JRE cache directory in '{Path.Combine(sonarUserHome, "cache", jreDescriptor.Sha256)}' could not be created.");
        }
    }

    private async Task<JreCacheResult> DownloadAndUnpackJre(string downloadTarget, JreDescriptor jreDescriptor, string cacheRootLocation, Func<Task<Stream>> jreDownload) =>
        await DownloadJre(downloadTarget, jreDownload) is { } exception
            ? new JreCacheFailure(string.Format(Resources.ERR_JreDownloadFailed, exception.Message))
            : await UnpackJre(downloadTarget, jreDescriptor, cacheRootLocation);

    private async Task<Exception> DownloadJre(string downloadTarget, Func<Task<Stream>> jreDownload)
    {
        try
        {
            using var fileStream = fileWrapper.Create(downloadTarget);
            using var downloadStream = await jreDownload();
            await downloadStream.CopyToAsync(fileStream);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private async Task<JreCacheResult> UnpackJre(string downloadTarget, JreDescriptor jreDescriptor, string cacheRootLocation)
    {
        return new JreCacheFailure("NotImplemented. The JRE is downloaded, but we still need to check, unpack, and set permissions.");
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

    private bool EnsureCacheRoot(string sonarUserHome, out string cacheRootLocation)
    {
        if (EnsureDirectoryExists(sonarUserHome) is { } sonarUserHomeValidated
            && EnsureDirectoryExists(Path.Combine(sonarUserHomeValidated, "cache")) is { } cacheRoot)
        {
            cacheRootLocation = cacheRoot;
            return true;
        }
        else
        {
            cacheRootLocation = null;
            return false;
        }
    }
}
