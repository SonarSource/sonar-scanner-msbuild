﻿/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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
using System.Linq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.Shim
{
    public static class PathHelper
    {
        public static string WithTrailingDirectorySeparator(this DirectoryInfo directory)
        {
            _ = directory ?? throw new ArgumentNullException(nameof(directory));
            if (directory.FullName.EndsWith(Path.DirectorySeparatorChar.ToString()) || directory.FullName.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                return directory.FullName;
            }
            else if (directory.FullName.Contains(Path.AltDirectorySeparatorChar))
            {
                return directory.FullName + Path.AltDirectorySeparatorChar;
            }
            else
            {
                return directory.FullName + Path.DirectorySeparatorChar;
            }
        }

        public static bool IsInDirectory(this FileInfo file, DirectoryInfo directory)
        {
            var normalizedDirectoryPath = directory.WithTrailingDirectorySeparator();
            return file.FullName.StartsWith(normalizedDirectoryPath, FileInfoEqualityComparer.ComparisonType);
        }

        /// <summary>
        /// Returns longest common root path.
        /// In case paths do not share common root, path from most common drive is selected.
        /// </summary>
        public static DirectoryInfo BestCommonRoot(IEnumerable<DirectoryInfo> paths)
        {
            if (paths == null)
            {
                return null;
            }
            var pathParts = paths.Select(GetParts).ToArray();
            if (BestRoot(pathParts) is { } bestRoot)
            {
                pathParts = pathParts.Where(x => x[0] == bestRoot).ToArray();
                var shortest = pathParts.OrderBy(x => x.Length).First();
                return new DirectoryInfo(Path.Combine(shortest.TakeWhile((x, index) => pathParts.All(parts => parts[index] == x)).ToArray()));
            }
            else
            {
                return null;
            }
        }

        public static string[] GetParts(DirectoryInfo directory)
        {
            _ = directory ?? throw new ArgumentNullException(nameof(directory));
            var parts = new List<string>();
            while (directory.Parent != null)
            {
                parts.Add(directory.Name);
                directory = directory.Parent;
            }
            parts.Add(directory.Name);
            return parts.AsEnumerable().Reverse().ToArray();
        }

        private static string BestRoot(string[][] pathParts)
        {
            var roots = pathParts.Select(x => x[0])
                .GroupBy(x => x)
                .Select(x => new { Root = x.Key, Count = x.Count() })
                .OrderByDescending(x => x.Count)
                .ToArray();
            if (roots.Length == 0)
            {
                return null;
            }
            else if (roots.Length == 1)
            {
                return roots[0].Root;
            }
            else    // Paths do not share common root. Choose the best one, if there's a clear winner.
            {
                return roots[0].Count > roots[1].Count
                    ? roots[0].Root
                    : null;
            }
        }
    }
}
