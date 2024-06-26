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

using System.Text.RegularExpressions;

namespace SonarScanner.MSBuild.Common;

public static class RegexExtensions
{
    /// <summary>
    /// Matches the input to the regex. Returns <see langword="false" /> in case of an <see cref="RegexMatchTimeoutException" />.
    /// </summary>
    public static bool SafeIsMatch(this Regex regex, string input) =>
        regex.SafeIsMatch(input, false);

    /// <summary>
    /// Matches the input to the regex. Returns <paramref name="timeoutFallback"/> in case of an <see cref="RegexMatchTimeoutException" />.
    /// </summary>
    public static bool SafeIsMatch(this Regex regex, string input, bool timeoutFallback)
    {
        try
        {
            return regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            return timeoutFallback;
        }
    }
}
