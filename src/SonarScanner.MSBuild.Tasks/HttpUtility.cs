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

namespace SonarScanner.MSBuild.Tasks;

#pragma warning disable S3776   // Cognitive Complexity of methods should not be too high

internal static class HttpUtility
{
    // ecma-404 conformant JSON string encoder
    // https://ecma-international.org/publications-and-standards/standards/ecma-404/
    public static string JavaScriptStringEncode(string value)
    {
        if (value is null)
        {
            return "null"; // JSON null literal
        }

        var sb = new StringBuilder(value.Length + 2);
        sb.Append('\"');

        foreach (var c in value)
        {
            // Use a switch statement instead of a switch expression, to avoid conversions from char to string and use the best overload of sb.Append for each case.
            switch (c)
            {
                case '\"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c <= 0x1F)
                    {
                        sb.AppendFormat(@"\u{0:X4}", (int)c);
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }

        sb.Append('\"');
        return sb.ToString();
    }
}
