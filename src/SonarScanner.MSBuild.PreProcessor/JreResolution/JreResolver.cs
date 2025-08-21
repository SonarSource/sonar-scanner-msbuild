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

namespace SonarScanner.MSBuild.PreProcessor.JreResolution;

// https://xtranet-sonarsource.atlassian.net/wiki/spaces/LANG/pages/3155001372/Scanner+Bootstrapping
public class JreResolver(ISonarWebServer server, JreDownloader downloader, ILogger logger) : IJreResolver
{
    public async Task<string> ResolveJrePath(ProcessedArgs args)
    {
        logger.LogDebug(Resources.MSG_JreResolver_Resolving, string.Empty);
        if (!IsValid(args))
        {
            return null;
        }

        if (await DownloadJre(args) is { } jrePath)
        {
            return jrePath;
        }
        else
        {
            logger.LogDebug(Resources.MSG_JreResolver_Resolving, " Retrying...");
            return await DownloadJre(args);
        }
    }

    private async Task<string> DownloadJre(ProcessedArgs args)
    {
        var metadata = await server.DownloadJreMetadataAsync(args.OperatingSystem, args.Architecture);
        if (metadata is null)
        {
            logger.LogDebug(Resources.MSG_JreResolver_MetadataFailure);
            return null;
        }

        var descriptor = metadata.ToDescriptor();
        var result = downloader.IsJreCached(descriptor);
        switch (result)
        {
            case CacheHit hit:
                logger.LogDebug(Resources.MSG_JreResolver_CacheHit, hit.FilePath);
                return hit.FilePath;
            case CacheMiss:
                logger.LogDebug(Resources.MSG_JreResolver_CacheMiss);
                return await DownloadJre(metadata, descriptor);
            case CacheError failure:
                logger.LogDebug(Resources.MSG_JreResolver_CacheFailure, failure.Message);
                return null;
        }

        throw new NotSupportedException("Cache result is expected to be Hit, Miss, or Failure.");
    }

    private async Task<string> DownloadJre(JreMetadata metadata, JreDescriptor descriptor)
    {
        var result = await downloader.DownloadJreAsync(descriptor, () => server.DownloadJreAsync(metadata));
        if (result is DownloadSuccess hit)
        {
            logger.LogDebug(Resources.MSG_JreResolver_DownloadSuccess, hit.FilePath);
            return hit.FilePath;
        }
        else if (result is DownloadError failure)
        {
            logger.LogDebug(Resources.MSG_JreResolver_DownloadFailure, failure.Message);
            return null;
        }

        throw new NotSupportedException("Download result is expected to be Hit or Failure.");
    }

    private bool IsValid(ProcessedArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.JavaExePath))
        {
            logger.LogDebug(Resources.MSG_JreResolver_JavaExePathSet);
            return false;
        }
        if (args.SkipJreProvisioning)
        {
            logger.LogDebug(Resources.MSG_JreResolver_SkipJreProvisioningSet);
            return false;
        }
        if (!server.SupportsJreProvisioning)
        {
            logger.LogDebug(Resources.MSG_JreResolver_NotSupportedByServer);
            return false;
        }
        if (string.IsNullOrWhiteSpace(args.OperatingSystem))
        {
            logger.LogDebug(Resources.MSG_JreResolver_OperatingSystemMissing);
            return false;
        }
        if (string.IsNullOrWhiteSpace(args.Architecture))
        {
            logger.LogDebug(Resources.MSG_JreResolver_ArchitectureMissing);
            return false;
        }
        return true;
    }
}
