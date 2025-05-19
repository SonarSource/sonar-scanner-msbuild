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

namespace SonarScanner.MSBuild.PreProcessor.Test;

public partial class PreProcessorTests
{
    [TestMethod]
    public async Task Execute_WritesTelemetry_SetViaCLI()
    {
        var args = new List<string>(CreateArgs())
        {
            "/d:sonar.scanner.scanAll=false"
        };
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        var settings = factory.ReadSettings();
        var preProcessor = new PreProcessor(factory, new ConsoleLogger(false));

        var success = await preProcessor.Execute(args);

        success.Should().BeTrue("Expecting the pre-processing to complete successfully");
        Directory.GetFiles(settings.SonarOutputDirectory).Select(Path.GetFileName).Should().Contain(FileConstants.TelemetryFileName);
        File.ReadAllText(Path.Combine(settings.SonarOutputDirectory, FileConstants.TelemetryFileName))
            .Should()
            .BeEquivalentTo(Contents(
                """{"s4net.params.sonar_scanner_scanAll.value":"false"}""",
                """{"s4net.params.sonar_scanner_scanAll.source":"CLI"}"""));
    }

    [TestMethod]
    public async Task Execute_WritesTelemetry_SetViaServer()
    {
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory(serverProperties: new Dictionary<string, string> { { "sonar.scanner.scanAll", "false" } });
        var settings = factory.ReadSettings();
        var preProcessor = new PreProcessor(factory, new ConsoleLogger(false));

        var success = await preProcessor.Execute(CreateArgs());

        success.Should().BeTrue("Expecting the pre-processing to complete successfully");
        Directory.GetFiles(settings.SonarOutputDirectory).Select(Path.GetFileName).Should().Contain(FileConstants.TelemetryFileName);
        File.ReadAllText(Path.Combine(settings.SonarOutputDirectory, FileConstants.TelemetryFileName))
            .Should()
            .BeEquivalentTo(Contents(
                """{"s4net.params.sonar_scanner_scanAll.value":"false"}""",
                """{"s4net.params.sonar_scanner_scanAll.source":"SQ_SERVER_SETTINGS"}"""));
    }

    [TestMethod]
    public async Task Execute_WritesTelemetry_SetViaAnalysisXml()
    {
        var analysisXmlPath = CreateAnalysisXml(TestContext.TestRunDirectory, new Dictionary<string, string> { { "sonar.scanner.scanAll", "false" } });
        var args = new List<string>(CreateArgs())
        {
            $"/s:{analysisXmlPath}"
        };
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        var settings = factory.ReadSettings();
        var preProcessor = new PreProcessor(factory, new ConsoleLogger(false));

        var success = await preProcessor.Execute(args);

        success.Should().BeTrue("Expecting the pre-processing to complete successfully");
        Directory.GetFiles(settings.SonarOutputDirectory).Select(Path.GetFileName).Should().Contain(FileConstants.TelemetryFileName);
        File.ReadAllText(Path.Combine(settings.SonarOutputDirectory, FileConstants.TelemetryFileName))
            .Should()
            .BeEquivalentTo(Contents(
                """{"s4net.params.sonar_scanner_scanAll.value":"false"}""",
                """{"s4net.params.sonar_scanner_scanAll.source":"SONARQUBE_ANALYSIS_XML"}"""));
    }

    [TestMethod]
    public async Task Execute_WritesTelemetry_SetViaEnvVariable()
    {
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        var settings = factory.ReadSettings();
        Environment.SetEnvironmentVariable("SONARQUBE_SCANNER_PARAMS", """{"sonar.scanner.scanAll": "false"}""");
        var preProcessor = new PreProcessor(factory, new ConsoleLogger(false));

        var success = await preProcessor.Execute(CreateArgs());

        success.Should().BeTrue("Expecting the pre-processing to complete successfully");
        Directory.GetFiles(settings.SonarOutputDirectory).Select(Path.GetFileName).Should().Contain(FileConstants.TelemetryFileName);
        File.ReadAllText(Path.Combine(settings.SonarOutputDirectory, FileConstants.TelemetryFileName))
            .Should()
            .BeEquivalentTo(Contents(
                """{"s4net.params.sonar_scanner_scanAll.value":"false"}""",
                """{"s4net.params.sonar_scanner_scanAll.source":"SONARQUBE_SCANNER_PARAMS"}"""
            ));

    }

    [TestMethod]
    public async Task Execute_WritesTelemetry_SetViaMultipleSources_WritesAll()
    {
        using var scope = new TestScope(TestContext);
        var factory = new MockObjectFactory();
        var settings = factory.ReadSettings();
        Environment.SetEnvironmentVariable("SONARQUBE_SCANNER_PARAMS", """{"sonar.scanner.scanAll": "true"}""");
        var args = new List<string>(CreateArgs())
        {
            "/d:sonar.scanner.scanAll=false"
        };
        var preProcessor = new PreProcessor(factory, new ConsoleLogger(false));

        var success = await preProcessor.Execute(args);

        success.Should().BeTrue("Expecting the pre-processing to complete successfully");
        Directory.GetFiles(settings.SonarOutputDirectory).Select(Path.GetFileName).Should().Contain(FileConstants.TelemetryFileName);
        File.ReadAllText(Path.Combine(settings.SonarOutputDirectory, FileConstants.TelemetryFileName))
            .Should()
            .BeEquivalentTo(Contents(
                """{"s4net.params.sonar_scanner_scanAll.value":"false"}""",
                """{"s4net.params.sonar_scanner_scanAll.source":"CLI"}""",
                """{"s4net.params.sonar_scanner_scanAll.value":"true"}""",
                """{"s4net.params.sonar_scanner_scanAll.source":"SONARQUBE_SCANNER_PARAMS"}"""));
    }

    // Contents are created with string builder to have the correct line endings for each OS
    private static string Contents(params string[] telemetryMessages)
    {
        var st = new StringBuilder();
        foreach (var message in telemetryMessages)
        {
            st.AppendLine(message);
        }
        return st.ToString();
    }

    private static string CreateAnalysisXml(string parentDir, Dictionary<string, string> properties = null)
    {
        Directory.Exists(parentDir).Should().BeTrue("Test setup error: expecting the parent directory to exist: {0}", parentDir);
        var fullPath = Path.Combine(parentDir, "SonarQube.Analysis.xml");
        var xmlProperties = new StringBuilder();
        if (properties is not null)
        {
            foreach (var property in properties)
            {
                xmlProperties.AppendLine($"""<Property Name="{property.Key}">{property.Value}</Property>""");
            }
        }
        var content = $"""
           <?xml version="1.0" encoding="utf-8" ?>
           <SonarQubeAnalysisProperties  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://www.sonarsource.com/msbuild/integration/2015/1">
             {xmlProperties.ToString()}
           </SonarQubeAnalysisProperties>
           """;

        File.WriteAllText(fullPath, content);
        return fullPath;
    }
}
