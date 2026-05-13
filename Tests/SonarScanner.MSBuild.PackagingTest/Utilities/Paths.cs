/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

namespace SonarScanner.MSBuild.PackagingTest.Utilities;

public static class Paths
{
    public static string ProjectRoot { get; }
    public static string BinariesRoot { get; }

    static Paths()
    {
        ProjectRoot = FindRoot(".github");
        BinariesRoot = Path.Combine(ProjectRoot, "Packaging", "Binaries");
        Console.WriteLine("Project root: " + ProjectRoot);
    }

    private static string FindRoot(string expectedSubdirectory)
    {
        var current = Path.GetFullPath(".");
        var root = Path.GetPathRoot(current);
        while (current != root)
        {
            if (Directory.Exists(Path.Combine(current, expectedSubdirectory)))
            {
                return current;
            }
            else
            {
                current = Path.GetDirectoryName(current);
            }
        }
        throw new InvalidOperationException($"Could not find root directory for '{expectedSubdirectory}' from current path: {Path.GetFullPath(".")}");
    }
}
