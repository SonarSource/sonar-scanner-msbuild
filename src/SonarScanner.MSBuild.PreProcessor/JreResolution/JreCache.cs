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
        if (fileCache.EnsureCacheRoot() is not null)
        {
            var extractedPath = JreExtractionPath(jreDescriptor);
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
        var jreDownloadPath = fileCache.EnsureDownloadDirectory(jreDescriptor);
        if (jreDownloadPath is null)
        {
            return new CacheFailure(string.Format(Resources.ERR_CacheDirectoryCouldNotBeCreated, fileCache.FileRootPath(jreDescriptor)));
        }
        // If we do not support the archive format, there is no point in downloading. Therefore we bail out early in such a case.
        if (unpackerFactory.Create(logger, directoryWrapper, fileWrapper, filePermissionsWrapper, jreDescriptor.Filename) is not { } unpacker)
        {
            return new CacheFailure(string.Format(Resources.ERR_JreArchiveFormatNotSupported, jreDescriptor.Filename));
        }
        var downloadTarget = Path.Combine(jreDownloadPath, jreDescriptor.Filename);
        if (await fileCache.EnsureFileDownload(jreDownloadPath, downloadTarget, jreDescriptor, jreDownload) is { } errorMessage)
        {
            return new CacheFailure(errorMessage);
        }
        else
        {
            return UnpackJre(unpacker, downloadTarget, jreDescriptor);
        }
    }

    private CacheResult UnpackJre(IUnpacker unpacker, string jreArchive, JreDescriptor jreDescriptor)
    {
        // We extract the archive to a temporary folder in the right location, to avoid conflicts with other scanners.
        var tempExtractionPath = Path.Combine(fileCache.FileRootPath(jreDescriptor), directoryWrapper.GetRandomFileName());
        var finalExtractionPath = JreExtractionPath(jreDescriptor); // If all goes well, this will be the final folder. We rename the temporary folder to this one.
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

    private string JreExtractionPath(JreDescriptor jreDescriptor) =>
        Path.Combine(fileCache.FileRootPath(jreDescriptor), $"{jreDescriptor.Filename}_extracted");
}
