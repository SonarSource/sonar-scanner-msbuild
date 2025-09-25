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

public class TestAnalysisWarnings : IAnalysisWarnings
{
    private readonly TestLogger logger;

    // All messages are normalized to Unix line endings, because Resx files contains multiline messages with CRLF and we emit mix of LF and CRFL to logs on *nix system
    public List<string> Messages { get; }

    public TestAnalysisWarnings(TestLogger logger)
    {
        this.logger = logger;
        Messages = [];
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
