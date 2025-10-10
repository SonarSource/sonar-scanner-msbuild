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
            "2f10fe6ac36213958201a67383c712a587e3843e32ae1edf06f01062d6fd1407", // https://binaries.sonarsource.com/Distribution/sonar-scanner-cli/sonar-scanner-cli-5.0.2.4997.zip.sha256
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
        if (await DownloadScannerCli() is { } scannerCLIPath)
        {
            return scannerCLIPath;
        }
        else
        {
            runtime.LogDebug(Resources.MSG_Resolver_Resolving, nameof(ScannerCliResolver), SonarScannerCLI, " Retrying...");
            return await DownloadScannerCli();
        }
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
                runtime.Telemetry[TelemetryKeys.ScannerCliDownload] = TelemetryValues.ScannerCliDownload.CacheHit;
                return cacheHit.FilePath;
            case Downloaded downloaded:
                runtime.LogDebug(Resources.MSG_Resolver_DownloadSuccess, nameof(ScannerCliResolver), SonarScannerCLI, downloaded.FilePath);
                runtime.Telemetry[TelemetryKeys.ScannerCliDownload] = TelemetryValues.ScannerCliDownload.Downloaded;
                return downloaded.FilePath;
            case DownloadError error:
                runtime.LogDebug(Resources.MSG_Resolver_DownloadFailure, nameof(ScannerCliResolver), error.Message);
                runtime.Telemetry[TelemetryKeys.ScannerCliDownload] = TelemetryValues.ScannerCliDownload.Failed;
                return null;
            default:
                throw new NotSupportedException("Download result is expected to be FileRetrieved or DownloadError.");
        }
    }
}
