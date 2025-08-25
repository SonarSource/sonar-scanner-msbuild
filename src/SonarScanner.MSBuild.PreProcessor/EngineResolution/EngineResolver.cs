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

namespace SonarScanner.MSBuild.PreProcessor.EngineResolution;

public class EngineResolver : IEngineResolver
{
    private readonly ISonarWebServer server;
    private readonly ILogger logger;
    private readonly IChecksum checksum;
    private readonly string sonarUserHome;
    private readonly IDirectoryWrapper directoryWrapper;
    private readonly IFileWrapper fileWrapper;

    public EngineResolver(ISonarWebServer server,
                          ILogger logger,
                          string sonarUserHome,
                          IChecksum checksum = null,
                          IDirectoryWrapper directoryWrapper = null,
                          IFileWrapper fileWrapper = null)
    {
        this.server = server;
        this.logger = logger;
        this.sonarUserHome = sonarUserHome;
        this.checksum = checksum ?? ChecksumSha256.Instance;
        this.directoryWrapper = directoryWrapper ?? DirectoryWrapper.Instance;
        this.fileWrapper = fileWrapper ?? FileWrapper.Instance;
    }

    public async Task<string> ResolveEngine(ProcessedArgs args)
    {
        if (args.EngineJarPath is { } localEngine)
        {
            logger.LogDebug(Resources.MSG_EngineResolver_UsingLocalEngine, localEngine);
            return localEngine;
        }
        if (!server.SupportsJreProvisioning) // JRE and sonar engine provisioning were introduced by the same version of SQ  S
        {
            logger.LogDebug(Resources.MSG_EngineResolver_NotSupportedByServer);
            return null;
        }
        if (await server.DownloadEngineMetadataAsync() is not { } metadata)
        {
            logger.LogDebug(Resources.MSG_EngineResolver_MetadataFailure);
            return null;
        }

        if (await ResolveEnginePath(metadata) is { } enginePath)
        {
            return enginePath;
        }
        else
        {
            logger.LogDebug(Resources.MSG_JreResolver_Resolving, " Retrying...");
            return await ResolveEnginePath(metadata);
        }
    }

    public async Task<string> ResolveEnginePath(EngineMetadata metadata)
    {
        var cachedDownloader = new CachedDownloader(logger, directoryWrapper, fileWrapper, checksum, metadata.ToDescriptor(), sonarUserHome);
        switch (cachedDownloader.IsFileCached())
        {
            case CacheHit hit:
                logger.LogDebug(Resources.MSG_JreResolver_CacheHit, hit.FilePath);
                return hit.FilePath;
            case CacheMiss:
                logger.LogDebug(Resources.MSG_JreResolver_CacheMiss);
                return await DownloadEngine(cachedDownloader, metadata);
            case CacheError error:
                logger.LogDebug(Resources.MSG_JreResolver_CacheFailure, error.Message);
                return null;
            default:
                throw new NotSupportedException("File Resolution is expected to be CacheHit, CacheMiss, or CacheError.");
        }
    }

    public async Task<string> DownloadEngine(CachedDownloader cachedDownloader, EngineMetadata metadata)
    {
        var result = await cachedDownloader.DownloadFileAsync(() => server.DownloadEngineAsync(metadata));
        if (result is DownloadSuccess success)
        {
            logger.LogDebug(Resources.MSG_JreResolver_DownloadSuccess, success.FilePath);
            return success.FilePath;
        }
        else if (result is DownloadError error)
        {
            logger.LogDebug(Resources.MSG_JreResolver_DownloadFailure, error.Message);
            return null;
        }
        throw new NotSupportedException("Download result is expected to be DownloadSuccess or DownloadError.");
    }
}
