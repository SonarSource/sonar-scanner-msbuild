/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

namespace SonarScanner.MSBuild.Common;

public static class ExceptionExtensions
{
    public static string MessageChain(this Exception exception) =>
        string.Join(" -> ", exception.UnreportedMessages());

    public static IEnumerable<string> UnreportedMessages(this Exception exception, string alreadyReported = "")
    {
        var messages = new List<string>();
        AppendMessages(exception);
        return messages;

        void AppendMessages(Exception current)
        {
            while (current is not null)
            {
                if (!alreadyReported.Contains(current.Message) && !messages.Any(x => x.Contains(current.Message)))
                {
                    messages.Add(current.Message);
                }
                if (current is AggregateException aggregate)
                {
                    foreach (var inner in aggregate.InnerExceptions)
                    {
                        AppendMessages(inner);
                    }
                    return;
                }
                current = current.InnerException;
            }
        }
    }
}
