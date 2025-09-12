﻿/*
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

// https://xtranet-sonarsource.atlassian.net/wiki/spaces/LANG/pages/3155001372/Scanner+Bootstrapping
public class JreResolver
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

    public virtual async Task<string> ResolvePath(ProcessedArgs args)
    {
        runtime.LogDebug(Resources.MSG_Resolver_Resolving, nameof(JreResolver), "JRE", string.Empty);
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
            runtime.LogDebug(Resources.MSG_Resolver_Resolving, nameof(JreResolver), "JRE", " Retrying...");
            return await DownloadJre(args);
        }
    }

    private async Task<string> DownloadJre(ProcessedArgs args)
    {
        var metadata = await server.DownloadJreMetadataAsync(args.OperatingSystem, args.Architecture);
        if (metadata is null)
        {
            runtime.LogDebug(Resources.MSG_Resolver_MetadataFailure, nameof(JreResolver));
            runtime.Telemetry[TelemetryKeys.JreDownload] = TelemetryValues.JreDownload.Failed;
            return null;
        }

        var descriptor = metadata.ToDescriptor();
        if (unpackerFactory.Create(descriptor.Filename) is { } unpacker)
        {
            var jreDownloader = new JreDownloader(runtime, unpacker, checksum, sonarUserHome, descriptor);
            if (jreDownloader.IsJreCached() is { } filePath)
            {
                runtime.LogDebug(Resources.MSG_Resolver_CacheHit, nameof(JreResolver), filePath);
                runtime.Telemetry[TelemetryKeys.JreDownload] = TelemetryValues.JreDownload.CacheHit;
                return filePath;
            }
            else
            {
                runtime.LogDebug(Resources.MSG_Resolver_CacheMiss, "JRE");
                return await DownloadJre(jreDownloader, metadata);
            }
        }
        else
        {
            runtime.LogDebug(Resources.MSG_Resolver_CacheFailure, nameof(JreResolver), string.Format(Resources.ERR_JreArchiveFormatNotSupported, descriptor.Filename));
            runtime.Telemetry[TelemetryKeys.JreDownload] = TelemetryValues.JreDownload.Failed;
            return null;
        }
    }

    private async Task<string> DownloadJre(JreDownloader jreDownloader, JreMetadata metadata)
    {
        switch (await jreDownloader.DownloadJreAsync(() => server.DownloadJreAsync(metadata)))
        {
            case FileRetrieved success:
                runtime.LogDebug(Resources.MSG_Resolver_DownloadSuccess, nameof(JreResolver), "JRE", success.FilePath);
                runtime.Telemetry[TelemetryKeys.JreDownload] = TelemetryValues.JreDownload.Downloaded;
                return success.FilePath;
            case DownloadError error:
                runtime.LogDebug(Resources.MSG_Resolver_DownloadFailure, nameof(JreResolver), error.Message);
                runtime.Telemetry[TelemetryKeys.JreDownload] = TelemetryValues.JreDownload.Failed;
                return null;
            default:
                throw new NotSupportedException("Download result is expected to be FileRetrieved or DownloadError.");
        }
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
