﻿/*
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

// HACK: Workaround for SONARAZDO-179

// v2 of the AzureDevOps VSTest task may delete the .trx files from disc after it has uploaded them
// (it does this when using the distributed task runner (?) e.g. when running tests in parallel).
// We normally read .trx files to locate the .coverage files but obviously we can't do this if the
// files are not present.
//
// However, it doesn't delete the coverage files, but they will be written to a different location.
// This code is a partial workaround for the bug - if the normal .trx/.coverage process didn't work
// we'll fall back on searching for .coverage files in the secondary location.
// The test results from the .trx file will still be missing, but the code coverage will be found,
// which is more important.
namespace SonarScanner.MSBuild.TFS;

// This class can be rewritten to be methods of BuildVNextCoverageReportProcessor.
internal class BuildVNextCoverageSearchFallback
{
    internal const string AgentTempDirectory = "AGENT_TEMPDIRECTORY";

    private readonly ILogger logger;

    public BuildVNextCoverageSearchFallback(ILogger logger)
    {
        this.logger = logger;
    }

    public IEnumerable<string> FindCoverageFiles()
    {
        logger.LogInfo("Falling back on locating coverage files in the agent temp directory.");

        var agentTempDirectory = CheckAgentTempDirectory();
        if (agentTempDirectory is null)
        {
            return [];
        }

        logger.LogInfo($"Searching for coverage files in {agentTempDirectory}");
        var files = Directory.GetFiles(agentTempDirectory, "*.coverage", SearchOption.AllDirectories);

        if (files is null || files.Length == 0)
        {
            logger.LogInfo("No coverage files found in the agent temp directory.");
            return [];
        }

        LogDebugFileList("All matching files:", files);

        var fileWithContentHashes = files.Select(x =>
            {
                using var fileStream = new FileStream(x, FileMode.Open);
                using var bufferedStream = new BufferedStream(fileStream);
                using var sha = new SHA256CryptoServiceProvider();
                var contentHash = sha.ComputeHash(bufferedStream);
                return new FileWithContentHash(x, contentHash);
            });

        files = fileWithContentHashes
            .Distinct(new FileHashComparer())
            .Select(x => x.FullFilePath)
            .ToArray();

        LogDebugFileList("Unique coverage files:", files);
        return files;
    }

    internal /* for testing */ string CheckAgentTempDirectory()
    {
        var agentTempDirectory = Environment.GetEnvironmentVariable(AgentTempDirectory);
        if (string.IsNullOrEmpty(agentTempDirectory))
        {
            logger.LogDebug($"Env var {AgentTempDirectory} is not set.");
            return null;
        }

        if (!Directory.Exists(agentTempDirectory))
        {
            logger.LogDebug($"Calculated location for {AgentTempDirectory} does not exist: {agentTempDirectory}");
            return null;
        }

        return agentTempDirectory;
    }

    private void LogDebugFileList(string headerMessage, string[] files)
    {
        logger.LogDebug($"{headerMessage} count={files.Length}");
        foreach (var file in files)
        {
            logger.LogDebug($"\t{file}");
        }
    }

    /// <summary>
    /// Compares file name and content hash tuples based on their hashes.
    /// </summary>
    internal class FileHashComparer : IEqualityComparer<FileWithContentHash>
    {
        public bool Equals(FileWithContentHash x, FileWithContentHash y) =>
            x.ContentHash.SequenceEqual(y.ContentHash);

        // We solely rely on `Equals`
        public int GetHashCode(FileWithContentHash obj) => 0;
    }

    internal class FileWithContentHash
    {
        public string FullFilePath { get; }
        public byte[] ContentHash { get; }

        public FileWithContentHash(string fullFilePath, byte[] contentHash)
        {
            FullFilePath = fullFilePath;
            ContentHash = contentHash;
        }
    }
}
