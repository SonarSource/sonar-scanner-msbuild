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

public class JreDownloader : CachingDownloader
{
    private readonly IUnpackerFactory unpackerFactory;
    private readonly IFilePermissionsWrapper filePermissionsWrapper;
    private IUnpacker unpacker;

    public JreDownloader(ILogger logger,
                         IDirectoryWrapper directoryWrapper,
                         IFileWrapper fileWrapper,
                         IChecksum checksum,
                         IUnpackerFactory unpackerFactory,
                         IFilePermissionsWrapper filePermissionsWrapper,
                         string sonarUserHome) : base(logger, directoryWrapper, fileWrapper, checksum, sonarUserHome)
    {
        this.unpackerFactory = unpackerFactory;
        this.filePermissionsWrapper = filePermissionsWrapper;
    }

    public override FileResolution IsFileCached(FileDescriptor fileDescriptor)
    {
        if (fileDescriptor is not JreDescriptor jreDescriptor)
        {
            throw new ArgumentException($"{nameof(JreDownloader)} must be used with {nameof(JreDescriptor)}");
        }

        if (EnsureCacheRoot() is not null)
        {
            var extractedPath = JreExtractionPath(jreDescriptor);
            if (directoryWrapper.Exists(extractedPath))
            {
                var extractedJavaExe = Path.Combine(extractedPath, jreDescriptor.JavaPath);
                return fileWrapper.Exists(extractedJavaExe)
                    ? new ResolutionSuccess(extractedJavaExe)
                    : new ResolutionError(string.Format(Resources.ERR_JavaExeNotFoundAtExpectedLocation, extractedJavaExe));
            }
            else
            {
                return new CacheMiss();
            }
        }
        return new ResolutionError(string.Format(Resources.ERR_CacheDirectoryCouldNotBeCreated, Path.Combine(CacheRoot)));
    }

    public ResolutionError CreateUnpacker(FileDescriptor fileDescriptor)
    {
        if (unpackerFactory.Create(logger, directoryWrapper, fileWrapper, filePermissionsWrapper, fileDescriptor.Filename) is { } archiveUnpacker)
        {
            unpacker = archiveUnpacker;
            return null;
        }
        else
        {
            return new ResolutionError(string.Format(Resources.ERR_JreArchiveFormatNotSupported, fileDescriptor.Filename));
        }
    }

    public virtual FileResolution UnpackJre(string jreArchive, JreDescriptor jreDescriptor)
    {
        // We extract the archive to a temporary folder in the right location, to avoid conflicts with other scanners.
        var tempExtractionPath = Path.Combine(FileRootPath(jreDescriptor), directoryWrapper.GetRandomFileName());
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
                return new ResolutionSuccess(Path.Combine(finalExtractionPath, jreDescriptor.JavaPath));
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
            return new ResolutionError(Resources.ERR_JreExtractionFailed);
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
        Path.Combine(FileRootPath(jreDescriptor), $"{jreDescriptor.Filename}_extracted");
}
