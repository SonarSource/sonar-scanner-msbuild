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
using System.Text.RegularExpressions;

namespace SonarScanner.MSBuild.Common;

public static class RegexExtensions
{
#pragma warning disable T0004 // T0004 Use 'SafeRegex.Matches' instead.
    private static readonly MatchCollection EmptyMatchCollection = Regex.Matches(string.Empty, "a", RegexOptions.None, RegexConstants.DefaultTimeout);
#pragma warning restore T0004

    /// <summary>
    /// Matches the input to the regex. Returns <see cref="Match.Empty" /> in case of an <see cref="RegexMatchTimeoutException" />.
    /// </summary>
    public static Match SafeMatch(this Regex regex, string input)
    {
        try
        {
#pragma warning disable T0004 // T0004 Use 'SafeRegex.Matches' instead.
            return regex.Match(input);
#pragma warning restore T0004
        }
        catch (RegexMatchTimeoutException)
        {
            return Match.Empty;
        }
    }

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
#pragma warning disable T0004 // T0004 Use 'SafeRegex.Matches' instead.
            return regex.IsMatch(input);
#pragma warning restore T0004
        }
        catch (RegexMatchTimeoutException)
        {
            return timeoutFallback;
        }
    }

    /// <summary>
    /// Matches the input to the regex. Returns an empty <see cref="MatchCollection" /> in case of an <see cref="RegexMatchTimeoutException" />.
    /// </summary>
    public static MatchCollection SafeMatches(this Regex regex, string input)
    {
        try
        {
#pragma warning disable T0004 // T0004 Use 'SafeRegex.Matches' instead.
            var res = regex.Matches(input);
#pragma warning restore T0004
            _ = res.Count; // MatchCollection is lazy. Accessing "Count" executes the regex and caches the result
            return res;
        }
        catch (RegexMatchTimeoutException)
        {
            return EmptyMatchCollection;
        }
    }
}

public static class SafeRegex
{
    /// <summary>
    /// Matches the input to the regex. Returns <see langword="false" /> in case of an <see cref="RegexMatchTimeoutException" />.
    /// </summary>
    public static bool IsMatch(string input, string pattern) =>
        IsMatch(input, pattern, RegexOptions.None);

    /// <inheritdoc cref="IsMatch(string, string)"/>
    public static bool IsMatch(string input, string pattern, RegexOptions options) =>
        IsMatch(input, pattern, options, RegexConstants.DefaultTimeout);

    /// <inheritdoc cref="IsMatch(string, string)"/>
    public static bool IsMatch(string input, string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        try
        {
#pragma warning disable T0004 // T0004 Use 'SafeRegex.Matches' instead.
            return Regex.IsMatch(input, pattern, options, matchTimeout);
#pragma warning restore T0004
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
