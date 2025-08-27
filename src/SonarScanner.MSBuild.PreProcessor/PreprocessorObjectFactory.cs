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

using System.Net;
using SonarScanner.MSBuild.PreProcessor.EngineResolution;
using SonarScanner.MSBuild.PreProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor.JreResolution;
using SonarScanner.MSBuild.PreProcessor.Roslyn;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;
using SonarScanner.MSBuild.PreProcessor.WebServer;

namespace SonarScanner.MSBuild.PreProcessor;

/// <summary>
/// Default implementation of the object factory interface that returns the product implementations of the required classes.
/// </summary>
/// <remarks>
/// Note: the factory is stateful and expects objects to be requested in the order they are used.
/// </remarks>
public class PreprocessorObjectFactory : IPreprocessorObjectFactory
{
    private readonly IRuntime runtime;

    public PreprocessorObjectFactory(IRuntime runtime) =>
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public async Task<ISonarWebServer> CreateSonarWebServer(ProcessedArgs args, IDownloader webDownloader = null, IDownloader apiDownloader = null)
    {
        _ = args ?? throw new ArgumentNullException(nameof(args));
        var userName = args.GetSetting(SonarProperties.SonarToken, null) ?? args.GetSetting(SonarProperties.SonarUserName, null);
        var password = args.GetSetting(SonarProperties.SonarPassword, null);
        var clientCertPath = args.GetSetting(SonarProperties.ClientCertPath, null);
        var clientCertPassword = args.GetSetting(SonarProperties.ClientCertPassword, null);

        if (!ValidateServerUrl(args.ServerInfo.ServerUrl))
        {
            return null;
        }
        webDownloader ??= CreateDownloader(args.ServerInfo.ServerUrl);
        apiDownloader ??= CreateDownloader(args.ServerInfo.ApiBaseUrl);
        if (!await CanAuthenticate(webDownloader))
        {
            return null;
        }

        var serverVersion = await QueryServerVersion(apiDownloader, webDownloader);
        if (!ValidateServerVersion(args.ServerInfo, serverVersion))
        {
            return null;
        }
        if (args.ServerInfo.IsSonarCloud)
        {
            if (string.IsNullOrWhiteSpace(args.Organization))
            {
                runtime.Logger.LogError(Resources.ERR_MissingOrganization);
                runtime.Logger.LogWarning(Resources.WARN_DefaultHostUrlChanged);
                return null;
            }
            return new SonarCloudWebServer(webDownloader, apiDownloader, serverVersion, runtime.Logger, args.Organization, args.HttpTimeout);
        }
        else
        {
            return new SonarQubeWebServer(webDownloader, apiDownloader, serverVersion, runtime.Logger, args.Organization);
        }

        IDownloader CreateDownloader(string baseUrl) =>
            new WebClientDownloaderBuilder(baseUrl, args.HttpTimeout, runtime.Logger)
                .AddAuthorization(userName, password)
                .AddCertificate(clientCertPath, clientCertPassword)
                .AddServerCertificate(args.TruststorePath, args.TruststorePassword)
                .Build();
    }

    public ITargetsInstaller CreateTargetInstaller() =>
        new TargetsInstaller(runtime.Logger);

    public RoslynAnalyzerProvider CreateRoslynAnalyzerProvider(ISonarWebServer server,
                                                               string localCacheTempPath,
                                                               BuildSettings teamBuildSettings,
                                                               IAnalysisPropertyProvider sonarProperties,
                                                               IEnumerable<SonarRule> rules,
                                                               string language) =>
        new(new EmbeddedAnalyzerInstaller(server, localCacheTempPath, runtime.Logger), runtime.Logger, teamBuildSettings, sonarProperties, rules, language);

    public IResolver CreateJreResolver(ISonarWebServer server, string sonarUserHome) =>
        new JreResolver(server, runtime.Logger, ChecksumSha256.Instance, sonarUserHome);

    public IResolver CreateEngineResolver(ISonarWebServer server, string sonarUserHome) =>
        new EngineResolver(server, runtime.Logger, sonarUserHome);

    private bool ValidateServerUrl(string serverUrl)
    {
        if (!Uri.IsWellFormedUriString(serverUrl, UriKind.Absolute))
        {
            runtime.Logger.LogError(Resources.ERR_InvalidSonarHostUrl, serverUrl);
            return false;
        }

        // If the baseUri has relative parts (like "/api"), then the relative part must be terminated with a slash, (like "/api/"),
        // if the relative part of baseUri is to be preserved in the constructed Uri.
        // See: https://learn.microsoft.com/en-us/dotnet/api/system.uri.-ctor?view=net-7.0
        var serverUri = WebUtils.CreateUri(serverUrl);
        if (serverUri.Scheme != Uri.UriSchemeHttp && serverUri.Scheme != Uri.UriSchemeHttps)
        {
            runtime.Logger.LogError(Resources.ERR_MissingUriScheme, serverUrl);
            return false;
        }
        return true;
    }

    private bool ValidateServerVersion(HostInfo serverInfo, Version serverVersion)
    {
        if (serverVersion is null)
        {
            return false;
        }
        // Make sure the server is the one we detected from the user settings
        else if (SonarProduct.IsSonarCloud(serverVersion) != serverInfo.IsSonarCloud)
        {
            var errorMessage = serverInfo.IsSonarCloud
                ? Resources.ERR_DetectedErroneouslySonarCloud
                : Resources.ERR_DetectedErroneouslySonarQube;
            runtime.Logger.LogError(errorMessage);
            return false;
        }
        return true;
    }

    private async Task<Version> QueryServerVersion(IDownloader downloader, IDownloader fallback)
    {
        runtime.Logger.LogDebug(Resources.MSG_FetchingVersion);

        try
        {
            return await QueryVersion(downloader, "analysis/version", LoggerVerbosity.Debug);
        }
        catch
        {
            try
            {
                return await QueryVersion(fallback, "api/server/version", LoggerVerbosity.Info);
            }
            catch
            {
                runtime.Logger.LogError(Resources.ERR_ErrorWhenQueryingServerVersion);
                return null;
            }
        }

        static async Task<Version> QueryVersion(IDownloader downloader, string path, LoggerVerbosity failureVerbosity)
        {
            var contents = await downloader.Download(path, failureVerbosity: failureVerbosity);
            return new Version(contents.Split('-')[0]);
        }
    }

    /// <summary>
    /// Makes a throw-away request to the server to ensure we can properly authenticate.
    /// </summary>
    private async Task<bool> CanAuthenticate(IDownloader downloader)
    {
        var response = await downloader.DownloadResource("api/settings/values?component=unknown");
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            runtime.Logger.LogWarning(Resources.WARN_AuthenticationFailed);
            // This might fail in the scenario where the user does not specify sonar.host.url.
            runtime.Logger.LogWarning(Resources.WARN_DefaultHostUrlChanged);
            return false;
        }
        else
        {
            return true;
        }
    }
}
