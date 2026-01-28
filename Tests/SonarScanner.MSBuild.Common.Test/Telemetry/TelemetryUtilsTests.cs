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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class TelemetryUtilsTests
{
#pragma warning disable S103 // Lines should not be too long
    [TestMethod]
    // Sensitive data
    [DataRow(SonarProperties.SonarToken, "secret")]
    [DataRow("SONAR.TOKEN", "secret")]
    [DataRow(SonarProperties.SonarPassword, "secret")]
    [DataRow(SonarProperties.SonarUserName, "secret")]
    [DataRow(SonarProperties.ClientCertPassword, "secret")]
    [DataRow(SonarProperties.TruststorePassword, "secret")]
    [DataRow(SonarProperties.JavaxNetSslTrustStorePassword, "secret")]
    [DataRow(SonarProperties.Organization, "secret")]
    [DataRow(SonarProperties.ProjectKey, "secret")]
    // Excluded
    [DataRow(SonarProperties.HostUrl, "secret")]
    [DataRow(SonarProperties.ApiBaseUrl, "secret")]
    [DataRow(SonarProperties.SonarcloudUrl, "secret")]
    [DataRow(SonarProperties.Region, "secret")]
    // File paths
    [DataRow(SonarProperties.ClientCertPath, "path/to/cert.pfx", "dotnetenterprise.s4net.params.sonar_clientcert_path.source=CLI", "dotnetenterprise.s4net.params.sonar_clientcert_path.value=.pfx")]
    [DataRow(SonarProperties.TruststorePath, "path/to/cert.p12", "dotnetenterprise.s4net.params.sonar_scanner_truststorepath.source=CLI", "dotnetenterprise.s4net.params.sonar_scanner_truststorepath.value=.p12")]
    [DataRow(SonarProperties.JavaExePath, "path/to/java", "dotnetenterprise.s4net.params.sonar_scanner_javaexepath.source=CLI")]
    [DataRow(SonarProperties.JavaExePath, "path/to/java.exe", "dotnetenterprise.s4net.params.sonar_scanner_javaexepath.source=CLI", "dotnetenterprise.s4net.params.sonar_scanner_javaexepath.value=.exe")]
    [DataRow(SonarProperties.JavaExePath, "", "dotnetenterprise.s4net.params.sonar_scanner_javaexepath.source=CLI")]
    // Directories
    [DataRow(SonarProperties.PullRequestCacheBasePath, "/SomePath", "dotnetenterprise.s4net.params.sonar_pullrequest_cache_basepath.source=CLI", "dotnetenterprise.s4net.params.sonar_pullrequest_cache_basepath.value=rooted")]
    [DataRow(SonarProperties.VsCoverageXmlReportsPaths, "/SomePath", "dotnetenterprise.s4net.params.sonar_cs_vscoveragexml_reportspaths.source=CLI", "dotnetenterprise.s4net.params.sonar_cs_vscoveragexml_reportspaths.value=rooted")]
    [DataRow(SonarProperties.VsTestReportsPaths, "/SomePath", "dotnetenterprise.s4net.params.sonar_cs_vstest_reportspaths.source=CLI", "dotnetenterprise.s4net.params.sonar_cs_vstest_reportspaths.value=rooted")]
    [DataRow(SonarProperties.PluginCacheDirectory, "/SomePath", "dotnetenterprise.s4net.params.sonar_plugin_cache_directory.source=CLI", "dotnetenterprise.s4net.params.sonar_plugin_cache_directory.value=rooted")]
    [DataRow(SonarProperties.ProjectBaseDir, "/SomePath", "dotnetenterprise.s4net.params.sonar_projectbasedir.source=CLI", "dotnetenterprise.s4net.params.sonar_projectbasedir.value=rooted")]
    [DataRow(SonarProperties.UserHome, "/SomePath", "dotnetenterprise.s4net.params.sonar_userhome.source=CLI", "dotnetenterprise.s4net.params.sonar_userhome.value=rooted")]
    [DataRow(SonarProperties.WorkingDirectory, "/SomePath", "dotnetenterprise.s4net.params.sonar_working_directory.source=CLI", "dotnetenterprise.s4net.params.sonar_working_directory.value=rooted")]
    [DataRow(SonarProperties.WorkingDirectory, "SomePath", "dotnetenterprise.s4net.params.sonar_working_directory.source=CLI", "dotnetenterprise.s4net.params.sonar_working_directory.value=relative")]
    // Some wellknown properties with confidential data
    [DataRow(SonarProperties.ProjectBranch, "someValue", "dotnetenterprise.s4net.params.sonar_branch.source=CLI")]
    [DataRow(SonarProperties.ProjectName, "someValue", "dotnetenterprise.s4net.params.sonar_projectname.source=CLI")]
    [DataRow(SonarProperties.ProjectVersion, "someValue", "dotnetenterprise.s4net.params.sonar_projectversion.source=CLI")]
    [DataRow(SonarProperties.Sources, "someValue", "dotnetenterprise.s4net.params.sonar_sources.source=CLI")]
    [DataRow(SonarProperties.Tests, "someValue", "dotnetenterprise.s4net.params.sonar_tests.source=CLI")]
    // Whitelisted properties
    [DataRow(SonarProperties.OperatingSystem, "Windows", "dotnetenterprise.s4net.params.sonar_scanner_os.source=CLI", "dotnetenterprise.s4net.params.sonar_scanner_os.value=Windows")]
    [DataRow(SonarProperties.Architecture, "x64", "dotnetenterprise.s4net.params.sonar_scanner_arch.source=CLI", "dotnetenterprise.s4net.params.sonar_scanner_arch.value=x64")]
    [DataRow(SonarProperties.SourceEncoding, "UTF-8", "dotnetenterprise.s4net.params.sonar_sourceencoding.source=CLI", "dotnetenterprise.s4net.params.sonar_sourceencoding.value=UTF-8")]
    [DataRow(SonarProperties.JavaxNetSslTrustStoreType, "Windows-ROOT", "dotnetenterprise.s4net.params.javax_net_ssl_truststoretype.source=CLI", "dotnetenterprise.s4net.params.javax_net_ssl_truststoretype.value=Windows-ROOT")]
    // Unknown, pass-through properties
    [DataRow("something", "value", "dotnetenterprise.s4net.params.something.source=CLI")]
    [DataRow("something.other", "value", "dotnetenterprise.s4net.params.something_other.source=CLI")]
    [DataRow("Something.Other", "value", "dotnetenterprise.s4net.params.something_other.source=CLI")]
#pragma warning restore S103 // Lines should not be too long
    public void LoggedTelemetryFromProperties(string propertyId, string value, params string[] exepectedTelemetry) =>
        AssertTelemetry(propertyId, value, exepectedTelemetry);

    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    [DataRow(SonarProperties.JavaExePath, "invalidFileName.exe;*>\0//", "dotnetenterprise.s4net.params.sonar_scanner_javaexepath.source=CLI")]
    public void LoggedTelemetryFromPropertiesNoMacOS(string propertyId, string value, params string[] exepectedTelemetry) =>
        AssertTelemetry(propertyId, value, exepectedTelemetry);

    [TestMethod]
    [DataRow("http://localhost:9000", "localhost")]
    [DataRow("private.com", "custom_url")]
    public void LoggedTelemetryFromHostInfoSqServer(string serverUrl, string telemetryValue)
    {
        var telemetry = new TestTelemetry();
        var serverInfo = new ServerHostInfo(serverUrl, serverUrl);
        TelemetryUtils.AddTelemetry(telemetry, serverInfo);
        telemetry.Should().HaveMessage(TelemetryKeys.ServerInfoProduct, "SQ_Server")
            .And.HaveMessage(TelemetryKeys.ServerInfoServerUrl, telemetryValue);
    }

    [TestMethod]
    [DataRow("https://sonarcloud.io", "", "https://sonarcloud.io", "default")]
    [DataRow("https://sonarcloud.io", "region", "https://sonarcloud.io", "region")]
    [DataRow("https://sonarqube.us", "us", "https://sonarqube.us", "us")]
    [DataRow("private/server", "region", "custom_url", "region")]
    public void LoggedTelemetryFromHostInfoSqCloud(string serverUrl, string region, string telemetryUrlValue, string telemetryRegionValue)
    {
        var telemetry = new TestTelemetry();
        var serverInfo = new CloudHostInfo(serverUrl, serverUrl, region);
        TelemetryUtils.AddTelemetry(telemetry, serverInfo);
        telemetry.Should().HaveMessage(TelemetryKeys.ServerInfoProduct, "SQ_Cloud")
            .And.HaveMessage(TelemetryKeys.ServerInfoServerUrl, telemetryUrlValue)
            .And.HaveMessage(TelemetryKeys.ServerInfoRegion, telemetryRegionValue);
    }

    [TestMethod]
    public void AddCIEnvironmentTelemetry_NoCIEnvironment_DoesNotAddTelemetry()
    {
        using var scope = new EnvironmentVariableScope();
        var telemetry = new TestTelemetry();
        TelemetryUtils.AddCIEnvironmentTelemetry(telemetry);
        telemetry.Messages.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("GITHUB_ACTIONS", "true", "GitHubActions")]
    [DataRow("TF_BUILD", "true", "AzureDevops")]
    [DataRow("GITLAB_CI", "true", "GitLabCI")]
    [DataRow("JENKINS_URL", "http://jenkins/", "Jenkins")]
    [DataRow("TEAMCITY_VERSION", "2023.11", "TeamCity")]
    [DataRow("BUILDKITE", "true", "Buildkite")]
    public void AddCIEnvironmentTelemetry_CIEnvironmentDetected_AddsTelemetry(string envVar, string value, string expectedPlatform)
    {
        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(envVar, value);
        var telemetry = new TestTelemetry();
        TelemetryUtils.AddCIEnvironmentTelemetry(telemetry);
        telemetry.Should().HaveMessage("dotnetenterprise.s4net.ci_platform", expectedPlatform);
    }

    [TestMethod]
    [DataRow("sonar.token", "dotnetenterprise.s4net.params.sonar_token")]
    [DataRow("SONAR.TOKEN", "dotnetenterprise.s4net.params.sonar_token")]
    [DataRow("sonar.host.url", "dotnetenterprise.s4net.params.sonar_host_url")]
    [DataRow("sonar.projectBaseDir", "dotnetenterprise.s4net.params.sonar_projectbasedir")]
    [DataRow("sonar.cs.vscoveragexml.reportsPaths", "dotnetenterprise.s4net.params.sonar_cs_vscoveragexml_reportspaths")]
    [DataRow("simple", "dotnetenterprise.s4net.params.simple")]
    [DataRow("UPPERCASE", "dotnetenterprise.s4net.params.uppercase")]
    [DataRow("with-dash", "dotnetenterprise.s4net.params.with_dash")]
    [DataRow("with_underscore", "dotnetenterprise.s4net.params.with_underscore")]
    [DataRow("with.multiple...dots", "dotnetenterprise.s4net.params.with_multiple___dots")]
    [DataRow("special!@#$%chars", "dotnetenterprise.s4net.params.special_____chars")]
    [DataRow("123numeric", "dotnetenterprise.s4net.params.123numeric")]
    [DataRow("mixed123AND.special-chars_here", "dotnetenterprise.s4net.params.mixed123and_special_chars_here")]
    [DataRow("", "dotnetenterprise.s4net.params.")]
    public void ToTelemetryId_SanitizesPropertyName(string property, string expected) =>
        TelemetryUtils.ToTelemetryId(property).Should().Be(expected);

    private static void AssertTelemetry(string propertyId, string value, string[] exepectedTelemetry)
    {
        var telemetry = new TestTelemetry();
        var list = new ListPropertiesProvider(PropertyProviderKind.CLI);
        list.AddProperty(propertyId, value);
        var provider = new AggregatePropertiesProvider(list);
        TelemetryUtils.AddTelemetry(telemetry, provider);
        telemetry.Messages.Should().HaveCount(exepectedTelemetry.Length);
        foreach (var expected in exepectedTelemetry)
        {
            var parts = expected.Split('=');
            var (expectedPropertyId, expectedValue) = (parts[0], parts[1]);
            telemetry.Should().HaveMessage(expectedPropertyId, expectedValue);
        }
    }
}
