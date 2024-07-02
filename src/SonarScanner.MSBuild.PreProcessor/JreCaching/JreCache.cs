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
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.JreCaching;

internal class JreCache(IDirectoryWrapper directoryWrapper, IFileWrapper fileWrapper) : IJreCache
{
    public JreCacheResult IsJreCached(string sonarUserHome, JreDescriptor jreDescriptor)
    {
        if (EnsureCacheRoot(sonarUserHome, out var cacheRoot))
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
            && EnsureDirectoryExists(Path.Combine(cacheRootLocation, jreDescriptor.Sha256)) is { } jreDownloadPath)
        {
            var downloadTarget = Path.Combine(jreDownloadPath, jreDescriptor.Filename);
            return fileWrapper.Exists(downloadTarget)
                ? await UnpackJre(downloadTarget, jreDescriptor, cacheRootLocation)
                : await DownloadAndUnpackJre(jreDownloadPath, downloadTarget, jreDescriptor, cacheRootLocation, jreDownload);
        }
        else
        {
            return new JreCacheFailure($"The JRE cache directory in '{Path.Combine(sonarUserHome, "cache", jreDescriptor.Sha256)}' could not be created.");
        }
    }

    private async Task<JreCacheResult> DownloadAndUnpackJre(string jreDownloadPath, string downloadTarget, JreDescriptor jreDescriptor, string cacheRootLocation, Func<Task<Stream>> jreDownload)
    {
        if (await DownloadJre(jreDownloadPath, downloadTarget, jreDownload) is { } exception)
        {
            if (fileWrapper.Exists(downloadTarget)) // Even though the download failed, there is a small chance the file was downloaded by another scanner in the meantime.
            {
                return await UnpackJre(downloadTarget, jreDescriptor, cacheRootLocation);
            }
            return new JreCacheFailure(string.Format(Resources.ERR_JreDownloadFailed, exception.Message));
        }
        else
        {
            return await UnpackJre(downloadTarget, jreDescriptor, cacheRootLocation);
        }
    }

    private async Task<Exception> DownloadJre(string jreDownloadPath, string downloadTarget, Func<Task<Stream>> jreDownload)
    {
        // We download to a temporary file in the right folder.
        // This avoids conflicts, if multiple scanner try to download to the same file.
        var tempFileName = Path.GetRandomFileName();
        var tempFile = Path.Combine(jreDownloadPath, tempFileName);
        try
        {
            using var fileStream = fileWrapper.Create(tempFile);
            try
            {
                using var downloadStream = await jreDownload();
                await downloadStream.CopyToAsync(fileStream);
                fileStream.Close();
                fileWrapper.Move(tempFile, downloadTarget);
                return null;
            }
            catch
            {
                try
                {
                    // Cleanup the temp file
                    EnsureClosed(fileStream);
                    fileWrapper.Delete(tempFile);
                }
                catch
                {
                    // Ignore any failures to delete the temp file
                }
                throw;
            }
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static void EnsureClosed(Stream fileStream)
    {
        try
        {
            fileStream.Close();
        }
        catch
        {
            // If closing the file fails, just move on.
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
        if (EnsureDirectoryExists(Path.Combine(sonarUserHome, "cache")) is { } cacheRoot)
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
