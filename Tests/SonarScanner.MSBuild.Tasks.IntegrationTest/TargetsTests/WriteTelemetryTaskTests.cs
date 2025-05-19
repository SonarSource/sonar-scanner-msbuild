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
    private const string TelemetryFileName = "Telemetry.json";

    public TestContext TestContext { get; set; }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void WriteTelemetryWritesJsonToFile()
    {
        var result = ExecuteMsBuild("""
            <ItemGroup>
              <Telemetry Include="TestKey1" Value="SomeMessage"/>
            </ItemGroup>
            <ItemGroup>
              <Telemetry Include="TestKey1" Value="SomeOtherMessage"/>
              <Telemetry Include="TestKey2" Value="SomeMessage"/>
            </ItemGroup>
            <WriteTelemetry Filename="$(TelemetryFilename)" Key="Test1" Value="123" Telemetry="@(Telemetry)"/>
            <WriteTelemetry Filename="$(TelemetryFilename)" Key="Test2" Value="456"/>
            """, true);
        var telemetryFilename = result.GetPropertyValue("TelemetryFilename");
        result.BuildSucceeded.Should().BeTrue();
        File.Exists(telemetryFilename).Should().BeTrue();
        File.ReadAllText(telemetryFilename).Should().Be(new StringBuilder() // NewLine is OS specific and raw string blocks always use the new line of this file.
            .AppendLine("""{"Test1":"123"}""")
            .AppendLine("""{"TestKey1":"SomeMessage"}""")
            .AppendLine("""{"TestKey1":"SomeOtherMessage"}""")
            .AppendLine("""{"TestKey2":"SomeMessage"}""")
            .AppendLine("""{"Test2":"456"}""")
            .ToString());
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void WriteTelemetryFailsWithoutUndefinedFilename()
    {
        var result = ExecuteMsBuild("""
            <WriteTelemetry Filename="$(Undefined)" Key="Test1" Value="123"/>
            """, false);
        result.BuildSucceeded.Should().BeFalse();
        result.Errors.Should().Contain("""The "WriteTelemetry" task was not given a value for the required parameter "Filename".""");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void WriteTelemetryFailsWithoutFilename()
    {
        var result = ExecuteMsBuild("""
            <WriteTelemetry Key="Test1" Value="123"/>
            """, false);
        result.BuildSucceeded.Should().BeFalse();
        result.Errors.Should().Contain("""The "WriteTelemetry" task was not given a value for the required parameter "Filename".""");
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [TestMethod]
    public void WriteTelemetryWarningIfTelemetryFileCanNotBeCreated()
    {
        var result = ExecuteMsBuild("""
            <WriteTelemetry Filename="$(TelemetryDirectory)/SubDirectory/Telemetry.json" Key="Test1" Value="123"/>
            """, true);
        result.BuildSucceeded.Should().BeTrue();
        var telemetryDirectory = result.GetPropertyValue("TelemetryDirectory");
        result.Warnings.Should().Contain($"""
            Could not find a part of the path '{Path.Combine(telemetryDirectory, "SubDirectory", TelemetryFileName)}'.
            """);
    }

    private BuildLog ExecuteMsBuild(string writeTelemetry, bool buildShouldSucceed)
    {
        var telemetryDirectory = Path.Combine(TestContext.TestRunDirectory, UniqueDirectory.CreateNext(TestContext.TestRunDirectory));
        var telemetryFilename = Path.Combine(telemetryDirectory, TelemetryFileName);
        var projFileContent = $"""
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <UsingTask
                  TaskName="{nameof(WriteTelemetry)}"
                  AssemblyFile="{typeof(WriteTelemetry).Assembly.Location}">
              </UsingTask>
              <PropertyGroup>
                <TelemetryDirectory>{telemetryDirectory}</TelemetryDirectory>
                <TelemetryFilename>{telemetryFilename}</TelemetryFilename>
              </PropertyGroup>
              <Target Name="TestTarget">
                {writeTelemetry}
              </Target>
            </Project>
            """;
        var projFilename = Path.Combine(telemetryDirectory, "TestProject.proj");
        File.WriteAllText(projFilename, projFileContent);

        return BuildRunner.BuildTargets(TestContext, projFilename, buildShouldSucceed, "TestTarget");
    }
}
