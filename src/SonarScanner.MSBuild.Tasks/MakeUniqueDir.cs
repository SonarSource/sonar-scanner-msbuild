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

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.Tasks;

public class MakeUniqueDir : Task
{
    /// <summary>
    /// The path where folder will be created, usually that is .sonarqube/out in the solution root
    /// </summary>
    [Required]
    public string Path { get; set; }

    /// <summary>
    /// Returns the name of the created folder
    /// </summary>
    [Output]
    public string UniqueName { get; private set; }

    /// <summary>
    /// Returns the full path of the created folder
    /// </summary>
    [Output]
    public string UniquePath { get; private set; }

    public override bool Execute()
    {
        UniqueName = UniqueDirectory.CreateNext(Path);
        UniquePath = System.IO.Path.Combine(Path, UniqueName);
        return true;
    }
}
