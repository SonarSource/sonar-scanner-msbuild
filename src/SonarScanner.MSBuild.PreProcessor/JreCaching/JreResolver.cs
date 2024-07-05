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

using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.JreCaching;

// https://xtranet-sonarsource.atlassian.net/wiki/spaces/LANG/pages/3155001372/Scanner+Bootstrapping
internal class JreResolver : IJreResolver
{
    private readonly ISonarWebServer server;
    private readonly IJreCache cache;
    private readonly ILogger logger;

    public JreResolver(ISonarWebServer server, IJreCache cache, ILogger logger)
    {
        this.server = server;
        this.cache = cache;
        this.logger = logger;
    }

    public async Task<string> ResolveJrePath(ProcessedArgs args, string sonarUserHome)
    {
        logger.LogDebug(Resources.MSG_JreResolver_Starting);
        if (!IsValid(args))
        {
            return null;
        }

        var metadata = await server.DownloadJreMetadataAsync(args.OperatingSystem, args.Architecture);
        if (metadata is null)
        {
            logger.LogDebug(Resources.MSG_JreResolver_MetadataFailure);
            return null;
        }

        var descriptor = metadata.ToDescriptor();
        var isCachedResult = cache.IsJreCached(sonarUserHome, descriptor);
        if (isCachedResult is JreCacheHit hit)
        {
            logger.LogDebug(Resources.MSG_JreResolver_CacheHit, hit.JavaExe);
            return hit.JavaExe;
        }
        else if (isCachedResult is JreCacheMiss)
        {
            logger.LogDebug(Resources.MSG_JreResolver_CacheMiss);
            return await DownloadJre(sonarUserHome, metadata, descriptor);
        }
        else
        {
            logger.LogDebug(Resources.MSG_JreResolver_CacheFailure);
            return null;
        }
    }

    private async Task<string> DownloadJre(string sonarUserHome, JreMetadata metadata, JreDescriptor descriptor, bool retry = true)
    {
        var retrying = retry ? string.Empty : " Retrying...";
        logger.LogDebug(Resources.MSG_JreResolver_DownloadAttempt, retrying);
        var result = await cache.DownloadJreAsync(sonarUserHome, descriptor, () => server.DownloadJreAsync(metadata));
        if (result is JreCacheHit hit)
        {
            logger.LogDebug(Resources.MSG_JreResolver_DownloadSuccess, hit.JavaExe);
            return hit.JavaExe;
        }
        else
        {
            return retry
                ? await DownloadJre(sonarUserHome, metadata, descriptor, false)
                : null;
        }
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
