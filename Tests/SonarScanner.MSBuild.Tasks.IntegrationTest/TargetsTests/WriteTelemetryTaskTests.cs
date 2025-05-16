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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Tasks.IntegrationTest.TargetsTests;

[TestClass]
public class WriteTelemetryTaskTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void WriteTelemetryWritesJsonToFile()
    {
        var msBuild = MSBuildLocator.GetMSBuildPath(TestContext);
        var uniqueDir = Path.Combine(TestContext.TestRunDirectory, UniqueDirectory.CreateNext(TestContext.TestRunDirectory));
        var telemetryFilename = Path.Combine(uniqueDir, "Telemetry.json");
        var projFileContent = $"""
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <UsingTask
                  TaskName="{nameof(WriteTelemetry)}"
                  AssemblyFile="{typeof(WriteTelemetry).Assembly.Location}">
              </UsingTask>
              <PropertyGroup>
                <TelemetryFilename>{telemetryFilename}</TelemetryFilename>
              </PropertyGroup>
              <Target Name="MyTarget">
                <ItemGroup>
                  <Telemetry Include="TestKey1" Value="SomeMessage"/>
                </ItemGroup>
                <ItemGroup>
                  <Telemetry Include="TestKey1" Value="SomeOtherMessage"/>
                </ItemGroup>
                <ItemGroup>
                  <Telemetry Include="TestKey2" Value="SomeMessage"/>
                </ItemGroup>
                <WriteTelemetry Filename="$(TelemetryFilename)" Key ="Test1" Value="123" Telemetry="@(Telemetry)"/>
                <WriteTelemetry Filename="$(TelemetryFilename)" Key ="Test2" Value="456"/>
              </Target>
            </Project>
            """;
        var projFilename = Path.Combine(uniqueDir, "TestProject.proj");
        File.WriteAllText(projFilename, projFileContent);

        string[] msbuildArgs = [projFilename, "/m:1", "/t:MyTarget"];
        var args = new ProcessRunnerArguments(msBuild, false)
        {
            CmdLineArgs = msbuildArgs
        };
        var runner = new ProcessRunner(new ConsoleLogger(true));
        var result = runner.Execute(args);
        result.Succeeded.Should().BeTrue();
        File.Exists(telemetryFilename).Should().BeTrue();
        File.ReadAllText(telemetryFilename).Should().Be(new StringBuilder() // NewLine is OS specific and raw string blocks always use \r\n.
            .AppendLine("""{"Test1":"123"}""")
            .AppendLine("""{"TestKey1":"SomeMessage"}""")
            .AppendLine("""{"TestKey1":"SomeOtherMessage"}""")
            .AppendLine("""{"TestKey2":"SomeMessage"}""")
            .AppendLine("""{"Test2":"456"}""")
            .ToString());
    }
}
