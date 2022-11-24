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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.PreProcessor.Protobuf;

namespace SonarScanner.MSBuild.PreProcessor
{
    public sealed class CacheProcessor : IDisposable
    {
        private readonly ILogger logger;
        private readonly ISonarQubeServer server;
        private readonly ProcessedArgs localSettings;
        private readonly IBuildSettings buildSettings;
        private readonly HashAlgorithm sha256 = new SHA256Managed();

        public string PullRequestCacheBasePath { get; }
        public string UnchangedFilesPath { get; private set; }

        public CacheProcessor(ISonarQubeServer server, ProcessedArgs localSettings, IBuildSettings buildSettings, ILogger logger)
        {
            this.server = server ?? throw new ArgumentNullException(nameof(server));
            this.localSettings = localSettings ?? throw new ArgumentNullException(nameof(localSettings));
            this.buildSettings = buildSettings ?? throw new ArgumentNullException(nameof(buildSettings));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (localSettings.GetSetting(SonarProperties.ProjectBaseDir, NullWhenEmpty(buildSettings.SourcesDirectory) ?? NullWhenEmpty(buildSettings.SonarScannerWorkingDirectory)) is { } path)
            {
                PullRequestCacheBasePath = Path.GetFullPath(path);
            }

            static string NullWhenEmpty(string value) =>
                value == string.Empty ? null : value;
        }

        public async Task Execute()
        {
            logger.LogDebug("Processing analysis cache");
            if (PullRequestBaseBranch(localSettings) is { } baseBranch)
            {
                if (PullRequestCacheBasePath is null)
                {
                    logger.LogWarning(Resources.WARN_NoPullRequestCacheBasePath);
                }
                else
                {
                    logger.LogInfo(Resources.MSG_Processing_PullRequest_Branch, baseBranch);
                    if (await server.DownloadCache(localSettings.ProjectKey, baseBranch) is { } cache)
                    {
                        ProcessPullRequest(cache);
                    }
                    else
                    {
                        logger.LogInfo(Resources.MSG_NoCacheData);
                    }
                }
            }
            else
            {
                logger.LogDebug(Resources.MSG_Processing_PullRequest_NoBranch);
            }
        }

        internal /* for testing */ byte[] ContentHash(string path)
        {
            using var stream = new FileStream(path, FileMode.Open);
            return sha256.ComputeHash(stream);
        }

        internal /* for testing */ void ProcessPullRequest(AnalysisCacheMsg cache)
        {
            var unchangedFiles = new List<string>();
            foreach (var item in cache.Map)
            {
                var path = Path.Combine(PullRequestCacheBasePath, item.Key);
                if (File.Exists(path) && ContentHash(path).SequenceEqual(item.Value))
                {
                    unchangedFiles.Add(path);
                }
            }
            if (unchangedFiles.Any())
            {
                UnchangedFilesPath = Path.Combine(buildSettings.SonarConfigDirectory, "UnchangedFiles.txt");
                File.WriteAllLines(UnchangedFilesPath, unchangedFiles);
            }
        }

        private static string PullRequestBaseBranch(ProcessedArgs localSettings) =>
            localSettings.AggregateProperties.TryGetProperty(SonarProperties.PullRequestBase, out var baseBranchProperty)
                ? baseBranchProperty.Value
                : null;

        public void Dispose() =>
            sha256.Dispose();
    }
}
