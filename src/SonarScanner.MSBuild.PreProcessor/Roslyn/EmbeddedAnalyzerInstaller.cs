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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.Roslyn
{
    /// <summary>
    /// Handles fetching embedded resources from SonarQube plugins
    /// </summary>
    /// <remarks>
    /// <para>
    /// We won't be able to run the analyzers unless the user is using MSBuild 14.0 or later.
    /// However, this code is called during the pre-process stage i.e. we don't know which
    /// version of MSBuild will be used so we have to download the analyzers even if we
    /// can't then use them.
    /// </para>
    /// <para>
    /// The plugin resources are cached locally under %temp%\.sonarqube\.static\[package_version]\[resource]
    /// If the required version is available locally then it will not be downloaded from the
    /// SonarQube server.
    /// </para>
    /// </remarks>
    public class EmbeddedAnalyzerInstaller : IAnalyzerInstaller
    {
        private readonly ISonarQubeServer server;
        private readonly ILogger logger;
        private readonly PluginResourceCache cache;

        public EmbeddedAnalyzerInstaller(ISonarQubeServer server, ILogger logger)
            : this(server, GetLocalCacheDirectory(), logger)
        {
        }

        public EmbeddedAnalyzerInstaller(ISonarQubeServer server, string localCacheDirectory, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(localCacheDirectory))
            {
                throw new ArgumentNullException(nameof(localCacheDirectory));
            }

            this.server = server ?? throw new ArgumentNullException(nameof(server));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            this.logger.LogDebug(RoslynResources.EAI_LocalAnalyzerCache, localCacheDirectory);
            Directory.CreateDirectory(localCacheDirectory); // ensure the cache dir exists

            this.cache = new PluginResourceCache(localCacheDirectory);
        }

        #region IAnalyzerInstaller methods

        public IEnumerable<AnalyzerPlugin> InstallAssemblies(IEnumerable<Plugin> plugins)
        {
            if (plugins == null)
            {
                throw new ArgumentNullException(nameof(plugins));
            }

            if (!plugins.Any())
            {
                this.logger.LogInfo(RoslynResources.EAI_NoPluginsSpecified);
                return Enumerable.Empty<AnalyzerPlugin>(); // nothing to deploy
            }

            this.logger.LogInfo(RoslynResources.EAI_InstallingAnalyzers);

            var analyzerPlugins = new List<AnalyzerPlugin>();
            foreach (var plugin in plugins)
            {
                var files = GetPluginResourceFiles(plugin);
                // Don't add the plugin to the list if it doesn't have any assemblies
                if (files.Any())
                {
                    var analyzerPlugin = new AnalyzerPlugin(plugin.Key, plugin.Version, plugin.StaticResourceName, files);
                    analyzerPlugins.Add(analyzerPlugin);
                }
            }

            return analyzerPlugins;
        }

        #endregion IAnalyzerInstaller methods

        #region Private methods

        /// <summary>
        /// We want the resource cache to be in a well-known location so we can re-use files that have
        /// already been installed (although this won't help for e.g. hosted build agents)
        /// </summary>
        private static string GetLocalCacheDirectory()
        {
            var localCache = Path.Combine(Path.GetTempPath(), ".sonarqube", "resources");
            return localCache;
        }

        private IEnumerable<string> GetPluginResourceFiles(Plugin plugin)
        {
            this.logger.LogDebug(RoslynResources.EAI_ProcessingPlugin, plugin.Key, plugin.Version);

            var cacheDir = this.cache.GetResourceSpecificDir(plugin);

            var allFiles = FetchFilesFromCache(cacheDir);

            if (allFiles.Any())
            {
                this.logger.LogDebug(RoslynResources.EAI_CacheHit, cacheDir);
            }
            else
            {
                this.logger.LogDebug(RoslynResources.EAI_CacheMiss);
                if (FetchResourceFromServer(plugin, cacheDir))
                {
                    allFiles = FetchFilesFromCache(cacheDir);
                    Debug.Assert(allFiles.Any(), "Expecting to find files in cache after successful fetch from server");
                }
            }

            return allFiles;
        }

        private static IEnumerable<string> FetchFilesFromCache(string pluginCacheDir)
        {
            if (Directory.Exists(pluginCacheDir))
            {
                return Directory.GetFiles(pluginCacheDir, "*.*", SearchOption.AllDirectories)
                    .Where(name => !name.EndsWith(".zip"));
            }
            return Enumerable.Empty<string>();
        }

        private bool FetchResourceFromServer(Plugin plugin, string targetDir)
        {
            this.logger.LogDebug(RoslynResources.EAI_FetchingPluginResource, plugin.Key, plugin.Version, plugin.StaticResourceName);

            Directory.CreateDirectory(targetDir);

            var success = this.server.TryDownloadEmbeddedFile(plugin.Key, plugin.StaticResourceName, targetDir).Result;

            if (success)
            {
                var targetFilePath = Path.Combine(targetDir, plugin.StaticResourceName);

                if (IsZipFile(targetFilePath))
                {
                    this.logger.LogDebug(Resources.MSG_ExtractingFiles, targetDir);
                    ZipFile.ExtractToDirectory(targetFilePath, targetDir);
                }
            }
            else
            {
                this.logger.LogWarning(RoslynResources.EAI_PluginResourceNotFound, plugin.Key, plugin.Version, plugin.StaticResourceName);
            }
            return success;
        }

        private static bool IsZipFile(string fileName)
        {
            return string.Equals(".zip", Path.GetExtension(fileName), StringComparison.OrdinalIgnoreCase);
        }

        #endregion Private methods
    }
}
