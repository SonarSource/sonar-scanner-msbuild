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

using System;
using System.Text.RegularExpressions;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild;

public static class StringExtensions
{
    /// <summary>
    /// Returns a new string in which all occurrences of a specified string in the current
    /// instance are replaced with another specified string.
    /// </summary>
    /// <param name="input">The string to search for a match.</param>
    /// <param name="oldValue">The string to be replaced.</param>
    /// <param name="newValue">The string to replace all occurrences of oldValue.</param>
    /// <returns>A string that is equivalent to the current string except that all instances of
    /// oldValue are replaced with newValue. If oldValue is not found in the current
    /// instance, the method returns the current instance unchanged.
    /// </returns>
    public static string ReplaceCaseInsensitive(this string input, string oldValue, string newValue) =>
        // Based on https://stackoverflow.com/a/6276029/7156760
        Regex.Replace(input, Regex.Escape(oldValue), newValue.Replace("$", "$$"), RegexOptions.IgnoreCase, RegexConstants.DefaultTimeout);

    public static string RedactSensitiveData(this string input)
    {
        var indexes = SonarProperties.SensitivePropertyKeys
            .Select(x => input.IndexOf(x, StringComparison.OrdinalIgnoreCase))
            .Where(x => x > -1)
            .ToArray();
        if (indexes.Length > 0)
        {
            return input.Substring(0, indexes.Min()) + Resources.MSG_CmdLine_SensitiveCmdLineArgsAlternativeText;
        }

        return input;
    }
}
