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
    private readonly IDirectoryWrapper directoryWrapper;
    private readonly IFileWrapper fileWrapper;
    private readonly ILogger logger;
    private readonly FileDescriptor fileDescriptor;
    private readonly string downloadTarget;

    public string FileRootPath { get; }
    public string CacheLocation { get; }

    public CachedDownloader(IRuntime runtime, IChecksum checksum, FileDescriptor fileDescriptor, string sonarUserHome)
    {
        logger = runtime.Logger;
        directoryWrapper = runtime.Directory;
        fileWrapper = runtime.File;
        this.checksum = checksum;
        this.fileDescriptor = fileDescriptor;

        FileRootPath = Path.Combine(sonarUserHome, "cache", fileDescriptor.Sha256);
        CacheLocation = Path.Combine(FileRootPath, fileDescriptor.Filename);
        downloadTarget = Path.Combine(FileRootPath, fileDescriptor.Filename);
    }

    public virtual string IsFileCached() =>
        fileWrapper.Exists(CacheLocation)   // We do not check the SHA256 of the found file.
            ? CacheLocation
            : null;

    public virtual async Task<DownloadResult> DownloadFileAsync(Func<Task<Stream>> download)
    {
        if (EnsureDirectoryExists(FileRootPath))
        {
            return await EnsureFileIsDownloaded(download) is { } downloadError
                ? downloadError
                : new DownloadSuccess(downloadTarget);
        }
        return new DownloadError(string.Format(Resources.MSG_DirectoryCouldNotBeCreated, FileRootPath));
    }

    internal bool ValidateChecksum(string downloadTarget, string sha256)
    {
        try
        {
            using var fs = fileWrapper.Open(downloadTarget);
            var fileChecksum = checksum.ComputeHash(fs);
            logger.LogDebug(Resources.MSG_FileChecksum, fileChecksum, sha256);
            return string.Equals(fileChecksum, sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogDebug(Resources.ERR_ChecksumCalculationFailed, downloadTarget, ex.Message);
            return false;
        }
    }

    private bool EnsureDirectoryExists(string directory)
    {
        try
        {
            if (!directoryWrapper.Exists(directory))
            {
                directoryWrapper.CreateDirectory(directory);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TryDeleteFile(string tempFile)
    {
        try
        {
            logger.LogDebug(Resources.MSG_DeletingFile, tempFile);
            fileWrapper.Delete(tempFile);
        }
        catch (Exception ex)
        {
            logger.LogDebug(Resources.MSG_DeletingFileFailure, tempFile, ex.Message);
        }
    }

    private async Task<DownloadError> EnsureFileIsDownloaded(Func<Task<Stream>> download)
    {
        if (fileWrapper.Exists(downloadTarget))
        {
            logger.LogDebug(Resources.MSG_FileAlreadyDownloaded, downloadTarget);
            if (ValidateFile(downloadTarget) is null)
            {
                return null;
            }
        }
        logger.LogDebug(Resources.MSG_StartingFileDownload);
        if (await DownloadAndValidateFile(download) is { } error)
        {
            logger.LogDebug(error.Message);
            if (fileWrapper.Exists(downloadTarget)) // Even though the download failed, there is a small chance the file was downloaded by another scanner in the meantime.
            {
                logger.LogDebug(Resources.MSG_FileFoundAfterFailedDownload, downloadTarget);
                return ValidateFile(downloadTarget);
            }
            return error;
        }
        return null;
    }

    private async Task<DownloadError> DownloadAndValidateFile(Func<Task<Stream>> download)
    {
        // We download to a temporary file in the correct folder.
        // This avoids conflicts, if multiple scanner try to download to the same file.
        var tempFileName = directoryWrapper.GetRandomFileName();
        var tempFile = Path.Combine(FileRootPath, tempFileName);
        try
        {
            using var fileStream = fileWrapper.Create(tempFile);
            using var downloadStream = await download() ?? throw new InvalidOperationException(Resources.ERR_DownloadStreamNull);
            await downloadStream.CopyToAsync(fileStream);
            fileStream.Close();
            if (ValidateFile(tempFile) is { } error)
            {
                return new(string.Format(Resources.ERR_DownloadFailed, error.Message));
            }
            else
            {
                fileWrapper.Move(tempFile, downloadTarget);
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
}
