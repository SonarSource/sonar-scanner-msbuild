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
using System.IO;
using Newtonsoft.Json;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    class SubdirIndex
    {
        private readonly string basedir;
        private readonly string indexPath;
        private readonly IDictionary<string, string> mapping;

        public SubdirIndex(string basedir)
        {
            this.basedir = basedir;
            this.indexPath = Path.Combine(basedir, "index.json");
            this.mapping = Read();
        }

        public IDictionary<string, string> Read()
        {
            if (File.Exists(indexPath))
            {
                return JsonConvert.DeserializeObject<IDictionary<string, string>>(File.ReadAllText(indexPath));
            }
            return new Dictionary<string, string>();
        }

        private void Flush()
        {
            File.WriteAllText(indexPath, JsonConvert.SerializeObject(mapping));
        }

        public string GetOrCreatePath(string key)
        {
            lock (mapping)
            {
                string path;
                if (!mapping.TryGetValue(key, out path))
                {
                    path = FindAndCreateNextAvailablePath();
                    mapping.Add(key, path);
                    Flush();
                }
                return path;
            }
        }

        private string FindAndCreateNextAvailablePath()
        {
            int count = mapping.Count;
            while (true)
            {
                count++;
                string subdir = Path.Combine(basedir, count.ToString());
                if (!Directory.Exists(subdir))
                {
                    Directory.CreateDirectory(subdir);
                    return subdir;
                }
            }
        }
    }

    /// <summary>
    /// Handles mapping plugin resources to filesystem directories.
    /// </summary>
    public class PluginResourceCache
    {
        private readonly string basedir;
        private readonly SubdirIndex index;

        public PluginResourceCache(string basedir)
        {
            if (string.IsNullOrWhiteSpace(basedir))
            {
                throw new ArgumentNullException("basedir");
            }
            if (!Directory.Exists(basedir))
            {
                throw new DirectoryNotFoundException("no such directory: " + basedir);
            }

            this.basedir = basedir;
            this.index = new SubdirIndex(basedir);
        }

        /// <summary>
        /// Returns a unique directory for the specific resource
        /// </summary>
        public string GetResourceSpecificDir(Plugin plugin)
        {
            return index.GetOrCreatePath(CreateKey(plugin));
        }

        private string CreateKey(Plugin plugin)
        {
            return String.Join("/", plugin.Key, plugin.Version, plugin.StaticResourceName);
        }
    }
}
