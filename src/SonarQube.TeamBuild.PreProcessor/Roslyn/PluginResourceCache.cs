/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using System.Threading;
using Newtonsoft.Json;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    public class SubdirIndex
    {
        // global locking, to ensure synchronized access to index file by multiple processes
        private static readonly Mutex mutex = new Mutex(false, @"Global\90CD3CFF-A12C-4013-A44A-199B8C26818B");

        private readonly string basedir;
        private readonly string indexPath;

        public SubdirIndex(string basedir)
        {
            this.basedir = basedir;
            indexPath = Path.Combine(basedir, "index.json");
        }

        public string GetOrCreatePath(string key)
        {
            mutex.WaitOne();
            try
            {
                var mapping = ReadMapping();
                if (!mapping.TryGetValue(key, out string path))
                {
                    path = FindAndCreateNextAvailablePath(mapping.Count);
                    mapping.Add(key, path);
                    File.WriteAllText(indexPath, JsonConvert.SerializeObject(mapping));
                }
                return path;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
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
            var count = start;
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

            index = new SubdirIndex(basedir);
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
