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

using System.Text.Json.Nodes;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class UiWarningsTests
{
    [TestMethod]
    public void Write_GenerateFile()
    {
        const string prefixRegex = @"\d{2}:\d{2}:\d{2}(.\d{1,3})?  WARNING:";
        using var output = new OutputCaptureScope();
        var fileWrapper = Substitute.For<IFileWrapper>();
        var uiWarnings = new UiWarnings(fileWrapper, new ConsoleLogger(includeTimestamp: true));

        uiWarnings.Log("uiWarn1");
        output.AssertExpectedLastMessageRegex($"{prefixRegex} uiWarn1");

        uiWarnings.Log("uiWarn2", null);
        output.AssertExpectedLastMessageRegex($"{prefixRegex} uiWarn2");

        uiWarnings.Log("uiWarn3 {0}", "xxx");
        output.AssertExpectedLastMessageRegex($"{prefixRegex} uiWarn3 xxx");

        const string outputDir = "outputDir";
        var expected = """
            [
                { "text": "uiWarn1" },
                { "text": "uiWarn2" },
                { "text": "uiWarn3 xxx" }
            ]
            """;
        uiWarnings.Log(outputDir); // this should not contain any timestamps.
        fileWrapper.Received(1).WriteAllText(
            Path.Combine(outputDir, FileConstants.UIWarningsFileName),
            Arg.Is<string>(x => IsMatchingJson(expected, x)));
    }

    private static bool IsMatchingJson(string expected, string actual) =>
        JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual));
}
