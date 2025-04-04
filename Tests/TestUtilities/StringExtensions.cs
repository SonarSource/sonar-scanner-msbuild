﻿/*
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

public static class StringExtensions
{
    /// <summary>
    /// Replaces CRLF and CR line endings with LF.
    /// </summary>
    public static string NormalizeLineEndings(this string input) =>
        input.Replace("\r\n", "\n").Replace("\r", "\n");

    /// <summary>
    /// Remove trailing whitespace at the end of each line.
    /// </summary>
    public static string TrimEndOfLineWhitespace(this string input) =>
        string.Join("\r\n", input.Split(["\r\n"], StringSplitOptions.None).Select(x => string.Join("\n", x.Split('\n').Select(s => s.TrimEnd()))));
}
