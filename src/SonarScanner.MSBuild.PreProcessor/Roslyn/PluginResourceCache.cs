/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.IO;
using Newtonsoft.Json;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.Roslyn;

public class SubdirIndex
{
    // global locking, to ensure synchronized access to index file by multiple processes
    private const string MutexName = @"Global\D63DF69F-BC65-4E00-87ED-A922B7CC623D";

    private readonly string basedir;
    private readonly string indexPath;

    public SubdirIndex(string basedir)
    {
        this.basedir = basedir;
        indexPath = Path.Combine(basedir, "index.json");
    }

    public string GetOrCreatePath(string key)
    {
        using (new SingleGlobalInstanceMutex(MutexName))
        {
            var mapping = ReadMapping();
            if (!mapping.TryGetValue(key, out var path))
            {
                path = FindAndCreateNextAvailablePath(mapping.Count);
                mapping.Add(key, path);
                File.WriteAllText(indexPath, JsonConvert.SerializeObject(mapping));
            }
            return path;
        }
    }

    private IDictionary<string, string> ReadMapping() =>
        File.Exists(indexPath)
            ? JsonConvert.DeserializeObject<IDictionary<string, string>>(File.ReadAllText(indexPath))
            : new Dictionary<string, string>();

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
            throw new ArgumentNullException(nameof(basedir));
        }
        if (!Directory.Exists(basedir))
        {
            throw new DirectoryNotFoundException("no such directory: " + basedir);
        }

        index = new SubdirIndex(basedir);
    }

    /// <summary>
    /// Returns a unique directory for the specific resource.
    /// </summary>
    public string GetResourceSpecificDir(Plugin plugin) =>
        index.GetOrCreatePath(CreateKey(plugin));

    private static string CreateKey(Plugin plugin) =>
        string.Join("/", plugin.Key, plugin.Version, plugin.StaticResourceName);
}
