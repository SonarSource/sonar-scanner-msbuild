﻿/*
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

namespace SonarScanner.MSBuild.Tasks.IntegrationTest.TargetsTests;

[TestClass]
public class WriteSonarTelemetryTaskTests
{
    private const string TelemetryFileName = "Telemetry.json";

    public TestContext TestContext { get; set; }

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
            <WriteSonarTelemetry Filename="$(TelemetryFilename)" Key="Test1" Value="123" Telemetry="@(Telemetry)"/>
            <WriteSonarTelemetry Filename="$(TelemetryFilename)" Key="Test2" Value="456"/>
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

    [TestMethod]
    public void WriteTelemetryFailsWithoutUndefinedFilename()
    {
        var result = ExecuteMsBuild("""
            <WriteSonarTelemetry Filename="$(Undefined)" Key="Test1" Value="123"/>
            """, false);
        result.BuildSucceeded.Should().BeFalse();
        result.Errors.Should().Contain("""The "WriteSonarTelemetry" task was not given a value for the required parameter "Filename".""");
    }

    [TestMethod]
    public void WriteTelemetryFailsWithoutFilename()
    {
        var result = ExecuteMsBuild("""
            <WriteSonarTelemetry Key="Test1" Value="123"/>
            """, false);
        result.BuildSucceeded.Should().BeFalse();
        result.Errors.Should().Contain("""The "WriteSonarTelemetry" task was not given a value for the required parameter "Filename".""");
    }

    [TestMethod]
    public void WriteTelemetryWarningIfTelemetryFileCanNotBeCreated()
    {
        var result = ExecuteMsBuild("""
            <WriteSonarTelemetry Filename="$(TelemetryDirectory)/SubDirectory/Telemetry.json" Key="Test1" Value="123"/>
            """, true);
        result.BuildSucceeded.Should().BeTrue();
        var telemetryDirectory = result.GetPropertyValue("TelemetryDirectory");
        result.Warnings.Should().Contain($"""
            Could not find a part of the path '{Path.Combine(telemetryDirectory, "SubDirectory", TelemetryFileName)}'.
            """);
    }

    [TestMethod]
    public void WriteTelemetryCreateNewWritesOnlyTheFirstEntry()
    {
        var result = ExecuteMsBuild("""
            <ItemGroup>
              <Telemetry Include="TestKey1" Value="SomeMessage"/>
              <Telemetry Include="TestKey2" Value="SomeMessage"/>
            </ItemGroup>
            <WriteSonarTelemetry Filename="$(TelemetryFilename)" CreateNew="true" Telemetry="@(Telemetry)"/>
            <WriteSonarTelemetry Filename="$(TelemetryFilename)" CreateNew="true" Telemetry="@(Telemetry)"/>            <!-- This is ignored, because the file already exists and CreateNew="true" -->
            <WriteSonarTelemetry Filename="$(TelemetryFilename)" CreateNew="true" Key="IgnoredKey" Value="456"/>        <!-- This is ignored, because the file already exists and CreateNew="true" -->
            <WriteSonarTelemetry Filename="$(TelemetryFilename)" CreateNew="false" Key="TestKey3" Value="SomeMessage"/> <!-- This is appended, because CreateNew="false" -->
            <WriteSonarTelemetry Filename="$(TelemetryFilename)" Key="TestKey4" Value="SomeMessage"/>                   <!-- This is appended, because CreateNew defaults to "false" -->
            """, true);
        var telemetryFilename = result.GetPropertyValue("TelemetryFilename");
        result.BuildSucceeded.Should().BeTrue();
        File.Exists(telemetryFilename).Should().BeTrue();
        File.ReadAllText(telemetryFilename).Should().Be(new StringBuilder() // NewLine is OS specific and raw string blocks always use the new line of this file.
            .AppendLine("""{"TestKey1":"SomeMessage"}""")
            .AppendLine("""{"TestKey2":"SomeMessage"}""")
            .AppendLine("""{"TestKey3":"SomeMessage"}""")
            .AppendLine("""{"TestKey4":"SomeMessage"}""")
            .ToString());
        result.Warnings.Should().BeEmpty();
    }

    private BuildLog ExecuteMsBuild(string writeTelemetry, bool buildShouldSucceed)
    {
        var telemetryDirectory = Path.Combine(TestContext.TestRunDirectory, UniqueDirectory.CreateNext(TestContext.TestRunDirectory));
        var telemetryFilename = Path.Combine(telemetryDirectory, TelemetryFileName);
        var projFileContent = $"""
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <UsingTask
                  TaskName="{nameof(WriteSonarTelemetry)}"
                  AssemblyFile="{typeof(WriteSonarTelemetry).Assembly.Location}">
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
