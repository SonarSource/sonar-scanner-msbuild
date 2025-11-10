/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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
public class AnalysisWarningsTests
{
    [TestMethod]
    public void Write_GenerateFile()
    {
        const string prefixRegex = @"\d{2}:\d{2}:\d{2}(.\d{1,3})?  WARNING:";
        using var output = new OutputCaptureScope();
        var fileWrapper = Substitute.For<IFileWrapper>();
        var analysisWarnings = new AnalysisWarnings(fileWrapper, new ConsoleLogger(includeTimestamp: true));

        analysisWarnings.Log("warn1");
        output.AssertExpectedLastMessageRegex($"{prefixRegex} warn1");

        analysisWarnings.Log("warn2 {0}", "xxx");
        output.AssertExpectedLastMessageRegex($"{prefixRegex} warn2 xxx");

        const string outputDir = "outputDir";
        var expected = """
            [
                { "text": "warn1" },
                { "text": "warn2 xxx" }
            ]
            """;
        analysisWarnings.Write(outputDir);    // this should not contain any timestamps.
        fileWrapper.Received(1).WriteAllText(
            Path.Combine(outputDir, FileConstants.AnalysisWarningsFileName),
            Arg.Is<string>(x => IsMatchingJson(expected, x)));
    }

    private static bool IsMatchingJson(string expected, string actual) =>
        JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual));
}
