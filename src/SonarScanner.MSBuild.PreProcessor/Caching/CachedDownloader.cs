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

using SonarScanner.MSBuild.PreProcessor.Interfaces;

namespace SonarScanner.MSBuild.PreProcessor.Caching;

public class CachedDownloader
{
    private readonly IChecksum checksum;
    private readonly IRuntime runtime;
    private readonly FileDescriptor fileDescriptor;

    public string FileRootPath { get; }
    public string CacheLocation { get; }

    public CachedDownloader(IRuntime runtime, IChecksum checksum, FileDescriptor fileDescriptor, string sonarUserHome)
    {
        this.runtime = runtime;
        this.checksum = checksum;
        this.fileDescriptor = fileDescriptor;

        FileRootPath = Path.Combine(sonarUserHome, "cache", fileDescriptor.Sha256);
        CacheLocation = Path.Combine(FileRootPath, fileDescriptor.Filename);
    }

    public virtual async Task<DownloadResult> DownloadFileAsync(Func<Task<Stream>> download)
    {
        if (EnsureDirectoryExists() is { } createDirectoryError)
        {
            return createDirectoryError;
        }
        if (CheckCache() is { } cacheHit)
        {
            return cacheHit;
        }
        runtime.LogDebug(Resources.MSG_Downloader_CacheMiss, CacheLocation);
        return await DownloadFile(download);
    }

    private DownloadError EnsureDirectoryExists()
    {
        try
        {
            if (!runtime.Directory.Exists(FileRootPath))
            {
                runtime.Directory.CreateDirectory(FileRootPath);
            }
            return null;
        }
        catch
        {
            return new DownloadError(string.Format(Resources.MSG_DirectoryCouldNotBeCreated, FileRootPath));
        }
    }

    private CacheHit CheckCache()
    {
        if (runtime.File.Exists(CacheLocation))
        {
            runtime.LogDebug(Resources.MSG_FileAlreadyDownloaded, CacheLocation);
            if (ValidateFile(CacheLocation) is null)
            {
                return new CacheHit(CacheLocation);
            }
        }
        return null;
    }

    private async Task<DownloadResult> DownloadFile(Func<Task<Stream>> download)
    {
        if (await DownloadAndValidate(download) is { } downloadError)
        {
            runtime.LogDebug(downloadError.Message);
            if (runtime.File.Exists(CacheLocation)) // Even though the download failed, there is a small chance the file was downloaded by another scanner in the meantime.
            {
                runtime.LogDebug(Resources.MSG_FileFoundAfterFailedDownload, CacheLocation);
                return ValidateFile(CacheLocation) is { } validationError
                    ? validationError
                    : new Downloaded(CacheLocation);
            }
            return downloadError;
        }
        return new Downloaded(CacheLocation);
    }

    private async Task<DownloadError> DownloadAndValidate(Func<Task<Stream>> download)
    {
        // We download to a temporary file in the correct folder.
        // This avoids conflicts, if multiple scanner try to download to the same file.
        var tempFileName = runtime.Directory.GetRandomFileName();
        var tempFile = Path.Combine(FileRootPath, tempFileName);
        try
        {
            using var fileStream = runtime.File.Create(tempFile);
            using var downloadStream = await download() ?? throw new InvalidOperationException(Resources.ERR_DownloadStreamNull);
            await downloadStream.CopyToAsync(fileStream);
            fileStream.Close();
            if (ValidateFile(tempFile) is { } error)
            {
                return new(string.Format(Resources.ERR_DownloadFailed, error.Message));
            }
            else
            {
                runtime.File.Move(tempFile, CacheLocation);
                return null;
            }
        }
        catch (Exception e)
        {
            TryDeleteFile(tempFile);
            return new(string.Format(Resources.ERR_DownloadFailed, e.Message));
        }
    }

    private DownloadError ValidateFile(string file)
    {
        if (ValidateChecksum(file, fileDescriptor.Sha256))
        {
            return null;
        }
        else
        {
            TryDeleteFile(file);
            return new(Resources.ERR_ChecksumMismatch);
        }
    }

    private bool ValidateChecksum(string downloadTarget, string sha256)
    {
        try
        {
            using var fs = runtime.File.Open(downloadTarget);
            var fileChecksum = checksum.ComputeHash(fs);
            runtime.LogDebug(Resources.MSG_FileChecksum, fileChecksum, sha256);
            return string.Equals(fileChecksum, sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            runtime.LogDebug(Resources.ERR_ChecksumCalculationFailed, downloadTarget, ex.Message);
            return false;
        }
    }

    private void TryDeleteFile(string tempFile)
    {
        try
        {
            runtime.LogDebug(Resources.MSG_DeletingFile, tempFile);
            runtime.File.Delete(tempFile);
        }
        catch (Exception ex)
        {
            runtime.LogDebug(Resources.MSG_DeletingFileFailure, tempFile, ex.Message);
        }
    }
}
