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
using System.Linq;

namespace SonarScanner.Shim
{
    public static class PathHelper
    {
        public static bool IsPartOfAProject(string filePath, IEnumerable<string> projectPaths)
        {
            return projectPaths.Any(projectPath => IsInFolder(filePath, projectPath));
        }

        public static bool IsInFolder(string filePath, string folder)
        {
            var normalizedPath = Path.GetDirectoryName(Path.GetFullPath(filePath));
            return normalizedPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetCommonRoot(IEnumerable<string> paths)
        {
            var projectDirectoryParts = paths
                .Select(p => p.Split(Path.DirectorySeparatorChar))
                .ToList();

            if (projectDirectoryParts.Count == 0)
            {
                return string.Empty;
            }

            var commonParts = projectDirectoryParts
                .OrderBy(p => p.Length)
                .First()
                .TakeWhile((element, index) => projectDirectoryParts.All(p => p[index] == element))
                .ToArray();

            return string.Join(Path.DirectorySeparatorChar.ToString(), commonParts);
        }
    }
}
