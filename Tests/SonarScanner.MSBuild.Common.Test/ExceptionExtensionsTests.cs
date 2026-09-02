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
    public void Messages_Null_IsEmpty() =>
        ((Exception)null).Messages().Should().BeEmpty();

    [TestMethod]
    public void Messages_NoInnerException_ReturnsSingleMessage() =>
        new InvalidOperationException("Something went wrong").Messages().Should().BeEquivalentTo("Something went wrong");

    [TestMethod]
    public void Messages_InnerExceptions_ReturnsOutermostFirst() =>
        new InvalidOperationException(
            "The SSL connection could not be established, see inner exception.",
            new IOException("The handshake failed.", new Exception("The remote certificate is invalid.")))
            .Messages()
            .Should().BeEquivalentTo(
                [
                    "The SSL connection could not be established, see inner exception.",
                    "The handshake failed.",
                    "The remote certificate is invalid."
                ],
                x => x.WithStrictOrdering());

    [TestMethod]
    public void Messages_AggregateException_ReturnsAllInnerExceptions()
    {
        var messages = new AggregateException("Everything failed", new InvalidOperationException("First"), new IOException("Second", new Exception("Root cause"))).Messages().ToArray();

        // AggregateException.Message already embeds the messages of its direct inner exceptions, so only the tail can be asserted precisely.
        messages.Should().HaveCount(4);
        messages[0].Should().StartWith("Everything failed");
        messages.Skip(1).Should().BeEquivalentTo(["First", "Second", "Root cause"], x => x.WithStrictOrdering());
    }

    [TestMethod]
    public void MessageChain_JoinsAllMessagesOnASingleLine() =>
        new InvalidOperationException("Outer", new IOException("Inner")).MessageChain().Should().Be("Outer -> Inner");

    [TestMethod]
    public void MessageChain_DoesNotRepeatMessageContainedInPreviousInnerException() =>
        new InvalidOperationException("Outer", new IOException("Inner: Root cause", new Exception("Root cause")))
            .MessageChain()
            .Should()
            .Be("Outer -> Inner: Root cause");

    [TestMethod]
    public void MessageChain_NoInnerException_ReturnsMessage() =>
        new InvalidOperationException("Outer").MessageChain().Should().Be("Outer");
}
