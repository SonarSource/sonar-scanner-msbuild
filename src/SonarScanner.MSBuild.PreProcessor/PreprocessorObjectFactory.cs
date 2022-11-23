/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn;

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

        /// <summary>
        /// Reference to the SonarQube server to query.
        /// </summary>
        /// <remarks>Cannot be constructed at runtime until the command line arguments have been processed.
        /// Once it has been created, it is stored so the factory can use the same instance when constructing the analyzer provider</remarks>
        private ISonarQubeServer server;

        public PreprocessorObjectFactory(ILogger logger) =>
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public ISonarQubeServer CreateSonarQubeServer(ProcessedArgs args)
        {
            _ = args ?? throw new ArgumentNullException(nameof(args));
            var userName = args.GetSetting(SonarProperties.SonarUserName, null);
            var password = args.GetSetting(SonarProperties.SonarPassword, null);
            var clientCertPath = args.GetSetting(SonarProperties.ClientCertPath, null);
            var clientCertPassword = args.GetSetting(SonarProperties.ClientCertPassword, null);
            var client = CreateHttpClient(userName, password, clientCertPath, clientCertPassword);

            server = new SonarWebService(new WebClientDownloader(client, logger), args.SonarQubeUrl, logger);
            return server;
        }

        public ITargetsInstaller CreateTargetInstaller() =>
            new TargetsInstaller(logger);

        public IAnalyzerProvider CreateRoslynAnalyzerProvider() =>
            new RoslynAnalyzerProvider(new EmbeddedAnalyzerInstaller(EnsureServer(), logger), logger);

        internal static HttpClient CreateHttpClient(string userName, string password, string clientCertPath, string clientCertPassword)
        {
            var handler = new HttpClientHandler();

            if (clientCertPath is not null && clientCertPassword is not null)
            {
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ClientCertificates.Add(new X509Certificate2(clientCertPath, clientCertPassword));
            }

            var client =  new HttpClient(handler);
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

        private ISonarQubeServer EnsureServer() =>
            server ?? throw new InvalidOperationException(Resources.FACTORY_InternalError_MissingServer);

        private static bool IsAscii(string value) =>
            string.IsNullOrWhiteSpace(value)
            || !value.Any(x => x > sbyte.MaxValue);
    }
}
