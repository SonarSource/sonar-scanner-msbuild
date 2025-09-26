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

namespace SonarScanner.MSBuild.PreProcessor;

public class ArchiveDownloader
{
    private readonly IRuntime runtime;
    private readonly CachedDownloader cachedDownloader;
    private readonly IUnpacker unpacker;
    private readonly ArchiveDescriptor archiveDescriptor;
    private readonly string archiveExtractionPath;
    private readonly string extractedTargetFile;

    public ArchiveDownloader(IRuntime runtime,
                             IChecksum checksum,
                             string sonarUserHome,
                             ArchiveDescriptor archiveDescriptor,
                             UnpackerFactory unpackerFactory)
    {
        this.runtime = runtime;
        this.archiveDescriptor = archiveDescriptor;

        unpacker = unpackerFactory.Create(archiveDescriptor.Filename);
        cachedDownloader = new CachedDownloader(runtime, checksum, archiveDescriptor, sonarUserHome);
        archiveExtractionPath = $"{cachedDownloader.CacheLocation}_extracted";
        extractedTargetFile = Path.Combine(archiveExtractionPath, archiveDescriptor.TargetFilePath);
    }

    public virtual string IsTargetFileCached() =>
        runtime.File.Exists(extractedTargetFile)
            ? extractedTargetFile
            : null;

    public async Task<DownloadResult> DownloadAsync(Func<Task<Stream>> downloadStream)
    {
        if (unpacker is null)
        {
            return new DownloadError(string.Format(Resources.ERR_ArchiveFormatNotSupported, archiveDescriptor.Filename));
        }
        var result = await cachedDownloader.DownloadFileAsync(downloadStream);
        return result is FileRetrieved success ? UnpackArchive(success.FilePath) : result;
    }

    private DownloadResult UnpackArchive(string archiveFile)
    {
        // We extract the archive to a temporary folder in the right location, to avoid conflicts with other scanners.
        var tempExtractionPath = Path.Combine(cachedDownloader.FileRootPath, runtime.Directory.GetRandomFileName());
        try
        {
            runtime.LogDebug(Resources.MSG_StartingArchiveExtraction, archiveFile, tempExtractionPath);
            using var archiveStream = runtime.File.Open(archiveFile);
            unpacker.Unpack(archiveStream, tempExtractionPath);
            var expectedTargetFileInTempPath = Path.Combine(tempExtractionPath, archiveDescriptor.TargetFilePath);
            if (runtime.File.Exists(expectedTargetFileInTempPath))
            {
                runtime.LogDebug(Resources.MSG_MovingUnpackedFiles, tempExtractionPath, archiveExtractionPath);
                runtime.Directory.Move(tempExtractionPath, archiveExtractionPath);
                runtime.LogDebug(Resources.MSG_ArchiveExtractedSucessfully, archiveExtractionPath);
                return new Downloaded(extractedTargetFile);
            }
            else
            {
                throw new InvalidOperationException(string.Format(Resources.ERR_ArchiveTargetFileMissing, expectedTargetFileInTempPath));
            }
        }
        catch (Exception ex)
        {
            runtime.LogDebug(Resources.ERR_ExtractionFailedWithError, ex.Message);
            CleanupFolder(tempExtractionPath);
            return new DownloadError(Resources.ERR_ExtractionFailed);
        }
    }

    private void CleanupFolder(string tempExtractionPath)
    {
        try
        {
            runtime.Directory.Delete(tempExtractionPath, true);
        }
        catch (Exception ex)
        {
            runtime.LogDebug(Resources.ERR_ExtractionCleanupFailed, tempExtractionPath, ex.Message);
        }
    }
}
