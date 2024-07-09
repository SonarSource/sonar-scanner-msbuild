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

internal class JreCache(
    ILogger logger,
    IDirectoryWrapper directoryWrapper,
    IFileWrapper fileWrapper,
    IChecksum checksum,
    IUnpackerFactory unpackerFactory,
    IOperatingSystemProvider operatingSystemProvider) : IJreCache
{
    public JreCacheResult IsJreCached(string sonarUserHome, JreDescriptor jreDescriptor)
    {
        if (EnsureCacheRoot(sonarUserHome, out var cacheRoot))
        {
            var extractedPath = JreExtractionPath(jreDescriptor, cacheRoot);
            if (directoryWrapper.Exists(extractedPath))
            {
                var extractedJavaExe = Path.Combine(extractedPath, jreDescriptor.JavaPath);
                return fileWrapper.Exists(extractedJavaExe)
                    ? new JreCacheHit(extractedJavaExe)
                    : new JreCacheFailure(string.Format(Resources.ERR_JavaExeNotFoundAtExpectedLocation, extractedJavaExe));
            }
            else
            {
                return new JreCacheMiss();
            }
        }
        return new JreCacheFailure(string.Format(Resources.ERR_CacheDirectoryCouldNotBeCreated, Path.Combine(sonarUserHome, "cache")));
    }

    public async Task<JreCacheResult> DownloadJreAsync(string sonarUserHome, JreDescriptor jreDescriptor, Func<Task<Stream>> jreDownload)
    {
        if (!EnsureCacheRoot(sonarUserHome, out var cacheRoot)
            || EnsureDirectoryExists(JreRootPath(jreDescriptor, cacheRoot)) is not { } jreDownloadPath)
        {
            return new JreCacheFailure(string.Format(Resources.ERR_CacheDirectoryCouldNotBeCreated, JreRootPath(jreDescriptor, JresCacheRoot(sonarUserHome))));
        }
        // If we do not support the archive format, there is no point in downloading. Therefore we bail out early in such a case.
        if (unpackerFactory.Create(directoryWrapper, fileWrapper, operatingSystemProvider, jreDescriptor.Filename) is not { } unpacker)
        {
            return new JreCacheFailure(string.Format(Resources.ERR_JreArchiveFormatNotSupported, jreDescriptor.Filename));
        }
        var downloadTarget = Path.Combine(jreDownloadPath, jreDescriptor.Filename);
        if (fileWrapper.Exists(downloadTarget))
        {
            logger.LogDebug(Resources.MSG_JreAlreadyDownloaded, downloadTarget);
            return ValidateAndUnpackJre(unpacker, downloadTarget, jreDescriptor, cacheRoot);
        }
        else
        {
            return await DownloadAndUnpackJre(unpacker, jreDownloadPath, downloadTarget, jreDescriptor, cacheRoot, jreDownload);
        }
    }

    private async Task<JreCacheResult> DownloadAndUnpackJre(IUnpacker unpacker,
                                                            string jreDownloadPath,
                                                            string downloadTarget,
                                                            JreDescriptor jreDescriptor,
                                                            string cacheRoot,
                                                            Func<Task<Stream>> jreDownload)
    {
        if (await DownloadJre(jreDownloadPath, downloadTarget, jreDownload) is { } exception)
        {
            logger.LogDebug(Resources.ERR_JreDownloadFailed, exception.Message);
            if (fileWrapper.Exists(downloadTarget)) // Even though the download failed, there is a small chance the file was downloaded by another scanner in the meantime.
            {
                logger.LogDebug(Resources.MSG_JreFoundAfterFailedDownload, downloadTarget);
                return ValidateAndUnpackJre(unpacker, downloadTarget, jreDescriptor, cacheRoot);
            }
            return new JreCacheFailure(string.Format(Resources.ERR_JreDownloadFailed, exception.Message));
        }
        else
        {
            return ValidateAndUnpackJre(unpacker, downloadTarget, jreDescriptor, cacheRoot);
        }
    }

    private async Task<Exception> DownloadJre(string jreDownloadPath, string downloadTarget, Func<Task<Stream>> jreDownload)
    {
        logger.LogDebug(Resources.MSG_StartingJreDownload);
        // We download to a temporary file in the right folder.
        // This avoids conflicts, if multiple scanner try to download to the same file.
        var tempFileName = directoryWrapper.GetRandomFileName();
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
                    EnsureClosed(fileStream); // If we do not close  the stream, deleting the file fails with:
                                              // The process cannot access the file '<<path-to-file>>' because it is being used by another process.
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

    private JreCacheResult ValidateAndUnpackJre(IUnpacker unpacker, string jreArchive, JreDescriptor jreDescriptor, string cacheRoot)
    {
        if (ValidateChecksum(jreArchive, jreDescriptor.Sha256))
        {
            return UnpackJre(unpacker, jreArchive, jreDescriptor, cacheRoot);
        }
        else
        {
            try
            {
                logger.LogDebug(Resources.MSG_DeletingMismatchedJreArchive);
                fileWrapper.Delete(jreArchive);
            }
            catch (Exception ex)
            {
                logger.LogDebug(Resources.MSG_DeletingJreArchiveFailure, ex.Message);
            }
            return new JreCacheFailure(Resources.ERR_JreChecksumMissmatch);
        }
    }

    private JreCacheResult UnpackJre(IUnpacker unpacker, string jreArchive, JreDescriptor jreDescriptor, string cacheRoot)
    {
        // We extract the archive to a temporary folder in the right location, to avoid conflicts with other scanners.
        var tempExtractionPath = Path.Combine(JreRootPath(jreDescriptor, cacheRoot), directoryWrapper.GetRandomFileName());
        var finalExtractionPath = JreExtractionPath(jreDescriptor, cacheRoot); // If all goes well, this will be the final folder. We rename the temporary folder to this one.
        try
        {
            logger.LogDebug(Resources.MSG_StartingJreExtraction, jreArchive, tempExtractionPath);
            using var archiveStream = fileWrapper.Open(jreArchive);
            unpacker.Unpack(archiveStream, tempExtractionPath);
            var expectedJavaExeInTempPath = Path.Combine(tempExtractionPath, jreDescriptor.JavaPath);
            if (fileWrapper.Exists(expectedJavaExeInTempPath))
            {
                logger.LogDebug(Resources.MSG_MovingUnpackedJre, tempExtractionPath, finalExtractionPath);
                directoryWrapper.Move(tempExtractionPath, finalExtractionPath);
                logger.LogDebug(Resources.MSG_JreExtractedSucessfully, finalExtractionPath);
                return new JreCacheHit(Path.Combine(finalExtractionPath, jreDescriptor.JavaPath));
            }
            else
            {
                throw new InvalidOperationException(string.Format(Resources.ERR_JreJavaExeMissing, expectedJavaExeInTempPath));
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(Resources.ERR_JreExtractionFailedWithError, ex.Message);
            CleanupFolder(tempExtractionPath);
            return new JreCacheFailure(Resources.ERR_JreExtractionFailed);
        }
    }

    private void CleanupFolder(string tempExtractionPath)
    {
        try
        {
            directoryWrapper.Delete(tempExtractionPath, true);
        }
        catch (Exception ex)
        {
            logger.LogDebug(Resources.ERR_JreExtractionCleanupFailed, tempExtractionPath, ex.Message);
        }
    }

    private bool ValidateChecksum(string downloadTarget, string sha256)
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
            logger.LogDebug(Resources.ERR_JreChecksumCalculationFailed, downloadTarget, ex.Message);
            return false;
        }
    }

    private bool EnsureCacheRoot(string sonarUserHome, out string cacheRootLocation)
    {
        if (EnsureDirectoryExists(JresCacheRoot(sonarUserHome)) is { } cacheRoot)
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

    private static string JresCacheRoot(string sonarUserHome) =>
        Path.Combine(sonarUserHome, "cache");

    private static string JreRootPath(JreDescriptor jreDescriptor, string cacheRoot) =>
        Path.Combine(cacheRoot, jreDescriptor.Sha256);

    private static string JreExtractionPath(JreDescriptor jreDescriptor, string cacheRoot) =>
        Path.Combine(JreRootPath(jreDescriptor, cacheRoot), $"{jreDescriptor.Filename}_extracted");
}
