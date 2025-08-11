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
using SonarScanner.MSBuild.PreProcessor.Caching;
using SonarScanner.MSBuild.PreProcessor.JreResolution;
using SonarScanner.MSBuild.PreProcessor.Roslyn;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;
using SonarScanner.MSBuild.PreProcessor.Unpacking;
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
    private readonly ILogger logger;

    public PreprocessorObjectFactory(ILogger logger) =>
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
                logger.LogError(Resources.ERR_MissingOrganization);
                logger.LogWarning(Resources.WARN_DefaultHostUrlChanged);
                return null;
            }
            return new SonarCloudWebServer(webDownloader, apiDownloader, serverVersion, logger, args.Organization, args.HttpTimeout);
        }
        else
        {
            return new SonarQubeWebServer(webDownloader, apiDownloader, serverVersion, logger, args.Organization);
        }

        IDownloader CreateDownloader(string baseUrl) =>
            new WebClientDownloaderBuilder(baseUrl, args.HttpTimeout, logger)
                .AddAuthorization(userName, password)
                .AddCertificate(clientCertPath, clientCertPassword)
                .AddServerCertificate(args.TruststorePath, args.TruststorePassword)
                .Build();
    }

    public ITargetsInstaller CreateTargetInstaller() =>
        new TargetsInstaller(logger);

    public RoslynAnalyzerProvider CreateRoslynAnalyzerProvider(ISonarWebServer server,
                                                               string localCacheTempPath,
                                                               ILogger logger,
                                                               BuildSettings teamBuildSettings,
                                                               IAnalysisPropertyProvider sonarProperties,
                                                               IEnumerable<SonarRule> rules,
                                                               string language) =>
        new RoslynAnalyzerProvider(new EmbeddedAnalyzerInstaller(server, localCacheTempPath, logger), logger, teamBuildSettings, sonarProperties, rules, language);

    public IJreResolver CreateJreResolver(ISonarWebServer server)
    {
        var filePermissionsWrapper = new FilePermissionsWrapper(new OperatingSystemProvider(FileWrapper.Instance, logger));
        var fileCache = new FileCache(DirectoryWrapper.Instance, FileWrapper.Instance);
        var cache = new JreCache(logger, fileCache, DirectoryWrapper.Instance, FileWrapper.Instance, ChecksumSha256.Instance, UnpackerFactory.Instance, filePermissionsWrapper);
        return new JreResolver(server, cache, logger);
    }

    private bool ValidateServerUrl(string serverUrl)
    {
        if (!Uri.IsWellFormedUriString(serverUrl, UriKind.Absolute))
        {
            logger.LogError(Resources.ERR_InvalidSonarHostUrl, serverUrl);
            return false;
        }

        // If the baseUri has relative parts (like "/api"), then the relative part must be terminated with a slash, (like "/api/"),
        // if the relative part of baseUri is to be preserved in the constructed Uri.
        // See: https://learn.microsoft.com/en-us/dotnet/api/system.uri.-ctor?view=net-7.0
        var serverUri = WebUtils.CreateUri(serverUrl);
        if (serverUri.Scheme != Uri.UriSchemeHttp && serverUri.Scheme != Uri.UriSchemeHttps)
        {
            logger.LogError(Resources.ERR_MissingUriScheme, serverUrl);
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
            logger.LogError(errorMessage);
            return false;
        }
        return true;
    }

    private async Task<Version> QueryServerVersion(IDownloader downloader, IDownloader fallback)
    {
        logger.LogDebug(Resources.MSG_FetchingVersion);

        try
        {
            return await GetVersion(downloader, "analysis/version", LoggerVerbosity.Debug);
        }
        catch
        {
            try
            {
                return await GetVersion(fallback, "api/server/version", LoggerVerbosity.Info);
            }
            catch
            {
                logger.LogError(Resources.ERR_ErrorWhenQueryingServerVersion);
                return null;
            }
        }

        static async Task<Version> GetVersion(IDownloader downloader, string path, LoggerVerbosity failureVerbosity)
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
            logger.LogWarning(Resources.WARN_AuthenticationFailed);
            // This might fail in the scenario where the user does not specify sonar.host.url.
            logger.LogWarning(Resources.WARN_DefaultHostUrlChanged);
            return false;
        }
        else
        {
            return true;
        }
    }
}
