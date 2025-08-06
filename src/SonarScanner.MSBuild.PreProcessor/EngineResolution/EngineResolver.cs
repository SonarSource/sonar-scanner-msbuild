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

namespace SonarScanner.MSBuild.PreProcessor.EngineResolution;

public class EngineResolver : IEngineResolver
{
    private readonly ISonarWebServer server;
    private readonly IFileCache fileCache;
    private readonly ILogger logger;

    public EngineResolver(ISonarWebServer server, IFileCache fileCache, ILogger logger)
    {
        this.server = server;
        this.fileCache = fileCache;
        this.logger = logger;
    }

    public async Task<string> ResolveEngine(ProcessedArgs args, string sonarUserHome)
    {
        if (args.EngineJarPath is { } enginePath)
        {
            logger.LogDebug(Resources.MSG_EngineResolver_UsingLocalEngine, enginePath);
            return enginePath;
        }
        if (!server.SupportsJreProvisioning)
        {
            logger.LogDebug(Resources.MSG_EngineResolver_NotSupportedByServer);
            return null;
        }
        var metadata = await server.DownloadEngineMetadataAsync();
        if (metadata is null)
        {
            logger.LogDebug(Resources.MSG_EngineResolver_MetadataFailure);
            return null;
        }
        switch(fileCache.IsFileCached(sonarUserHome, metadata.ToDescriptor()))
        {
            case CacheHit hit:
                // logger.LogDebug(Resources.MSG_EngineResolver_CacheHit, hit.FilePath);
                return hit.FilePath;
            case CacheMiss:
                // logger.LogDebug(Resources.MSG_EngineResolver_CacheMiss);
                // var result = await fileCache.DownloadFileAsync(sonarUserHome, metadata.ToDescriptor(), () => server.DownloadEngineAsync(metadata));
                // if (result is CacheHit downloadHit)
                // {
                //     logger.LogDebug(Resources.MSG_EngineResolver_DownloadSuccess, downloadHit.FilePath);
                //     return downloadHit.FilePath;
                // }
                // logger.LogDebug(Resources.MSG_EngineResolver_DownloadFailure, result.Message);
                return null;
            case CacheFailure failure:
                // logger.LogDebug(Resources.MSG_EngineResolver_CacheFailure, failure.Message);
                return null;
        }
        return null;
    }
}
