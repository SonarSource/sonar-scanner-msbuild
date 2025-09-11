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
        runtime.LogDebug(Resources.MSG_Resolver_Resolving, nameof(EngineResolver), ScannerEngine, string.Empty);
        if (args.EngineJarPath is { } localEngine)
        {
            runtime.LogDebug(Resources.MSG_EngineResolver_UsingLocalEngine, localEngine);
            runtime.Telemetry[TelemetryKeys.NewBootstrappingEnabled] = TelemetryValues.NewBootstrapping.Disabled;
            runtime.Telemetry[TelemetryKeys.ScannerEngineDownload] = TelemetryValues.ScannerEngineDownload.UserSupplied;
            return localEngine;
        }
        if (!server.SupportsJreProvisioning) // JRE and sonar engine provisioning were introduced by the same version of SQ Server
        {
            runtime.LogDebug(Resources.MSG_EngineResolver_NotSupportedByServer);
            runtime.Telemetry[TelemetryKeys.NewBootstrappingEnabled] = TelemetryValues.NewBootstrapping.Unsupported;
            return null;
        }
        runtime.Telemetry[TelemetryKeys.NewBootstrappingEnabled] = TelemetryValues.NewBootstrapping.Enabled;
        if (await server.DownloadEngineMetadataAsync() is { } metadata)
        {
            if (await ResolveEnginePath(metadata) is { } enginePath)
            {
                return enginePath;
            }
            else
            {
                runtime.LogDebug(Resources.MSG_Resolver_Resolving, nameof(EngineResolver), ScannerEngine, " Retrying...");
                return await ResolveEnginePath(metadata);
            }
        }
        else
        {
            runtime.LogDebug(Resources.MSG_EngineResolver_MetadataFailure);
            return null;
        }
    }

    private async Task<string> ResolveEnginePath(EngineMetadata metadata)
    {
        var downloader = new CachedDownloader(runtime, checksum, metadata.ToDescriptor(), sonarUserHome);
        switch (await downloader.DownloadFileAsync(() => server.DownloadEngineAsync(metadata)))
        {
            case Downloaded success:
                runtime.LogDebug(Resources.MSG_Resolver_DownloadSuccess, nameof(EngineResolver), ScannerEngine, success.FilePath);
                runtime.Telemetry[TelemetryKeys.ScannerEngineDownload] = TelemetryValues.ScannerEngineDownload.Downloaded;
                return success.FilePath;
            case CacheHit cacheHit:
                runtime.LogDebug(Resources.MSG_Resolver_CacheHit, nameof(EngineResolver), cacheHit.FilePath);
                runtime.Telemetry[TelemetryKeys.ScannerEngineDownload] = TelemetryValues.ScannerEngineDownload.CacheHit;
                return cacheHit.FilePath;
            case DownloadError error:
                runtime.LogDebug(Resources.MSG_Resolver_DownloadFailure, nameof(EngineResolver), error.Message);
                runtime.Telemetry[TelemetryKeys.ScannerEngineDownload] = TelemetryValues.ScannerEngineDownload.Failed;
                return null;
            default:
                throw new NotSupportedException("Download result is expected to be DownloadSuccess, CacheHit or DownloadError.");
        }
    }
}
