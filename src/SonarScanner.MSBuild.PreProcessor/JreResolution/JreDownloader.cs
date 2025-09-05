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

public class JreDownloader
{
    private readonly IRuntime runtime;
    private readonly CachedDownloader cachedDownloader;
    private readonly IUnpacker unpacker;
    private readonly JreDescriptor jreDescriptor;
    private readonly string jreExtractionPath;
    private readonly string extractedJavaExe;

    public JreDownloader(IRuntime runtime,
                         IUnpacker unpacker,
                         IChecksum checksum,
                         string sonarUserHome,
                         JreDescriptor jreDescriptor)
    {
        this.runtime = runtime;
        this.unpacker = unpacker;
        this.jreDescriptor = jreDescriptor;

        cachedDownloader = new CachedDownloader(runtime, checksum, jreDescriptor, sonarUserHome);
        jreExtractionPath = $"{cachedDownloader.CacheLocation}_extracted";
        extractedJavaExe = Path.Combine(jreExtractionPath, jreDescriptor.JavaPath);
    }

    public virtual string IsJreCached() =>
        runtime.File.Exists(extractedJavaExe)
            ? extractedJavaExe
            : null;

    public async Task<DownloadResult> DownloadJreAsync(Func<Task<Stream>> jreDownload)
    {
        runtime.LogInfo(Resources.MSG_JreDownloadBottleneck, jreDescriptor.Filename);
        var result = await cachedDownloader.DownloadFileAsync(jreDownload);
        return result is Success success ? UnpackJre(success.FilePath) : result;
    }

    private DownloadResult UnpackJre(string jreArchive)
    {
        // We extract the archive to a temporary folder in the right location, to avoid conflicts with other scanners.
        var tempExtractionPath = Path.Combine(cachedDownloader.FileRootPath, runtime.Directory.GetRandomFileName());
        try
        {
            runtime.LogDebug(Resources.MSG_StartingJreExtraction, jreArchive, tempExtractionPath);
            using var archiveStream = runtime.File.Open(jreArchive);
            unpacker.Unpack(archiveStream, tempExtractionPath);
            var expectedJavaExeInTempPath = Path.Combine(tempExtractionPath, jreDescriptor.JavaPath);
            if (runtime.File.Exists(expectedJavaExeInTempPath))
            {
                runtime.LogDebug(Resources.MSG_MovingUnpackedJre, tempExtractionPath, jreExtractionPath);
                runtime.Directory.Move(tempExtractionPath, jreExtractionPath);
                runtime.LogDebug(Resources.MSG_JreExtractedSucessfully, jreExtractionPath);
                return new Downloaded(extractedJavaExe);
            }
            else
            {
                throw new InvalidOperationException(string.Format(Resources.ERR_JreJavaExeMissing, expectedJavaExeInTempPath));
            }
        }
        catch (Exception ex)
        {
            runtime.LogDebug(Resources.ERR_JreExtractionFailedWithError, ex.Message);
            CleanupFolder(tempExtractionPath);
            return new DownloadError(Resources.ERR_JreExtractionFailed);
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
            runtime.LogDebug(Resources.ERR_JreExtractionCleanupFailed, tempExtractionPath, ex.Message);
        }
    }
}
