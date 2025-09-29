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

using System.Net.Http;
using SonarScanner.MSBuild.PreProcessor.Caching;
using SonarScanner.MSBuild.PreProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor.Unpacking;

namespace SonarScanner.MSBuild.PreProcessor;

public class ScannerCliResolver : IResolver
{
    private const string SonarScannerCLI = "SonarScanner CLI";

    private static readonly ArchiveDescriptor Descriptor = new(
            "sonar-scanner-cli-5.0.2.4997.zip",
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Path.Combine("sonar-scanner-5.0.2.4997", "bin", "sonar-scanner"));
    private static readonly Uri ScannerCliUri = new("https://binaries.sonarsource.com/Distribution/sonar-scanner-cli/" + Descriptor.Filename);

    private readonly IChecksum checksum;
    private readonly string sonarUserHome;
    private readonly IRuntime runtime;
    private readonly UnpackerFactory unpackerFactory;
    private readonly HttpClient httpClient;

    public ScannerCliResolver(IChecksum checksum,
                              string sonarUserHome,
                              IRuntime runtime,
                              UnpackerFactory unpackerFactory = null,
                              HttpMessageHandler handler = null)
    {
        this.checksum = checksum;
        this.sonarUserHome = sonarUserHome;
        this.runtime = runtime;
        this.unpackerFactory = unpackerFactory ?? new UnpackerFactory(runtime);
        httpClient = handler is null ? new HttpClient() : new HttpClient(handler, true);
    }

    public async Task<string> ResolvePath(ProcessedArgs args)
    {
        runtime.LogDebug(Resources.MSG_Resolver_Resolving, nameof(ScannerCliResolver), SonarScannerCLI, string.Empty);
        var scannerCliPath = await DownloadScannerCli();
        if (scannerCliPath is null)
        {
            runtime.LogDebug(Resources.MSG_Resolver_Resolving, nameof(ScannerCliResolver), SonarScannerCLI, " Retrying...");
            scannerCliPath = await DownloadScannerCli();
        }
        runtime.OperatingSystem.SetPermission(scannerCliPath, Convert.ToInt32("755", 8));   // read, write and execute for owner; read and execute for group and others SCAN4NET-956
        return scannerCliPath;
    }

    private async Task<string> DownloadScannerCli()
    {
        var archiveDownloader = new ArchiveDownloader(runtime, unpackerFactory, checksum, sonarUserHome, Descriptor);
        return await DownloadScannerCli(archiveDownloader);
    }

    private async Task<string> DownloadScannerCli(ArchiveDownloader archiveDownloader)
    {
        switch (await archiveDownloader.DownloadAsync(() => httpClient.GetStreamAsync(ScannerCliUri)))
        {
            case CacheHit cacheHit:
                runtime.LogDebug(Resources.MSG_Resolver_CacheHit, nameof(ScannerCliResolver), cacheHit.FilePath);
                // runtime.Telemetry[TelemetryKeys.JreDownload] = TelemetryValues.JreDownload.CacheHit; TODO add telemetry SCAN4NET-957
                return cacheHit.FilePath;
            case Downloaded downloaded:
                runtime.LogDebug(Resources.MSG_Resolver_DownloadSuccess, nameof(ScannerCliResolver), SonarScannerCLI, downloaded.FilePath);
                // runtime.Telemetry[TelemetryKeys.JreDownload] = TelemetryValues.JreDownload.Downloaded;   TODO add telemetry SCAN4NET-957
                return downloaded.FilePath;
            case DownloadError error:
                runtime.LogDebug(Resources.MSG_Resolver_DownloadFailure, nameof(ScannerCliResolver), error.Message);
                // runtime.Telemetry[TelemetryKeys.JreDownload] = TelemetryValues.JreDownload.Failed;   TODO add telemetry SCAN4NET-957
                return null;
            default:
                throw new NotSupportedException("Download result is expected to be FileRetrieved or DownloadError.");
        }
    }
}
