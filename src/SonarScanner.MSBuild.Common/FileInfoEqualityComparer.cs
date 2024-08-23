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

namespace SonarScanner.MSBuild.Common;

public class FileInfoEqualityComparer : IEqualityComparer<FileInfo>
{
    // It is safe to assume we can use IgnoreCase pattern everywhere as .Net Core build on non-Windows OSes will fail
    // when the path doesn't match the file-system path.
    public static readonly StringComparison ComparisonType = StringComparison.OrdinalIgnoreCase;

    public static FileInfoEqualityComparer Instance { get; } = new FileInfoEqualityComparer();

    public bool Equals(FileInfo x, FileInfo y) => x.FullName.Equals(y.FullName, ComparisonType);

    public int GetHashCode(FileInfo obj) => obj.FullName.ToUpperInvariant().GetHashCode();
}
