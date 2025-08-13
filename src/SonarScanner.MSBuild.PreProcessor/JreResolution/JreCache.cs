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

using SonarScanner.MSBuild.PreProcessor.Caching;
using SonarScanner.MSBuild.PreProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor.Unpacking;

namespace SonarScanner.MSBuild.PreProcessor.JreResolution;

internal class JreCache(
    ILogger logger,
    IFileCache fileCache,
    IDirectoryWrapper directoryWrapper,
    IFileWrapper fileWrapper,
    IUnpackerFactory unpackerFactory,
    IFilePermissionsWrapper filePermissionsWrapper) : IJreCache
{
    public CacheResult IsJreCached(JreDescriptor jreDescriptor)
    {
        if (fileCache.EnsureCacheRoot() is { } cacheRoot)
        {
            var extractedPath = JreExtractionPath(jreDescriptor, cacheRoot);
            if (directoryWrapper.Exists(extractedPath))
            {
                var extractedJavaExe = Path.Combine(extractedPath, jreDescriptor.JavaPath);
                return fileWrapper.Exists(extractedJavaExe)
                    ? new CacheHit(extractedJavaExe)
                    : new CacheFailure(string.Format(Resources.ERR_JavaExeNotFoundAtExpectedLocation, extractedJavaExe));
            }
            else
            {
                return new CacheMiss();
            }
        }
        return new CacheFailure(string.Format(Resources.ERR_CacheDirectoryCouldNotBeCreated, Path.Combine(fileCache.CacheRoot)));
    }

    public async Task<CacheResult> DownloadJreAsync(JreDescriptor jreDescriptor, Func<Task<Stream>> jreDownload)
    {
        if (!(fileCache.EnsureCacheRoot() is { } cacheRoot)
            || fileCache.EnsureDirectoryExists(JreRootPath(jreDescriptor, cacheRoot)) is not { } jreDownloadPath)
        {
            return new CacheFailure(string.Format(Resources.ERR_CacheDirectoryCouldNotBeCreated, JreRootPath(jreDescriptor, fileCache.CacheRoot)));
        }
        // If we do not support the archive format, there is no point in downloading. Therefore we bail out early in such a case.
        if (unpackerFactory.Create(logger, directoryWrapper, fileWrapper, filePermissionsWrapper, jreDescriptor.Filename) is not { } unpacker)
        {
            return new CacheFailure(string.Format(Resources.ERR_JreArchiveFormatNotSupported, jreDescriptor.Filename));
        }
        var downloadTarget = Path.Combine(jreDownloadPath, jreDescriptor.Filename);
        if (fileWrapper.Exists(downloadTarget))
        {
            logger.LogDebug(Resources.MSG_JreAlreadyDownloaded, downloadTarget);
            return ValidateAndUnpackJre(unpacker, downloadTarget, jreDescriptor, cacheRoot);
        }
        else
        {
            return await DownloadValidateAndUnpackJre(unpacker, jreDownloadPath, downloadTarget, jreDescriptor, cacheRoot, jreDownload);
        }
    }

    private async Task<CacheResult> DownloadValidateAndUnpackJre(IUnpacker unpacker,
                                                                 string jreDownloadPath,
                                                                 string downloadTarget,
                                                                 JreDescriptor jreDescriptor,
                                                                 string cacheRoot,
                                                                 Func<Task<Stream>> jreDownload)
    {
        logger.LogDebug(Resources.MSG_StartingJreDownload);
        if (await fileCache.DownloadAndValidateFile(jreDownloadPath, downloadTarget, jreDescriptor, jreDownload) is { } exception)
        {
            logger.LogDebug(Resources.ERR_JreDownloadFailed, exception.Message);
            if (fileWrapper.Exists(downloadTarget)) // Even though the download failed, there is a small chance the file was downloaded by another scanner in the meantime.
            {
                logger.LogDebug(Resources.MSG_JreFoundAfterFailedDownload, downloadTarget);
                return ValidateAndUnpackJre(unpacker, downloadTarget, jreDescriptor, cacheRoot);
            }
            return new CacheFailure(string.Format(Resources.ERR_JreDownloadFailed, exception.Message));
        }
        else
        {
            return UnpackJre(unpacker, downloadTarget, jreDescriptor, cacheRoot);
        }
    }

    private CacheResult ValidateAndUnpackJre(IUnpacker unpacker, string jreArchive, JreDescriptor jreDescriptor, string cacheRoot)
    {
        if (fileCache.ValidateChecksum(jreArchive, jreDescriptor.Sha256))
        {
            return UnpackJre(unpacker, jreArchive, jreDescriptor, cacheRoot);
        }
        else
        {
            fileCache.TryDeleteFile(jreArchive);
            return new CacheFailure(Resources.ERR_JreChecksumMismatch);
        }
    }

    private CacheResult UnpackJre(IUnpacker unpacker, string jreArchive, JreDescriptor jreDescriptor, string cacheRoot)
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
                return new CacheHit(Path.Combine(finalExtractionPath, jreDescriptor.JavaPath));
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
            return new CacheFailure(Resources.ERR_JreExtractionFailed);
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

    private static string JreRootPath(JreDescriptor jreDescriptor, string cacheRoot) =>
        Path.Combine(cacheRoot, jreDescriptor.Sha256);

    private static string JreExtractionPath(JreDescriptor jreDescriptor, string cacheRoot) =>
        Path.Combine(JreRootPath(jreDescriptor, cacheRoot), $"{jreDescriptor.Filename}_extracted");
}
