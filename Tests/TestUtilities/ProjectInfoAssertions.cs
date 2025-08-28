/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

namespace TestUtilities;

public static class ProjectInfoAssertions
{
    public static ProjectInfo AssertProjectInfoExists(string rootOutputFolder, string fullProjectFileName)
    {
        var items = Directory.EnumerateDirectories(rootOutputFolder, "*.*", SearchOption.TopDirectoryOnly)
            .Select(x => Path.Combine(x, FileConstants.ProjectInfoFileName))
            .Where(File.Exists)
            .Select(ProjectInfo.Load)
            .ToArray();
        items.Should().NotBeEmpty("Failed to locate any project info files under the specified root folder");
        var match = items.FirstOrDefault(x => fullProjectFileName.Equals(x.FullPath, StringComparison.OrdinalIgnoreCase));
        match.Should().NotBeNull("Failed to retrieve a project info file for the specified project: {0}", fullProjectFileName);
        return match;
    }
}
