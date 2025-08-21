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
using SonarScanner.MSBuild.PreProcessor.Unpacking;

namespace SonarScanner.MSBuild.PreProcessor.JreResolution;

public class JreDownloader
{
    private readonly ILogger logger;
    private readonly CachedDownloader cachedDownloader;
    private readonly IDirectoryWrapper directoryWrapper;
    private readonly IFileWrapper fileWrapper;
    private readonly IUnpacker unpacker;
    private readonly JreDescriptor jreDescriptor;
    private readonly string jreExtractionPath;

    public JreDownloader(ILogger logger,
                         CachedDownloader cachedDownloader,
                         IDirectoryWrapper directoryWrapper,
                         IFileWrapper fileWrapper,
                         IUnpacker unpacker,
                         JreDescriptor jreDescriptor)
    {
        this.logger = logger;
        this.cachedDownloader = cachedDownloader;
        this.directoryWrapper = directoryWrapper;
        this.fileWrapper = fileWrapper;
        this.unpacker = unpacker;
        this.jreDescriptor = jreDescriptor;
        jreExtractionPath = Path.Combine(cachedDownloader.FileRootPath(jreDescriptor), $"{jreDescriptor.Filename}_extracted");
    }

    public virtual CacheResult IsJreCached()
    {
        if (cachedDownloader.EnsureCacheRoot() is not null)
        {
            var extractedPath = jreExtractionPath;
            if (directoryWrapper.Exists(extractedPath))
            {
                var extractedJavaExe = Path.Combine(extractedPath, jreDescriptor.JavaPath);
                return fileWrapper.Exists(extractedJavaExe)
                    ? new CacheHit(extractedJavaExe)
                    : new CacheError(string.Format(Resources.ERR_JavaExeNotFoundAtExpectedLocation, extractedJavaExe));
            }
            else
            {
                return new CacheMiss();
            }
        }
        return new CacheError(string.Format(Resources.ERR_CacheDirectoryCouldNotBeCreated, Path.Combine(cachedDownloader.CacheRoot)));
    }

    public async Task<DownloadResult> DownloadJreAsync(Func<Task<Stream>> jreDownload)
    {
        logger.LogInfo(Resources.MSG_JreDownloadBottleneck, jreDescriptor.Filename);
        var result = await cachedDownloader.DownloadFileAsync(jreDescriptor, jreDownload);
        return result is DownloadSuccess success ? UnpackJre(success.FilePath) : result;
    }

    private DownloadResult UnpackJre(string jreArchive)
    {
        // We extract the archive to a temporary folder in the right location, to avoid conflicts with other scanners.
        var tempExtractionPath = Path.Combine(cachedDownloader.FileRootPath(jreDescriptor), directoryWrapper.GetRandomFileName());
        var finalExtractionPath = jreExtractionPath; // If all goes well, this will be the final folder. We rename the temporary folder to this one.
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
                return new DownloadSuccess(Path.Combine(finalExtractionPath, jreDescriptor.JavaPath));
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
            return new DownloadError(Resources.ERR_JreExtractionFailed);
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
}
