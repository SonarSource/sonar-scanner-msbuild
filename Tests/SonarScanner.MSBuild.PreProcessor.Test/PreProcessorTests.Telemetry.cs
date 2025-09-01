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
        var telemetry = await SetUpTest(args);

        telemetry.Should()
            .BeEquivalentTo(new Dictionary<string, object>
            {
                { "dotnetenterprise.s4net.params.cmd_line1.source", "CLI" },
                { "dotnetenterprise.s4net.params.sonar_log_level.source", "CLI" },
                { "dotnetenterprise.s4net.params.sonar_scanner_scanall.source", "CLI" },
                { "dotnetenterprise.s4net.serverInfo.product", "SQ_Server" },
                { "dotnetenterprise.s4net.serverInfo.serverUrl", "custom_url" }
            });
    }

    [TestMethod]
    public async Task Execute_WritesTelemetry_SetViaAnalysisXml()
    {
        var analysisXmlPath = CreateAnalysisXml(TestContext.TestRunDirectory, new Dictionary<string, string> { { "sonar.scanner.scanAll", "false" } });
        var args = new List<string>(CreateArgs())
        {
            $"/s:{analysisXmlPath}"
        };
        var telemetry = await SetUpTest(args);

        telemetry.Should()
            .BeEquivalentTo(new Dictionary<string, object>
            {
                { "dotnetenterprise.s4net.params.cmd_line1.source", "CLI" },
                { "dotnetenterprise.s4net.params.sonar_log_level.source", "CLI" },
                { "dotnetenterprise.s4net.params.sonar_scanner_scanall.source", "SONARQUBE_ANALYSIS_XML" },
                { "dotnetenterprise.s4net.serverInfo.product", "SQ_Server" },
                { "dotnetenterprise.s4net.serverInfo.serverUrl", "custom_url" }
            });
    }

    [TestMethod]
    public async Task Execute_WritesTelemetry_SetViaEnvVariable()
    {
        var telemetry = await SetUpTest(environmentVariables: new KeyValuePair<string, string>("SONARQUBE_SCANNER_PARAMS", """{"sonar.scanner.scanAll": "false"}"""));

        telemetry.Should()
            .BeEquivalentTo(new Dictionary<string, object>
            {
                { "dotnetenterprise.s4net.params.cmd_line1.source", "CLI" },
                { "dotnetenterprise.s4net.params.sonar_log_level.source", "CLI" },
                { "dotnetenterprise.s4net.params.sonar_scanner_scanall.source", "SONARQUBE_SCANNER_PARAMS" },
                { "dotnetenterprise.s4net.serverInfo.product", "SQ_Server" },
                { "dotnetenterprise.s4net.serverInfo.serverUrl", "custom_url" }
            });
    }

    [TestMethod]
    public async Task Execute_WritesTelemetry_SetViaMultipleSources_ProviderWithHighestPriorityIsWritten()
    {
        var args = new List<string>(CreateArgs())
        {
            "/d:sonar.scanner.scanAll=false"
        };
        var telemetry = await SetUpTest(args, new KeyValuePair<string, string>("SONARQUBE_SCANNER_PARAMS", """{"sonar.scanner.scanAll": "true"}"""));

        telemetry.Should()
            .BeEquivalentTo(new Dictionary<string, object>
            {
                { "dotnetenterprise.s4net.params.cmd_line1.source", "CLI" },
                { "dotnetenterprise.s4net.params.sonar_log_level.source", "CLI" },
                { "dotnetenterprise.s4net.params.sonar_scanner_scanall.source", "CLI" },
                { "dotnetenterprise.s4net.serverInfo.product", "SQ_Server" },
                { "dotnetenterprise.s4net.serverInfo.serverUrl", "custom_url" }
            });
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

    private async Task<Dictionary<string, object>> SetUpTest(IEnumerable<string> args = null, params KeyValuePair<string, string>[] environmentVariables)
    {
        using var context = new Context(TestContext);
        using var env = new EnvironmentVariableScope();
        foreach (var envVariable in environmentVariables)
        {
            env.SetVariable(envVariable.Key, envVariable.Value);
        }

        (await context.Execute(args)).Should().BeTrue();
        return TelemetryContent(context.Factory);
    }

    private static Dictionary<string, object> TelemetryContent(MockObjectFactory factory)
    {
        var expectedTelemetryLocation = factory.ReadSettings().SonarOutputDirectory;
        factory.Runtime.Logger.TelemetryOutputPath.Should().Be(expectedTelemetryLocation);
        return factory.Runtime.Logger.TelemetryMessages;
    }
}
