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

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SonarScanner.MSBuild.Tasks;

/// <summary>
/// Build task to write out one or more zero-length files,
/// overwriting any existing files
/// </summary>
public class WriteZeroLengthFiles : Task
{
    #region Input properties

    [Required]
    public string[] FullFilePaths { get; set; }

    #endregion Input properties

    #region Overrides

    public override bool Execute()
    {
        byte[] empty = new byte[] { };

        foreach (var file in FullFilePaths)
        {
            Log.LogMessage(MessageImportance.Low, Resources.WriteZeroLengthFiles_WritingFile, file);
            File.WriteAllBytes(file, empty);
        }
        return true;
    }

    #endregion Overrides

}
