/*
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
        /// FIXME
        /// </summary>
        public static DirectoryInfo BestCommonRoot(IEnumerable<DirectoryInfo> paths)
        {
            if (paths == null)
            {
                return null;
            }
            var pathParts = paths.Select(GetParts).ToList();
            var shortest = pathParts.OrderBy(x => x.Length).First();
            var commonParts = shortest.TakeWhile((x, index) => pathParts.All(parts => parts[index] == x)).ToArray();
            return commonParts.Length == 0 ? null : new DirectoryInfo(Path.Combine(commonParts));
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
    }
}
