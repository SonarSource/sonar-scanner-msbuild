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

using System.Security.Cryptography;
using SonarScanner.MSBuild.PreProcessor.Interfaces;

namespace SonarScanner.MSBuild.PreProcessor.Caching;

public class FileCache : IFileCache
{
    private readonly IChecksum checksum;
    private readonly IDirectoryWrapper directoryWrapper;
    private readonly IFileWrapper fileWrapper;
    private readonly ILogger logger;
    private readonly string sonarUserHome;

    public string CacheRoot => Path.Combine(sonarUserHome, "cache");

    public FileCache(ILogger logger, IDirectoryWrapper directoryWrapper, IFileWrapper fileWrapper, IChecksum checksum, string sonarUserHome)
    {
        this.logger = logger;
        this.directoryWrapper = directoryWrapper;
        this.fileWrapper = fileWrapper;
        this.sonarUserHome = sonarUserHome;
        this.checksum = checksum;
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
        return new CacheFailure(string.Format(Resources.ERR_CacheDirectoryCouldNotBeCreated, CacheRoot));
    }

    public string EnsureCacheRoot() =>
        EnsureDirectoryExists(CacheRoot);

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

    public async Task<CacheResult> DownloadFileAsync(FileDescriptor fileDescriptor, Func<Task<Stream>> download)
    {
        if (EnsureDownloadDirectory(fileDescriptor) is { } downloadPath)
        {
            var downloadTarget = Path.Combine(downloadPath, fileDescriptor.Filename);
            return await EnsureFileIsDownloaded(downloadPath, downloadTarget, fileDescriptor, download) is { } cacheFailure
                ? cacheFailure
                : new CacheHit(downloadTarget);
        }
        else
        {
            return new CacheFailure(string.Format(Resources.ERR_CacheDirectoryCouldNotBeCreated, FileRootPath(fileDescriptor)));
        }
    }

    public async Task<CacheFailure> EnsureFileIsDownloaded(string downloadPath, string downloadTarget, FileDescriptor descriptor, Func<Task<Stream>> download)
    {
        if (fileWrapper.Exists(downloadTarget))
        {
            return ValidateFile(downloadTarget, descriptor);
        }
        logger.LogDebug(Resources.MSG_StartingFileDownload);
        if (await DownloadAndValidateFile(downloadPath, downloadTarget, descriptor, download) is { } exception)
        {
            logger.LogDebug(Resources.ERR_DownloadFailed, exception.Message);
            if (fileWrapper.Exists(downloadTarget)) // Even though the download failed, there is a small chance the file was downloaded by another scanner in the meantime.
            {
                logger.LogDebug(Resources.MSG_FileFoundAfterFailedDownload, downloadTarget);
                return ValidateFile(downloadTarget, descriptor);
            }
            return new(string.Format(Resources.ERR_DownloadFailed, exception.Message));
        }
        return null;
    }

    public string EnsureDownloadDirectory(FileDescriptor fileDescriptor) =>
        EnsureCacheRoot() is not null && EnsureDirectoryExists(FileRootPath(fileDescriptor)) is { } downloadPath ? downloadPath : null;

    public bool ValidateChecksum(string downloadTarget, string sha256)
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

    public void TryDeleteFile(string tempFile)
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

    public string FileRootPath(FileDescriptor descriptor) =>
        Path.Combine(CacheRoot, descriptor.Sha256);

    private async Task<Exception> DownloadAndValidateFile(string downloadPath, string downloadTarget, FileDescriptor descriptor, Func<Task<Stream>> download)
    {
        // We download to a temporary file in the correct folder.
        // This avoids conflicts, if multiple scanner try to download to the same file.
        var tempFileName = directoryWrapper.GetRandomFileName();
        var tempFile = Path.Combine(downloadPath, tempFileName);
        try
        {
            using var fileStream = fileWrapper.Create(tempFile);
            try
            {
                using var downloadStream = await download();
                if (downloadStream is null)
                {
                    throw new InvalidOperationException(Resources.ERR_DownloadStreamNull);
                }
                await downloadStream.CopyToAsync(fileStream);
                fileStream.Close();
                if (ValidateChecksum(tempFile, descriptor.Sha256))
                {
                    fileWrapper.Move(tempFile, downloadTarget);
                    return null;
                }
                else
                {
                    throw new CryptographicException(Resources.ERR_ChecksumMismatch);
                }
            }
            catch
            {
                // Cleanup the temp file
                EnsureClosed(fileStream); // If we do not close  the stream, deleting the file fails with:
                                          // The process cannot access the file '<<path-to-file>>' because it is being used by another process.
                TryDeleteFile(tempFile);
                throw;
            }
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private CacheFailure ValidateFile(string downloadTarget, FileDescriptor descriptor)
    {
        logger.LogDebug(Resources.MSG_FileAlreadyDownloaded, downloadTarget);
        if (ValidateChecksum(downloadTarget, descriptor.Sha256))
        {
            return null;
        }
        else
        {
            TryDeleteFile(downloadTarget);
            return new(Resources.ERR_ChecksumMismatch);
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

    private static string CacheLocation(string cacheRoot, FileDescriptor fileDescriptor) =>
        Path.Combine(cacheRoot, fileDescriptor.Sha256, fileDescriptor.Filename);
}
