/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            this.logger = logger;

            this.logger.LogDebug(RoslynResources.EAI_LocalAnalyzerCache, localCacheDirectory);
            Directory.CreateDirectory(localCacheDirectory); // ensure the cache dir exists

            this.cache = new PluginResourceCache(localCacheDirectory);
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

            HashSet<string> allFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Plugin plugin in plugins)
            {
                IEnumerable<string> files = GetPluginResourceFiles(plugin);
                foreach (string file in files)
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
            string localCache = Path.Combine(Path.GetTempPath(), ".sonarqube", "resources");
            return localCache;
        }

        private IEnumerable<string> GetPluginResourceFiles(Plugin plugin)
        {
            this.logger.LogDebug(RoslynResources.EAI_ProcessingPlugin, plugin.Key, plugin.Version);

            string cacheDir = cache.GetResourceSpecificDir(plugin);

            IEnumerable<string> allFiles = FetchFilesFromCache(cacheDir);

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

            bool success = server.TryDownloadEmbeddedFile(plugin.Key, plugin.StaticResourceName, targetDir);

            if (success)
            {
                string targetFilePath = Path.Combine(targetDir, plugin.StaticResourceName);

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

        /// <summary>
        /// Returns a version-specific directory in which the plugin resources
        /// will be cached
        /// </summary>
        private static string GetPluginSpecificDir(string baseDir, Plugin plugin)
        {
            // Format is: [base]\[plugin_version]
            string pluginVersionFolder = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0}_{1}",
                plugin.Key, plugin.Version);

            pluginVersionFolder = StripInvalidDirectoryChars(pluginVersionFolder);

            string fullDir = Path.Combine(baseDir, pluginVersionFolder);
            return fullDir;
        }

        private static string StripInvalidDirectoryChars(string folderName)
        {
            return StripInvalidChars(folderName, Path.GetInvalidPathChars());
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