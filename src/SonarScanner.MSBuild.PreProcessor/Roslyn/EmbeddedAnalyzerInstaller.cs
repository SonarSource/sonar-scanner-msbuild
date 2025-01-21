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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.Roslyn;

/// <summary>
/// Handles fetching embedded resources from SonarQube plugins.
/// </summary>
/// <remarks>
/// <para>
/// We won't be able to run the analyzers unless the user is using MSBuild 14.0 or later.
/// However, this code is called during the pre-process stage i.e. we don't know which
/// version of MSBuild will be used so we have to download the analyzers even if we
/// can't then use them.
/// </para>
/// <para>
/// The analyzer resources are cached locally under %temp%\.sonarqube\resources\(each analyzer inside a subfolder).
/// The %temp%\.sonarqube\resources\ folder contains a file called index.json which stores the folder to analyzer mapping.
/// If the required version is available locally then it will not be downloaded from the SonarQube/SonarCloud server.
/// </para>
/// </remarks>
public class EmbeddedAnalyzerInstaller : IAnalyzerInstaller
{
    private readonly ISonarWebServer server;
    private readonly ILogger logger;
    private readonly PluginResourceCache cache;

    public EmbeddedAnalyzerInstaller(ISonarWebServer server, ILogger logger) : this(server, GetLocalCacheDirectory(), logger) { }

    public EmbeddedAnalyzerInstaller(ISonarWebServer server, string localCacheDirectory, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(localCacheDirectory))
        {
            // we may end up sending an empty string from the PreProcessor
            // if the user does not specify a custom path, we will need to set it here.
            localCacheDirectory = GetLocalCacheDirectory();
        }

        this.server = server ?? throw new ArgumentNullException(nameof(server));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        this.logger.LogDebug(RoslynResources.EAI_LocalAnalyzerCache, localCacheDirectory);
        Directory.CreateDirectory(localCacheDirectory); // ensure the cache dir exists

        cache = new PluginResourceCache(localCacheDirectory);
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
            logger.LogInfo(RoslynResources.EAI_NoPluginsSpecified);
            return Enumerable.Empty<AnalyzerPlugin>(); // nothing to deploy
        }

        logger.LogInfo(RoslynResources.EAI_InstallingAnalyzers);

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
        logger.LogInfo(RoslynResources.EAI_ProcessingPlugin, plugin.Key, plugin.Version);

        var cacheDir = cache.GetResourceSpecificDir(plugin);

        var allFiles = FetchFilesFromCache(cacheDir);

        if (allFiles.Any())
        {
            logger.LogDebug(RoslynResources.EAI_CacheHit, cacheDir);
        }
        else
        {
            logger.LogDebug(RoslynResources.EAI_CacheMiss);
            FetchResourceFromServer(plugin, cacheDir);
            allFiles = FetchFilesFromCache(cacheDir);
            Debug.Assert(allFiles.Any(), "Expecting to find files in cache after successful fetch from server");
        }

        return allFiles;
    }

    private static IEnumerable<string> FetchFilesFromCache(string pluginCacheDir) =>
        Directory.Exists(pluginCacheDir)
            ? Directory.GetFiles(pluginCacheDir, "*.*", SearchOption.AllDirectories).Where(name => !name.EndsWith(".zip"))
            : Enumerable.Empty<string>();

    private void FetchResourceFromServer(Plugin plugin, string targetDir)
    {
        logger.LogDebug(RoslynResources.EAI_FetchingPluginResource, plugin.Key, plugin.Version, plugin.StaticResourceName);

        Directory.CreateDirectory(targetDir);

        if (server.TryDownloadEmbeddedFile(plugin.Key, plugin.StaticResourceName, targetDir).Result)
        {
            var targetFilePath = Path.Combine(targetDir, plugin.StaticResourceName);

            if (IsZipFile(targetFilePath))
            {
                logger.LogDebug(Resources.MSG_ExtractingFiles, targetDir);
                ZipFile.ExtractToDirectory(targetFilePath, targetDir);
            }
        }
        else
        {
            throw new FileNotFoundException(string.Format(RoslynResources.EAI_PluginResourceNotFound, plugin.Key, plugin.Version, plugin.StaticResourceName));
        }
    }

    private static bool IsZipFile(string fileName) =>
        string.Equals(".zip", Path.GetExtension(fileName), StringComparison.OrdinalIgnoreCase);

    #endregion Private methods
}
