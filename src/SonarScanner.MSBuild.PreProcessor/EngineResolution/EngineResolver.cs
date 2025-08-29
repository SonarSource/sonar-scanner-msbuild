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

public class EngineResolver : IResolver
{
    private const string ScannerEngine = "Scanner Engine";

    private readonly ISonarWebServer server;
    private readonly IRuntime runtime;
    private readonly IChecksum checksum;
    private readonly string sonarUserHome;

    public EngineResolver(ISonarWebServer server,
                          string sonarUserHome,
                          IRuntime runtime,
                          IChecksum checksum = null)
    {
        this.server = server;
        this.sonarUserHome = sonarUserHome;
        this.runtime = runtime;
        this.checksum = checksum ?? ChecksumSha256.Instance;
    }

    public async Task<string> ResolvePath(ProcessedArgs args)
    {
        runtime.Logger.LogDebug(Resources.MSG_Resolver_Resolving, nameof(EngineResolver), ScannerEngine, string.Empty);
        if (args.EngineJarPath is { } localEngine)
        {
            runtime.Logger.LogDebug(Resources.MSG_EngineResolver_UsingLocalEngine, localEngine);
            runtime.Logger.AddTelemetryMessage(TelemetryKeys.NewBootstrappingEnabled, TelemetryValues.NewBootstrappingStatus.Disabled);
            runtime.Logger.AddTelemetryMessage(TelemetryKeys.ScannerEngineDownload, TelemetryValues.ScannerEngineDownloadStatus.UserSupplied);
            return localEngine;
        }
        if (!server.SupportsJreProvisioning) // JRE and sonar engine provisioning were introduced by the same version of SQ Server
        {
            runtime.Logger.LogDebug(Resources.MSG_EngineResolver_NotSupportedByServer);
            runtime.Logger.AddTelemetryMessage(TelemetryKeys.NewBootstrappingEnabled, TelemetryValues.NewBootstrappingStatus.Unsupported);
            return null;
        }
        runtime.Logger.AddTelemetryMessage(TelemetryKeys.NewBootstrappingEnabled, TelemetryValues.NewBootstrappingStatus.Enabled);
        if (await server.DownloadEngineMetadataAsync() is { } metadata)
        {
            if (await ResolveEnginePath(metadata) is { } enginePath)
            {
                return enginePath;
            }
            else
            {
                runtime.Logger.LogDebug(Resources.MSG_Resolver_Resolving, nameof(EngineResolver), ScannerEngine, " Retrying...");
                return await ResolveEnginePath(metadata);
            }
        }
        else
        {
            runtime.Logger.LogDebug(Resources.MSG_EngineResolver_MetadataFailure);
            return null;
        }
    }

    private async Task<string> ResolveEnginePath(EngineMetadata metadata)
    {
        var cachedDownloader = new CachedDownloader(runtime, checksum, metadata.ToDescriptor(), sonarUserHome);
        switch (cachedDownloader.IsFileCached())
        {
            case CacheHit hit:
                runtime.Logger.LogDebug(Resources.MSG_Resolver_CacheHit, nameof(EngineResolver), hit.FilePath);
                runtime.Logger.AddTelemetryMessage(TelemetryKeys.ScannerEngineDownload, TelemetryValues.ScannerEngineDownloadStatus.CacheHit);
                return hit.FilePath;
            case CacheMiss:
                runtime.Logger.LogDebug(Resources.MSG_Resolver_CacheMiss, nameof(EngineResolver), ScannerEngine);
                return await DownloadEngine(cachedDownloader, metadata);
            case CacheError error:
                runtime.Logger.LogDebug(Resources.MSG_Resolver_CacheFailure, nameof(EngineResolver), error.Message);
                runtime.Logger.AddTelemetryMessage(TelemetryKeys.ScannerEngineDownload, TelemetryValues.ScannerEngineDownloadStatus.Failed);
                return null;
            default:
                throw new NotSupportedException("File Resolution is expected to be CacheHit, CacheMiss, or CacheError.");
        }
    }

    private async Task<string> DownloadEngine(CachedDownloader cachedDownloader, EngineMetadata metadata)
    {
        var result = await cachedDownloader.DownloadFileAsync(() => server.DownloadEngineAsync(metadata));
        if (result is DownloadSuccess success)
        {
            runtime.Logger.LogDebug(Resources.MSG_Resolver_DownloadSuccess, nameof(EngineResolver), ScannerEngine, success.FilePath);
            runtime.Logger.AddTelemetryMessage(TelemetryKeys.ScannerEngineDownload, TelemetryValues.ScannerEngineDownloadStatus.Downloaded);
            return success.FilePath;
        }
        else if (result is DownloadError error)
        {
            runtime.Logger.LogDebug(Resources.MSG_Resolver_DownloadFailure, nameof(EngineResolver), error.Message);
            runtime.Logger.AddTelemetryMessage(TelemetryKeys.ScannerEngineDownload, TelemetryValues.ScannerEngineDownloadStatus.Failed);
            return null;
        }
        throw new NotSupportedException("Download result is expected to be DownloadSuccess or DownloadError.");
    }
}
