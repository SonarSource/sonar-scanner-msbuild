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
    // Based in parts on the implementation in the mono-project
    // https://github.com/mono/mono/blob/0f53e9e151d92944cacab3e24ac359410c606df6/mcs/class/System.Web/System.Web/HttpUtility.cs#L528
    // Adopted to comply with ecma-404
    // https://ecma-international.org/publications-and-standards/standards/ecma-404/
    public static string JavaScriptStringEncode(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var len = value.Length;
        var needEncode = false;
        char c;
        for (var i = 0; i < len; i++)
        {
            c = value[i];

            if ((c >= 0 && c <= 0x1F) || c == '"' || c == '\\')
            {
                needEncode = true;
                break;
            }
        }

        if (!needEncode)
        {
            return "\"" + value + "\"";
        }

        var sb = new StringBuilder();
        sb.Append('"');

        for (var i = 0; i < len; i++)
        {
            c = value[i];
            if ((c >= 0 && c <= 7) || c == 11 || (c >= 14 && c <= 31))
            {
                sb.AppendFormat("\\u{0:x4}", (int)c);
            }
            else
            {
                sb.Append((int)c switch
                {
                    8 => "\\b",
                    9 => "\\t",
                    10 => "\\n",
                    12 => "\\f",
                    13 => "\\r",
                    34 => "\\\"",
                    92 => "\\\\",
                    _ => c,
                });
            }
        }

        sb.Append('"');
        return sb.ToString();
    }
}
