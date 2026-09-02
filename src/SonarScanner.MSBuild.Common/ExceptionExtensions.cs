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
    /// <summary>
    /// Returns the message of the exception, followed by the messages of all its inner exceptions, from the outermost to the innermost.
    /// The root cause of a failure is often only present in an inner exception (e.g. the certificate problem behind an SSL handshake failure),
    /// so it has to be reported for the failure to be actionable.
    /// </summary>
    public static IEnumerable<string> Messages(this Exception exception)
    {
        if (exception is null)
        {
            yield break;
        }
        yield return exception.Message;
        var inner = exception is AggregateException aggregate
            ? (IEnumerable<Exception>)aggregate.InnerExceptions
            : [exception.InnerException];
        foreach (var message in inner.SelectMany(x => x.Messages()))
        {
            yield return message;
        }
    }

    /// <summary>
    /// Returns the messages of the exception and its inner exceptions that are not already contained in <paramref name="precedingMessage"/>
    /// or in a previously returned message.
    /// </summary>
    public static IEnumerable<string> MessagesNotAlreadyContainedIn(this Exception exception, string precedingMessage)
    {
        var reportedMessages = new List<string> { precedingMessage };
        foreach (var message in exception.Messages())
        {
            if (!reportedMessages.Any(x => x.Contains(message)))
            {
                yield return message;
                reportedMessages.Add(message);
            }
        }
    }

    /// <summary>
    /// Returns the messages of the exception and all its inner exceptions, joined on a single line.
    /// </summary>
    public static string MessageChain(this Exception exception) =>
        string.Join(" -> ", exception.MessagesNotAlreadyContainedIn(string.Empty));
}
