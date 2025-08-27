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

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class SonarEngineWrapperTest
{
    private const string SampleInput = """
        { "scannerProperties":
            [
                {"key": "sonar.scanner.app", "value": "S4NET"}
            ]
        }
        """;

    private readonly TestRuntime runtime = new();

    [TestMethod]
    public void Execute_Success()
    {
        var runner = new MockProcessRunner(true);
        var engine = new SonarEngineWrapper(runtime, runner);
        var result = engine.Execute(new AnalysisConfig() { JavaExePath = "java.exe", EngineJarPath = "engine.jar" }, SampleInput);
        result.Should().BeTrue();
        runner.SuppliedArguments.Should().BeEquivalentTo(new
        {
            ExeName = "java.exe",
            CmdLineArgs = (string[])["-jar", "engine.jar"],
        });
        AssertWrittenInput(runner, SampleInput);
        runtime.Logger.AssertInfoLogged("The scanner engine has finished");
    }

    [TestMethod]
    public void Execute_Failure()
    {
        var runner = new MockProcessRunner(false);
        var engine = new SonarEngineWrapper(runtime, runner);
        var result = engine.Execute(new AnalysisConfig() { JavaExePath = "java.exe", EngineJarPath = "engine.jar" }, SampleInput);
        result.Should().BeFalse();
        runtime.Logger.AssertErrorLogged("The scanner engine did not complete successfully");
    }

    private static void AssertWrittenInput(MockProcessRunner runner, string expectedInput)
    {
        runner.SuppliedArguments.InputWriter.Should().NotBeNull();
        using var writer = new StreamWriter(new MemoryStream());
        runner.SuppliedArguments.InputWriter(writer);
        writer.Flush();
        writer.BaseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(writer.BaseStream);
        reader.ReadToEnd().Should().Be(expectedInput);
    }
}
