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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class TelemetryTest
{
    private readonly IFileWrapper fileWrapper = Substitute.For<IFileWrapper>();

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Telemetry_WriteTelemetryMessages()
    {
        var telemetry = new Telemetry(fileWrapper, new TestLogger());
        telemetry["key1"] = "value1";
        telemetry["key2"] = "value2";
        telemetry["key3"] = "value3";

        const string outputDir = "outputDir";
        telemetry.Write(outputDir);

        // Contents are created with string builder to have the correct line endings for each OS
        var contents = new StringBuilder()
            .AppendLine("""{"key1":"value1"}""")
            .AppendLine("""{"key2":"value2"}""")
            .AppendLine("""{"key3":"value3"}""")
            .ToString();
        fileWrapper.Received(1).AppendAllText(Path.Combine(outputDir, FileConstants.TelemetryFileName), contents);
    }

    [TestMethod]
    public void Telemetry_WriteTelemetryMessages_DifferentValueTypes()
    {
        var telemetry = new Telemetry(fileWrapper, new TestLogger());
        telemetry["key1"] = "value1";
        telemetry["key2"] = 2;
        telemetry["key3"] = true;

        const string outputDir = "outputDir";
        telemetry.Write(outputDir);

        // Contents are created with string builder to have the correct line endings for each OS
        var contents = new StringBuilder()
            .AppendLine("""{"key1":"value1"}""")
            .AppendLine("""{"key2":2}""")
            .AppendLine("""{"key3":true}""")
            .ToString();
        fileWrapper.Received(1).AppendAllText(Path.Combine(outputDir, FileConstants.TelemetryFileName), contents);
    }

    [TestMethod]
    public void Telemetry_WriteTelemetryMessages_IOException_DoesNotThrow()
    {
        var logger = new TestLogger();
        var telemetryJson = Path.Combine("outputDir", FileConstants.TelemetryFileName);
        fileWrapper.When(x => x.AppendAllText(telemetryJson, Arg.Any<string>())).Do(_ => throw new DirectoryNotFoundException($"Could not find a part of the path '{telemetryJson}'."));
        var telemetry = new Telemetry(fileWrapper, logger);
        telemetry.Write("outputDir");

        fileWrapper.Received(1).AppendAllText(telemetryJson, Arg.Any<string>());
        logger.Should().HaveWarnings("Could not write Telemetry.S4NET.json in outputDir");
    }

    [TestMethod]
    public void Telemetry_WriteTelemetryMessages_NotSupportedValueThrows()
    {
        var telemetry = new Telemetry(fileWrapper, new TestLogger());
        telemetry["key1"] = new Dictionary<string, string> { { "key2", "value" } };
        const string outputDir = "outputDir";
        telemetry.Invoking(x => x.Write(outputDir))
            .Should()
            .ThrowExactly<NotSupportedException>()
            .WithMessage("Unsupported telemetry message value type: System.Collections.Generic.Dictionary`2[System.String,System.String]");
    }

    [TestMethod]
    public void Telemetry_Write_CanOnlyBeCalledOnce()
    {
        const string outputDir = "outputDir";
        var telemetry = new Telemetry(fileWrapper, new TestLogger());
        telemetry.Invoking(x => x.Write(outputDir)).Should().NotThrow();
        telemetry.Invoking(x => x.Write(outputDir))
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage("The Telemetry was written already and should only write once.");
    }

    [TestMethod]
    public void Telemetry_Write_ImmuutableAfterWrite()
    {
        const string outputDir = "outputDir";
        var telemetry = new Telemetry(fileWrapper, new TestLogger());
        telemetry["key1"] = "value1";
        telemetry.Invoking(x => x.Write(outputDir)).Should().NotThrow();
        telemetry.Invoking(x => _ = x["key1"]).Should().NotThrow();
        telemetry.Invoking(x => x["key2"] = "value2")
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage("The Telemetry was written already. Any additions after the write are invalid, because they are not forwarded to the Java telemetry plugin.");
    }
}
