/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
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
using System.Linq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.Shim
{
    public static class PathHelper
    {
        public static string WithTrailingDirectorySeparator(this DirectoryInfo directory)
        {
            if (directory == null)
            {
                throw new ArgumentNullException(nameof(directory));
            }

            if (directory.FullName.EndsWith(Path.DirectorySeparatorChar.ToString()) ||
                directory.FullName.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                return directory.FullName;
            }

            if (directory.FullName.Contains(Path.AltDirectorySeparatorChar))
            {
                return directory.FullName + Path.AltDirectorySeparatorChar;
            }

            return directory.FullName + Path.DirectorySeparatorChar;
        }

        public static bool IsInDirectory(this FileInfo file, DirectoryInfo directory)
        {
            var normalizedDirectoryPath = directory.WithTrailingDirectorySeparator();
            return file.FullName.StartsWith(normalizedDirectoryPath, FileInfoEqualityComparer.ComparisonType);
        }

        public static DirectoryInfo GetCommonRoot(IEnumerable<DirectoryInfo> paths)
        {
            if (paths == null)
            {
                return null;
            }

            var projectDirectoryParts = paths
                .Select(GetParts)
                .ToList();

            var commonParts = projectDirectoryParts
                .OrderBy(p => p.Count)
                .First()
                .TakeWhile((element, index) => projectDirectoryParts.All(p => p[index] == element))
                .ToArray();

            if (commonParts.Length == 0)
            {
                return null;
            }

            return new DirectoryInfo(Path.Combine(commonParts));
        }

        public static IList<string> GetParts(DirectoryInfo directoryInfo)
        {
            if (directoryInfo == null)
            {
                throw new ArgumentNullException(nameof(directoryInfo));
            }

            var parts = new List<string>();
            var currentDirectoryInfo = directoryInfo;

            while (currentDirectoryInfo.Parent != null)
            {
                parts.Add(currentDirectoryInfo.Name);
                currentDirectoryInfo = currentDirectoryInfo.Parent;
            }

            parts.Add(currentDirectoryInfo.Name);
            parts.Reverse();

            return parts;
        }
    }
}
