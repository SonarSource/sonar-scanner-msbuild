/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

// https://xtranet-sonarsource.atlassian.net/wiki/spaces/LANG/pages/3155001372/Scanner+Bootstrapping
public class JreResolver : IResolver
{
    private readonly ISonarWebServer server;
    private readonly UnpackerFactory unpackerFactory;
    private readonly IChecksum checksum;
    private readonly string sonarUserHome;
    private readonly IRuntime runtime;

    public JreResolver(ISonarWebServer server,
                       IChecksum checksum,
                       string sonarUserHome,
                       IRuntime runtime,
                       UnpackerFactory unpackerFactory = null)
    {
        this.server = server;
        this.checksum = checksum;
        this.sonarUserHome = sonarUserHome;
        this.runtime = runtime;
        this.unpackerFactory = unpackerFactory ?? new UnpackerFactory(runtime);
    }

    public async Task<string> ResolvePath(ProcessedArgs args)
    {
        runtime.LogDebug(Resources.MSG_Resolver_Resolving, nameof(JreResolver), "JRE", string.Empty);
        if (!IsValid(args))
        {
            return null;
        }

        var result = await DownloadJre(args);
        if (result is FileRetrieved retrieved)
        {
            return retrieved.FilePath;
        }

        runtime.LogDebug(Resources.MSG_Resolver_Resolving, nameof(JreResolver), "JRE", " Retrying...");
        var retry = await DownloadJre(args);
        if (retry is FileRetrieved retriedFile)
        {
            return retriedFile.FilePath;
        }
        // The retry can fail before the download is even attempted, e.g. when the metadata could not be retrieved. The first failure is the informative one in that case.
        if ((retry ?? result) is DownloadError error)
        {
            LogDownloadFailure(error);
        }
        return null;
    }

    /// <summary>
    /// Provisioning failures are not fatal, the analysis falls back to a locally installed Java. They are still reported at error level, because a silent
    /// fallback is impossible to diagnose without verbose logs. The stack trace is only relevant when troubleshooting the scanner itself, so it stays at debug level.
    /// </summary>
    private void LogDownloadFailure(DownloadError error)
    {
        runtime.LogError(Resources.ERR_JreResolver_DownloadFailure, error.DetailedMessage);
        if (error.Exception is not null)
        {
            runtime.LogDebug(error.Exception.ToString());
        }
    }

    private async Task<DownloadResult> DownloadJre(ProcessedArgs args)
    {
        var metadata = await server.DownloadJreMetadataAsync(args.OperatingSystem, args.Architecture);
        if (metadata is null)
        {
            runtime.LogDebug(Resources.MSG_Resolver_MetadataFailure, nameof(JreResolver));
            runtime.Telemetry[TelemetryKeys.JreDownload] = TelemetryValues.JreDownload.Failed;
            return null;
        }
        var descriptor = metadata.ToDescriptor();
        var archiveDownloader = new ArchiveDownloader(runtime, unpackerFactory, checksum, sonarUserHome, descriptor);
        return await DownloadJre(archiveDownloader, metadata);
    }

    private async Task<DownloadResult> DownloadJre(ArchiveDownloader archiveDownloader, JreMetadata metadata)
    {
        var result = await archiveDownloader.DownloadAsync(() => server.DownloadJreAsync(metadata));
        switch (result)
        {
            case CacheHit cacheHit:
                runtime.LogDebug(Resources.MSG_Resolver_CacheHit, nameof(JreResolver), cacheHit.FilePath);
                runtime.Telemetry[TelemetryKeys.JreDownload] = TelemetryValues.JreDownload.CacheHit;
                break;
            case Downloaded downloaded:
                runtime.LogDebug(Resources.MSG_Resolver_DownloadSuccess, nameof(JreResolver), "JRE", downloaded.FilePath);
                runtime.LogInfo(Resources.MSG_JreDownloadBottleneck, metadata.Filename);
                runtime.Telemetry[TelemetryKeys.JreDownload] = TelemetryValues.JreDownload.Downloaded;
                break;
            case DownloadError error:
                runtime.LogDebug(Resources.MSG_Resolver_DownloadFailure, nameof(JreResolver), error.Message);
                runtime.Telemetry[TelemetryKeys.JreDownload] = TelemetryValues.JreDownload.Failed;
                break;
            default:
                throw new NotSupportedException("Download result is expected to be FileRetrieved or DownloadError.");
        }
        return result;
    }

    private bool IsValid(ProcessedArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.JavaExePath))
        {
            runtime.LogDebug(Resources.MSG_JreResolver_JavaExePathSet);
            runtime.Telemetry[TelemetryKeys.JreBootstrapping] = TelemetryValues.JreBootstrapping.Disabled;
            runtime.Telemetry[TelemetryKeys.JreDownload] = TelemetryValues.JreDownload.UserSupplied;
            return false;
        }
        if (args.SkipJreProvisioning)
        {
            runtime.LogDebug(Resources.MSG_JreResolver_SkipJreProvisioningSet);
            runtime.Telemetry[TelemetryKeys.JreBootstrapping] = TelemetryValues.JreBootstrapping.Disabled;
            return false;
        }
        if (!server.SupportsJreProvisioning)
        {
            runtime.LogDebug(Resources.MSG_JreResolver_NotSupportedByServer);
            runtime.Telemetry[TelemetryKeys.JreBootstrapping] = TelemetryValues.JreBootstrapping.UnsupportedByServer;
            return false;
        }
        if (string.IsNullOrWhiteSpace(args.OperatingSystem))
        {
            runtime.LogDebug(Resources.MSG_JreResolver_OperatingSystemMissing);
            runtime.Telemetry[TelemetryKeys.JreBootstrapping] = TelemetryValues.JreBootstrapping.UnsupportedNoOS;
            return false;
        }
        if (string.IsNullOrWhiteSpace(args.Architecture))
        {
            runtime.LogDebug(Resources.MSG_JreResolver_ArchitectureMissing);
            runtime.Telemetry[TelemetryKeys.JreBootstrapping] = TelemetryValues.JreBootstrapping.UnsupportedNoArch;
            return false;
        }
        runtime.Telemetry[TelemetryKeys.JreBootstrapping] = TelemetryValues.JreBootstrapping.Enabled;
        return true;
    }
}
