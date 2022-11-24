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
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Protobuf;

namespace SonarScanner.MSBuild.PreProcessor
{
    public sealed class CacheProcessor : IDisposable
    {
        private readonly ILogger logger;
        private readonly ISonarQubeServer server;
        private readonly ProcessedArgs settings;
        private readonly HashAlgorithm sha256 = new SHA256Managed();

        public string UnchangedFilesPath { get; }

        public CacheProcessor(ISonarQubeServer server, ProcessedArgs settings, ILogger logger)
        {
            this.server = server ?? throw new ArgumentNullException(nameof(server));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Execute()
        {
            logger.LogDebug("Processing analysis cache");
            // if (IsPullRequest() && DownloadPullRequestCache() is { } cache)
            // {
            //      ProcessPullRequest(cache);
            // }
        }

        internal /* for testing */ byte[] ContentHash(string path)
        {
            using var stream = new FileStream(path, FileMode.Open);
            return sha256.ComputeHash(stream);
        }

        //private void ProcessPullRequestCache()
        //{
        // ToDo: Deserialize
        // ToDo: Hash
        // ToDo: UnchangedFiles
        // ToDo: Save to disk -> "...\UnchangedFiles.txt"
        // ToDo: Populate AnalysisConfig
        //}

        // ToDo: Move IsPullRequest here

        public void Dispose() =>
            sha256.Dispose();
    }
}
