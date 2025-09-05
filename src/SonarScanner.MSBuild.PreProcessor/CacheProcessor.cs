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

using System.Security.Cryptography;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.PreProcessor.Protobuf;

namespace SonarScanner.MSBuild.PreProcessor;

public sealed class CacheProcessor : IDisposable
{
    private readonly ILogger logger;
    private readonly ISonarWebServer server;
    private readonly ProcessedArgs localSettings;
    private readonly IBuildSettings buildSettings;
    private readonly HashAlgorithm sha256 = new SHA256CryptoServiceProvider();

    public string PullRequestCacheBasePath { get; }
    public string UnchangedFilesPath { get; private set; }

    public CacheProcessor(ISonarWebServer server, ProcessedArgs localSettings, IBuildSettings buildSettings, ILogger logger)
    {
        this.server = server ?? throw new ArgumentNullException(nameof(server));
        this.localSettings = localSettings ?? throw new ArgumentNullException(nameof(localSettings));
        this.buildSettings = buildSettings ?? throw new ArgumentNullException(nameof(buildSettings));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (localSettings.SettingOrDefault(SonarProperties.ProjectBaseDir, NullWhenEmpty(buildSettings.SourcesDirectory) ?? NullWhenEmpty(buildSettings.SonarScannerWorkingDirectory)) is { } path)
        {
            PullRequestCacheBasePath = Path.GetFullPath(path);
        }

        static string NullWhenEmpty(string value) =>
            value == string.Empty ? null : value;
    }

    public async Task Execute()
    {
        logger.LogDebug("Processing analysis cache");
        if (PullRequestCacheBasePath is null)
        {
            logger.LogInfo(Resources.MSG_NoPullRequestCacheBasePath);
        }
        if (await server.DownloadCache(localSettings) is { Count: > 0 } cache)
        {
            logger.LogDebug(Resources.MSG_PullRequestCacheBasePath, PullRequestCacheBasePath);
            ProcessPullRequest(cache);
        }
        else
        {
            logger.LogInfo(Resources.MSG_NoCacheData);
        }
    }

    internal /* for testing */ byte[] ContentHash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open);
        return sha256.ComputeHash(stream);
    }

    internal /* for testing */ void ProcessPullRequest(IList<SensorCacheEntry> cache)
    {
        var invalidPathChars = Path.GetInvalidPathChars();

        var unchangedFiles = cache
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Key.IndexOfAny(invalidPathChars) < 0)
            .Select(x => new { Hash = x.Data, Path = Path.Combine(PullRequestCacheBasePath, x.Key) })
            .Where(x => File.Exists(x.Path) && ContentHash(x.Path).SequenceEqual(x.Hash))
            .Select(x => Path.GetFullPath(x.Path))
            .ToArray();
        if (unchangedFiles.Any())
        {
            UnchangedFilesPath = Path.Combine(buildSettings.SonarConfigDirectory, "UnchangedFiles.txt");
            File.WriteAllLines(UnchangedFilesPath, unchangedFiles);
        }
        logger.LogInfo(Resources.MSG_UnchangedFilesStats, unchangedFiles.Length, cache.Count);
    }

    public void Dispose() =>
        sha256.Dispose();
}
