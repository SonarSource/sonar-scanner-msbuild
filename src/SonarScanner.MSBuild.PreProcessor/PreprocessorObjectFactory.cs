/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
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

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn;
using SonarScanner.MSBuild.PreProcessor.WebService;

namespace SonarScanner.MSBuild.PreProcessor
{
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

        public async Task<ISonarWebService> CreateSonarWebService(ProcessedArgs args, IDownloader downloader = null)
        {
            _ = args ?? throw new ArgumentNullException(nameof(args));
            var userName = args.GetSetting(SonarProperties.SonarUserName, null);
            var password = args.GetSetting(SonarProperties.SonarPassword, null);
            var clientCertPath = args.GetSetting(SonarProperties.ClientCertPath, null);
            var clientCertPassword = args.GetSetting(SonarProperties.ClientCertPassword, null);
            var client = CreateHttpClient(userName, password, clientCertPath, clientCertPassword);

            // If the baseUri has relative parts (like "/api"), then the relative part must be terminated with a slash, (like "/api/"),
            // if the relative part of baseUri is to be preserved in the constructed Uri.
            // See: https://learn.microsoft.com/en-us/dotnet/api/system.uri.-ctor?view=net-7.0
            var serverUri = WebUtils.CreateUri(args.SonarQubeUrl);
            downloader ??= new WebClientDownloader(client, logger);
            var serverVersion = await QueryServerVersion(serverUri, downloader);

            return SonarProduct.IsSonarCloud(serverUri.Host, serverVersion)
                       ? new SonarCloudWebService(downloader, serverUri, serverVersion, logger)
                       : new SonarQubeWebService(downloader, serverUri, serverVersion, logger);
        }

        public ITargetsInstaller CreateTargetInstaller() =>
            new TargetsInstaller(logger);

        public IAnalyzerProvider CreateRoslynAnalyzerProvider(ISonarWebService server)
        {
            server = server ?? throw new ArgumentNullException(nameof(server));
            return new RoslynAnalyzerProvider(new EmbeddedAnalyzerInstaller(server, logger), logger);
        }

        public static HttpClient CreateHttpClient(string userName, string password, string clientCertPath, string clientCertPassword)
        {
            var handler = new HttpClientHandler();

            if (clientCertPath is not null && clientCertPassword is not null)
            {
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ClientCertificates.Add(new X509Certificate2(clientCertPath, clientCertPassword));
            }

            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SonarScanner-for-.NET", Utilities.ScannerVersion));
            // Wrong "UserAgent" header for backward compatibility. Should be removed as part of https://github.com/SonarSource/sonar-scanner-msbuild/issues/1421
            client.DefaultRequestHeaders.Add(HttpRequestHeader.UserAgent.ToString(), $"ScannerMSBuild/{Utilities.ScannerVersion}");
            if (userName != null)
            {
                if (userName.Contains(':'))
                {
                    throw new ArgumentException(Resources.WCD_UserNameCannotContainColon);
                }
                if (!IsAscii(userName) || !IsAscii(password))
                {
                    throw new ArgumentException(Resources.WCD_UserNameMustBeAscii);
                }

                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }
            return client;
        }

        private async Task<Version> QueryServerVersion(Uri serverUri, IDownloader downloader)
        {
            var uri = new Uri(serverUri, "api/server/version");
            try
            {
                var contents = await downloader.Download(uri);
                return new Version(contents.Split('-').First());
            }
            catch (Exception e)
            {
                logger.LogError("Failed to request and parse '{0}': {1}", uri, e.Message);
                throw;
            }
        }

        private static bool IsAscii(string value) =>
            string.IsNullOrWhiteSpace(value)
            || !value.Any(x => x > sbyte.MaxValue);
    }
}
