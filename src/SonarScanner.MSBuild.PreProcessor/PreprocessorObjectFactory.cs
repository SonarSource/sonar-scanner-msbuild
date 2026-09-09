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

using System.Net;
using SonarScanner.MSBuild.PreProcessor.EngineResolution;
using SonarScanner.MSBuild.PreProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor.JreResolution;
using SonarScanner.MSBuild.PreProcessor.Roslyn;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;
using SonarScanner.MSBuild.PreProcessor.SonarQubeClient;

namespace SonarScanner.MSBuild.PreProcessor;

/// <summary>
/// Default implementation of the object factory interface that returns the product implementations of the required classes.
/// </summary>
/// <remarks>
/// Note: the factory is stateful and expects objects to be requested in the order they are used.
/// </remarks>
public class PreprocessorObjectFactory
{
    private readonly IRuntime runtime;

    public PreprocessorObjectFactory(IRuntime runtime) =>
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public virtual async Task<SonarQubeBase> CreateClient(ProcessedArgs args, IDownloader webDownloader = null, IDownloader apiDownloader = null)
    {
        _ = args ?? throw new ArgumentNullException(nameof(args));
        var userName = args.SettingOrDefault(SonarProperties.SonarToken, null) ?? args.SettingOrDefault(SonarProperties.SonarUserName, null);
        var password = args.SettingOrDefault(SonarProperties.SonarPassword, null);
        var clientCertPath = args.SettingOrDefault(SonarProperties.ClientCertPath, null);
        var clientCertPassword = args.SettingOrDefault(SonarProperties.ClientCertPassword, null);

        if (!ValidateServerUrl(args.ServerInfo.ServerUrl))
        {
            return null;
        }
        webDownloader ??= CreateDownloader(args.ServerInfo.ServerUrl);
        apiDownloader ??= CreateDownloader(args.ServerInfo.ApiBaseUrl);
        if (!await VerifyCredentials(webDownloader))
        {
            return null;
        }

        return args.ServerInfo.IsCloud
            ? await SonarQubeCloud.Create(webDownloader, apiDownloader, runtime.Logger, args.Organization, args.HttpTimeout)
            : await SonarQubeServer.Create(webDownloader, apiDownloader, runtime, args.Organization);

        IDownloader CreateDownloader(string baseUrl) =>
            new WebClientDownloaderBuilder(baseUrl, args.HttpTimeout, runtime.Logger)
                .AddAuthorization(userName, password)
                .AddCertificate(clientCertPath, clientCertPassword)
                .AddServerCertificate(args.TruststorePath, args.TruststorePassword)
                .Build();
    }

    public virtual RoslynAnalyzerProvider CreateRoslynAnalyzerProvider(SonarQubeBase client,
                                                                       string localCacheTempPath,
                                                                       BuildSettings teamBuildSettings,
                                                                       IAnalysisPropertyProvider sonarProperties,
                                                                       IEnumerable<SonarRule> rules,
                                                                       string language) =>
        new(new EmbeddedAnalyzerInstaller(client, localCacheTempPath, runtime.Logger), runtime.Logger, teamBuildSettings, sonarProperties, rules, language);

    public virtual IResolver CreateJreResolver(SonarQubeBase client, string sonarUserHome) =>
        new JreResolver(client, ChecksumSha256.Instance, sonarUserHome, runtime);

    public virtual IResolver CreateEngineResolver(SonarQubeBase client, string sonarUserHome) =>
        new EngineResolver(client, sonarUserHome, runtime);

    public virtual IResolver CreateScannerCliResolver(SonarQubeBase client, string sonarUserHome) =>
        new ScannerCliResolver(ChecksumSha256.Instance, sonarUserHome, runtime);

    private bool ValidateServerUrl(string serverUrl)
    {
        if (!Uri.IsWellFormedUriString(serverUrl, UriKind.Absolute))
        {
            runtime.LogError(Resources.ERR_InvalidSonarHostUrl, serverUrl);
            return false;
        }

        // If the baseUri has relative parts (like "/api"), then the relative part must be terminated with a slash, (like "/api/"),
        // if the relative part of baseUri is to be preserved in the constructed Uri.
        // See: https://learn.microsoft.com/en-us/dotnet/api/system.uri.-ctor?view=net-7.0
        var serverUri = WebUtils.CreateUri(serverUrl);
        if (serverUri.Scheme != Uri.UriSchemeHttp && serverUri.Scheme != Uri.UriSchemeHttps)
        {
            runtime.LogError(Resources.ERR_MissingUriScheme, serverUrl);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Makes a throw-away request to the server to ensure we can properly authenticate.
    /// </summary>
    private async Task<bool> VerifyCredentials(IDownloader downloader)
    {
        var response = await downloader.DownloadResource(new("api/settings/values?component=unknown", UriKind.Relative));
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            runtime.LogWarning(Resources.WARN_AuthenticationFailed);
            // This might fail in the scenario where the user does not specify sonar.host.url.
            runtime.LogWarning(Resources.WARN_DefaultHostUrlChanged);
            return false;
        }
        else
        {
            return true;
        }
    }
}
