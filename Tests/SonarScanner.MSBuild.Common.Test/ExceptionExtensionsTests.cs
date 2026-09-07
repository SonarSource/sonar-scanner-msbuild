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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class ExceptionExtensionsTests
{
    [TestMethod]
    public void UnreportedMessages_NullExceptionWithAlreadyReported_ReturnsEmpty() =>
        ((Exception)null).UnreportedMessages("Already reported").Should().BeEmpty();

    [TestMethod]
    public void UnreportedMessages_AllExceptionMessagesContainedInAlreadyReported_ReturnsEmpty() =>
        new InvalidOperationException("Outer: Root cause", new Exception("Root cause"))
            .UnreportedMessages("Outer: Root cause")
            .Should().BeEmpty();

    [TestMethod]
    public void UnreportedMessages_NoExceptionMessageContainedInAlreadyReported_ReturnsAllExceptionMessages() =>
        new InvalidOperationException("Outer", new IOException("Inner", new Exception("Root cause")))
            .UnreportedMessages("Already reported")
            .Should().Equal("Outer", "Inner", "Root cause");

    [TestMethod]
    public void MessageChain_Null_IsEmpty() =>
        ((Exception)null).MessageChain().Should().BeEmpty();

    [TestMethod]
    public void MessageChain_NoInnerException_ReturnsMessage() =>
        new InvalidOperationException("Something went wrong").MessageChain().Should().Be("Something went wrong");

    [TestMethod]
    public void MessageChain_InnerExceptions_ReturnsOutermostFirst() =>
        new InvalidOperationException(
            "The SSL connection could not be established, see inner exception.",
            new IOException("The handshake failed.", new Exception("The remote certificate is invalid.")))
            .MessageChain()
            .Should().Be(
                "The SSL connection could not be established, see inner exception. -> The handshake failed. -> The remote certificate is invalid.");

    [TestMethod]
    public void MessageChain_AggregateException_DoesNotRepeatMessagesContainedInAggregateMessage()
    {
        var exception = new AggregateException("Everything failed", new InvalidOperationException("First"), new IOException("Second", new Exception("Root cause")));

        // On .NET, AggregateException.Message already embeds its direct inner-exception messages, so they are not repeated.
        // On .NET Framework it does not, so they all appear in the chain.
        var expected = exception.Message.Contains("First")
            ? $"{exception.Message} -> Root cause"
            : $"{exception.Message} -> First -> Second -> Root cause";

        exception.MessageChain().Should().Be(expected);
    }

    [TestMethod]
    public void MessageChain_DoesNotRepeatMessageContainedInPreviousInnerException() =>
        new InvalidOperationException("Outer", new IOException("Inner: Root cause", new Exception("Root cause")))
            .MessageChain()
            .Should()
            .Be("Outer -> Inner: Root cause");
}
