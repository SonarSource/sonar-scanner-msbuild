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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    class SubdirIndex
    {
        // global locking, to ensure synchronized access to index file by multiple processes
        private readonly EventWaitHandle waitHandle = new EventWaitHandle(true, EventResetMode.AutoReset, "90CD3CFF-A12C-4013-A44A-199B8C26818B");

        private readonly string basedir;
        private readonly string indexPath;

        public SubdirIndex(string basedir)
        {
            this.basedir = basedir;
            indexPath = Path.Combine(basedir, "index.json");
        }

        public string GetOrCreatePath(string key)
        {
            waitHandle.WaitOne();
            string path;
            var mapping = ReadMapping();
            if (!mapping.TryGetValue(key, out path))
            {
                path = FindAndCreateNextAvailablePath(mapping.Count);
                mapping.Add(key, path);
                File.WriteAllText(indexPath, JsonConvert.SerializeObject(mapping));
            }
            waitHandle.Set();
            return path;
        }

        private IDictionary<string, string> ReadMapping()
        {
            if (File.Exists(indexPath))
            {
                return JsonConvert.DeserializeObject<IDictionary<string, string>>(File.ReadAllText(indexPath));
            }
            return new Dictionary<string, string>();
        }

        private string FindAndCreateNextAvailablePath(int start)
        {
            int count = start;
            while (Directory.Exists(Path.Combine(basedir, count.ToString())))
            {
                count++;
            }
            var subdir = Path.Combine(basedir, count.ToString());
            Directory.CreateDirectory(subdir);
            return subdir;
        }
    }

    /// <summary>
    /// Handles mapping plugin resources to filesystem directories.
    /// </summary>
    public class PluginResourceCache
    {
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
            return string.Join("/", plugin.Key, plugin.Version, plugin.StaticResourceName);
        }
    }
}
