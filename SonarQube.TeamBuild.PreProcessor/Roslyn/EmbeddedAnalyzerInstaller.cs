//-----------------------------------------------------------------------
// <copyright file="EmbeddedAnalyzerInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
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
    /// The plugin resources are cached locally under %temp%\.sq\.static\[package_version]\[resource]
    /// If the required version is available locally then it will not be downloaded from the
    /// SonarQube server.
    /// </para>
    /// </remarks>
    public class EmbeddedAnalyzerInstaller : IAnalyzerInstaller
    {
        private readonly ISonarQubeServer server;
        private readonly string localCacheDirectory;
        private readonly ILogger logger;

        public EmbeddedAnalyzerInstaller(ISonarQubeServer server, ILogger logger)
            : this(server, GetLocalCacheDirectory(), logger)
        {
        }

        public EmbeddedAnalyzerInstaller(ISonarQubeServer server, string localCacheDirectory, ILogger logger)
        {
            if (server == null)
            {
                throw new ArgumentNullException("server");
            }
            if (string.IsNullOrWhiteSpace(localCacheDirectory))
            {
                throw new ArgumentNullException("localCacheDirectory");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            this.server = server;
            this.localCacheDirectory = localCacheDirectory;
            this.logger = logger;
        }

        #region IAnalyzerInstaller methods

        public IEnumerable<string> InstallAssemblies(IEnumerable<Plugin> plugins)
        {
            if (plugins == null)
            {
                throw new ArgumentNullException("plugins");
            }

            if (!plugins.Any())
            {
                this.logger.LogInfo(RoslynResources.EAI_NoPluginsSpecified);
                return Enumerable.Empty<string>(); // nothing to deploy
            }

            this.logger.LogInfo(RoslynResources.EAI_InstallingAnalyzers);

            this.logger.LogDebug(RoslynResources.EAI_LocalAnalyzerCache, this.localCacheDirectory);
            Directory.CreateDirectory(this.localCacheDirectory); // ensure the cache dir exists

            HashSet<string> allFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Plugin plugin in plugins)
            {
                IEnumerable<string> files = GetPluginResourceFiles(plugin);
                foreach(string file in files)
                {
                    allFiles.Add(file);
                }
            }

            return allFiles;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// We want the resource cache to be in a well-known location so we can re-use files that have
        /// already been installed (although this won't help for e.g. hosted build agents)
        /// </summary>
        private static string GetLocalCacheDirectory()
        {
            string localCache = Path.Combine(Path.GetTempPath(), ".sq", ".static");
            return localCache;
        }
        
        private IEnumerable<string> GetPluginResourceFiles(Plugin plugin)
        {
            string cacheDir = GetResourceSpecificDir(this.localCacheDirectory, plugin);

            if (Directory.Exists(cacheDir))
            {
                this.logger.LogDebug(RoslynResources.EAI_UsingCachedResource, cacheDir);
            }
            else
            {
                FetchResourceFromServer(plugin, cacheDir);
            }

            string[] allFiles = Directory.GetFiles(cacheDir, "*.*", SearchOption.AllDirectories);
            return allFiles;
        }

        private void FetchResourceFromServer(Plugin plugin, string targetDir)
        {
            logger.LogDebug(RoslynResources.EAI_FetchingPluginResource, plugin.Key, plugin.Version, plugin.StaticResourceName);

            Directory.CreateDirectory(targetDir);

            if (server.TryDownloadEmbeddedFile(plugin.Key, plugin.StaticResourceName, targetDir))
            {
                string targetFilePath = Path.Combine(targetDir, plugin.StaticResourceName);

                if (IsZipFile(targetFilePath))
                {
                    logger.LogDebug(Resources.MSG_ExtractingFiles, targetDir);
                    ZipFile.ExtractToDirectory(targetFilePath, targetDir);
                }
            }
            else
            {
                logger.LogWarning(RoslynResources.EAI_PluginResourceNotFound, plugin.Key, plugin.Version, plugin.StaticResourceName);
            }
        }

        /// <summary>
        /// Returns a version-specific directory in which the plugin resources
        /// will be cached
        /// </summary>
        public static string GetPluginSpecificDir(string baseDir, Plugin plugin)
        {
            // Format is: [base]\[plugin_version]
            string pluginVersionFolder = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0}_{1}",
                plugin.Key, plugin.Version);

            pluginVersionFolder = StripInvalidDirectoryChars(pluginVersionFolder);

            string fullDir = Path.Combine(baseDir, pluginVersionFolder);
            return fullDir;
        }

        /// <summary>
        /// Returns a unique directory for the specific resource
        /// </summary>
        public static string GetResourceSpecificDir(string baseDir, Plugin plugin)
        {
            // Format is: [base]\[plugin.version]\[resource name]

            string pluginDir = GetPluginSpecificDir(baseDir, plugin);
            string resourceFolder = StripInvalidDirectoryChars(plugin.StaticResourceName);

            string fullDir = Path.Combine(pluginDir, resourceFolder);
            return fullDir;
        }


        private static string StripInvalidDirectoryChars(string folderName)
        {
            return StripInvalidChars(folderName, Path.GetInvalidPathChars());
        }

        private static string StripInvalidFileChars(string folderName)
        {
            return StripInvalidChars(folderName, Path.GetInvalidFileNameChars());
        }

        private static string StripInvalidChars(string text, char[] invalidChars)
        {
            StringBuilder sb = new StringBuilder();

            foreach (char c in text)
            {
                if (!invalidChars.Contains(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }


        private static bool IsZipFile(string fileName)
        {
            return string.Equals(".zip", Path.GetExtension(fileName), StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}