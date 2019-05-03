/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using SonarScanner.MSBuild.Common;

// HACK: Workaround for VSTS-179

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

namespace SonarScanner.MSBuild.TFS
{
    internal interface IBuildVNextCoverageSearchFallback
    {
        IEnumerable<string> FindCoverageFiles();
    }


    internal class BuildVNextCoverageSearchFallback : IBuildVNextCoverageSearchFallback
    {
        internal const string AGENT_TEMP_DIRECTORY = "AGENT_TEMPDIRECTORY";

        public BuildVNextCoverageSearchFallback(ILogger logger)
        {
            this.Logger = logger;
        }

        private ILogger Logger { get; }

        public IEnumerable<string> FindCoverageFiles()
        {
            Logger.LogInfo("Falling back on locating coverage files in the agent temp directory.");

            var agentTempDirectory = GetAgentTempDirectory();
            if (agentTempDirectory == null)
            {
                return Enumerable.Empty<string>();
            }

            Logger.LogInfo($"Searching for coverage files in {agentTempDirectory}");
            var files = Directory.GetFiles(agentTempDirectory, "*.coverage", SearchOption.AllDirectories);

            if (files == null || files.Length == 0)
            {
                Logger.LogInfo($"No coverage files found in the agent temp directory.");
                return Enumerable.Empty<string>();
            }
            else
            {
                LogDebugFileList("All matching files:", files);

                // The same file might appear in multiple paths.
                // We're assuming the files are identical so it doesn't matter which one we pick.
                files = files.Distinct(new FileNameComparer()).ToArray();
                LogDebugFileList("Unique coverage files:", files);
                return files;
            }
        }

        internal /* for testing */ string GetAgentTempDirectory()
        {
            var agentTempDirectory = Environment.GetEnvironmentVariable(AGENT_TEMP_DIRECTORY);
            if (string.IsNullOrEmpty(agentTempDirectory))
            {
                Logger.LogDebug($"Env var {AGENT_TEMP_DIRECTORY} is not set.");
                return null;
            }

            if (!Directory.Exists(agentTempDirectory))
            {
                Logger.LogDebug($"Calculated location for {AGENT_TEMP_DIRECTORY} does not exist: {agentTempDirectory}");
                return null;
            }

            return agentTempDirectory;
        }

        private void LogDebugFileList(string headerMessage, string[] files)
        {
            Logger.LogDebug($"{headerMessage} count={files.Length}");
            foreach (var file in files)
            {
                Logger.LogDebug($"\t{file}");
            }
        }

        /// <summary>
        /// Compares full file paths based on just the file name part
        /// i.e. c:\aaa\bbb\file1.txt and c:\aaa\file1.txt are equal.
        /// </summary>
        internal class FileNameComparer : IEqualityComparer<string>
        {
            public bool Equals(string x, string y)
            {
                bool result = string.Equals(GetFileNameFromPath(x), GetFileNameFromPath(y), StringComparison.OrdinalIgnoreCase);
                return result;
            }

            public int GetHashCode(string obj)
            {
                return GetFileNameFromPath(obj).GetHashCode();
            }

            private static string GetFileNameFromPath(string fullPath) =>
                Path.GetFileName(fullPath).ToUpperInvariant();
        }

    }
}
