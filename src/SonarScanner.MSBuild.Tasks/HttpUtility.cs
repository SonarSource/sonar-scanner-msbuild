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
#pragma warning disable IDE0011 // Add braces
#pragma warning disable S121    // Control structures should use curly braces
#pragma warning disable SA1121  // Use built-in type alias
#pragma warning disable SA1503  // Braces should not be omitted
#pragma warning disable SA1408  // Conditional expressions should declare precedence
#pragma warning disable SA1519  // Braces should not be omitted from multi-line child statement

internal static class HttpUtility
{
    // Courtesy of the mono-project
    // https://github.com/mono/mono/blob/0f53e9e151d92944cacab3e24ac359410c606df6/mcs/class/System.Web/System.Web/HttpUtility.cs#L528
    public static string JavaScriptStringEncode(string value, bool addDoubleQuotes)
    {
        if (String.IsNullOrEmpty(value))
            return addDoubleQuotes ? "\"\"" : String.Empty;

        int len = value.Length;
        bool needEncode = false;
        char c;
        for (int i = 0; i < len; i++)
        {
            c = value[i];

            if (c >= 0 && c <= 31 || c == 34 || c == 39 || c == 92)
            {
                needEncode = true;
                break;
            }
        }

        if (!needEncode)
            return addDoubleQuotes ? "\"" + value + "\"" : value;

        var sb = new StringBuilder();
        if (addDoubleQuotes)
            sb.Append('"');

        for (int i = 0; i < len; i++)
        {
            c = value[i];
            if (c >= 0 && c <= 7 || c == 11 || c >= 14 && c <= 31 || c == 39)
                sb.AppendFormat("\\u{0:x4}", (int)c);
            else
                switch ((int)c)
                {
                    case 8:
                        sb.Append("\\b");
                        break;

                    case 9:
                        sb.Append("\\t");
                        break;

                    case 10:
                        sb.Append("\\n");
                        break;

                    case 12:
                        sb.Append("\\f");
                        break;

                    case 13:
                        sb.Append("\\r");
                        break;

                    case 34:
                        sb.Append("\\\"");
                        break;

                    case 92:
                        sb.Append("\\\\");
                        break;

                    default:
                        sb.Append(c);
                        break;
                }
        }

        if (addDoubleQuotes)
            sb.Append('"');

        return sb.ToString();
    }
}
