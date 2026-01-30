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
        var telemetry = await CreateTelemetry(args);

        // Note: cmd.line1 is an unknown property and is not reported (only whitelisted properties are reported)
        telemetry.Should().HaveMessage("dotnetenterprise.s4net.params.sonar_log_level.source", "CLI")
            .And.HaveMessage("dotnetenterprise.s4net.params.sonar_scanner_scanall.source", "CLI")
            .And.HaveMessage("dotnetenterprise.s4net.serverInfo.product", "SQ_Server")
            .And.HaveMessage("dotnetenterprise.s4net.serverInfo.serverUrl", "custom_url")
            .And.HaveMessage("dotnetenterprise.s4net.serverInfo.version", "5.6");
    }

    [TestMethod]
    public async Task Execute_WritesTelemetry_SetViaAnalysisXml()
    {
        var analysisXmlPath = CreateAnalysisXml(TestContext.TestRunDirectory, new Dictionary<string, string> { { "sonar.scanner.scanAll", "false" } });
        var args = new List<string>(CreateArgs())
        {
            $"/s:{analysisXmlPath}"
        };
        var telemetry = await CreateTelemetry(args);

        // Note: cmd.line1 is an unknown property and is not reported (only whitelisted properties are reported)
        telemetry.Should().HaveMessage("dotnetenterprise.s4net.params.sonar_log_level.source", "CLI")
            .And.HaveMessage("dotnetenterprise.s4net.params.sonar_scanner_scanall.source", "SONARQUBE_ANALYSIS_XML")
            .And.HaveMessage("dotnetenterprise.s4net.serverInfo.product", "SQ_Server")
            .And.HaveMessage("dotnetenterprise.s4net.serverInfo.serverUrl", "custom_url")
            .And.HaveMessage("dotnetenterprise.s4net.serverInfo.version", "5.6");
    }

    [TestMethod]
    public async Task Execute_WritesTelemetry_SetViaEnvVariable()
    {
        var telemetry = await CreateTelemetry(environmentVariables: new KeyValuePair<string, string>("SONARQUBE_SCANNER_PARAMS", """{"sonar.scanner.scanAll": "false"}"""));

        // Note: cmd.line1 is an unknown property and is not reported (only whitelisted properties are reported)
        telemetry.Should().HaveMessage("dotnetenterprise.s4net.params.sonar_log_level.source", "CLI")
            .And.HaveMessage("dotnetenterprise.s4net.params.sonar_scanner_scanall.source", "SONARQUBE_SCANNER_PARAMS")
            .And.HaveMessage("dotnetenterprise.s4net.serverInfo.product", "SQ_Server")
            .And.HaveMessage("dotnetenterprise.s4net.serverInfo.serverUrl", "custom_url")
            .And.HaveMessage("dotnetenterprise.s4net.serverInfo.version", "5.6");
    }

    [TestMethod]
    public async Task Execute_WritesTelemetry_SetViaMultipleSources_ProviderWithHighestPriorityIsWritten()
    {
        var args = new List<string>(CreateArgs())
        {
            "/d:sonar.scanner.scanAll=false"
        };
        var telemetry = await CreateTelemetry(args, null, new KeyValuePair<string, string>("SONARQUBE_SCANNER_PARAMS", """{"sonar.scanner.scanAll": "true"}"""));

        // Note: cmd.line1 is an unknown property and is not reported (only whitelisted properties are reported)
        telemetry.Should().HaveMessage("dotnetenterprise.s4net.params.sonar_log_level.source", "CLI")
            .And.HaveMessage("dotnetenterprise.s4net.params.sonar_scanner_scanall.source", "CLI")
            .And.HaveMessage("dotnetenterprise.s4net.serverInfo.product", "SQ_Server")
            .And.HaveMessage("dotnetenterprise.s4net.serverInfo.serverUrl", "custom_url")
            .And.HaveMessage("dotnetenterprise.s4net.serverInfo.version", "5.6");
    }

    [TestMethod]
    public async Task Execute_WritesTelemetry_ServerSettings()
    {
        var serverProperties = new Dictionary<string, string>
        {
            { "sonar.cs.analyzeGeneratedCode", "false" },      // matches default → should NOT appear
            { "sonar.cs.analyzeRazorCode", "false" },          // differs from default "true" → should appear
            { "sonar.cs.ignoreHeaderComments", "true" },       // matches default → should NOT appear
            { "sonar.cs.opencover.reportsPaths", "report.xml" }, // no default → should appear
            { "sonar.exclusions", "**/*.generated.cs" },       // no default → should appear
            { "not whitelisted", "value" }
        };
        var args = new List<string>(CreateArgs())
        {
            "/d:sonar.cs.analyzeGeneratedCode=true"   // overrides server setting (server has default, CLI overrides)
        };
        var telemetry = await CreateTelemetry(args, serverProperties);

        telemetry.Should()
            // Properties with non-default values from server should appear
            .HaveMessage("dotnetenterprise.s4net.params.sonar_cs_analyzerazorcode.source", "SQ_SERVER_SETTINGS")
            .And.HaveMessage("dotnetenterprise.s4net.params.sonar_cs_opencover_reportspaths.source", "SQ_SERVER_SETTINGS")
            .And.HaveMessage("dotnetenterprise.s4net.params.sonar_exclusions.source", "SQ_SERVER_SETTINGS")
            // CLI override should appear (even though the server value matched default, CLI takes precedence)
            .And.HaveMessage("dotnetenterprise.s4net.params.sonar_cs_analyzegeneratedcode.source", "CLI")
            // Properties with default values from server should NOT appear (THIS WILL FAIL - confirming RED state)
            .And.NotHaveKey("dotnetenterprise.s4net.params.sonar_cs_ignoreheadercomments.source")
            .And.NotHaveKey("not whitelisted");
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

    private async Task<TestTelemetry> CreateTelemetry(IEnumerable<string> args = null, Dictionary<string, string> serverProperties = null, params KeyValuePair<string, string>[] environmentVariables)
    {
        using var context = new Context(TestContext);
        if (serverProperties is not null)
        {
            context.Factory.Server.DownloadProperties(null, null).ReturnsForAnyArgs(serverProperties);
        }
        using var env = new EnvironmentVariableScope();
        foreach (var envVariable in environmentVariables)
        {
            env.SetVariable(envVariable.Key, envVariable.Value);
        }

        (await context.Execute(args)).Should().BeTrue();
        var expectedTelemetryLocation = context.Factory.ReadSettings().SonarOutputDirectory;
        context.Factory.Runtime.Telemetry.OutputPath.Should().Be(expectedTelemetryLocation);
        return context.Factory.Runtime.Telemetry;
    }
}
