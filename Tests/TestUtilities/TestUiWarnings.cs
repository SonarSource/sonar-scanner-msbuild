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

using System.Globalization;

namespace TestUtilities;

public class TestUiWarnings : IUiWarnings
{
    private readonly TestLogger logger;

    // All messages are normalized to Unix line endings, because Resx files contains multiline messages with CRLF and we emit mix of LF and CRFL to logs on *nix system
    public List<string> Messages { get; }

    public TestUiWarnings(TestLogger logger)
    {
        this.logger = logger;
        Messages = [];
    }

    public void AssertNoUIWarningsLogged() =>   // TODO custom assertions
        Messages.Should().BeEmpty("Expecting no UI warnings to be logged");

    public void AssertUIWarningLogged(string expected)
    {
        Messages.Should().Contain(expected.ToUnixLineEndings());
        logger.Should().HaveWarnings(expected);
    }

    /// <summary>
    /// Checks that at least one UI warning exists that contains all of the specified strings.
    /// </summary>
    public void AssertUIWarningExists(params string[] expected)
    {
        expected = expected.Select(x => x.ToUnixLineEndings()).ToArray();
        var matches = Messages.Where(x => expected.All(e => x.Contains(e)));
        matches.Should().NotBeEmpty("No UI warning contains the expected strings: {0}", string.Join(",", expected));
    }

    public void Log(string message, params object[] args)
    {
        Messages.Add(FormatMessage(message, args));
        logger.LogWarning(message, args);
    }

    public void Write(string outputFolder)
    {
        // no-op
    }

    private static string FormatMessage(string message, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, message, args).ToUnixLineEndings();
}
