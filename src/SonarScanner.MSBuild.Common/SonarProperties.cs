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

using System.Collections.ObjectModel;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Defines symbolic names for common SonarQube properties.
/// </summary>
public static class SonarProperties
{
    // SonarCloud server settings
    public const string CacheBaseUrl = "sonar.sensor.cache.baseUrl";

    // SonarQube server settings
    public const string HostUrl = "sonar.host.url";

    public const string JavaExePath = "sonar.scanner.javaExePath";
    public const string SkipJreProvisioning = "sonar.scanner.skipJreProvisioning";
    public const string EngineJarPath = "sonar.scanner.engineJarPath";
    public const string SonarToken = "sonar.token";
    public const string SonarUserName = "sonar.login"; // Deprecated by SonarQube
    public const string SonarPassword = "sonar.password"; // Deprecated by SonarQube

    public const string SonarcloudUrl = "sonar.scanner.sonarcloudUrl";
    public const string ApiBaseUrl = "sonar.scanner.apiBaseUrl";
    public const string OperatingSystem = "sonar.scanner.os";
    public const string Architecture = "sonar.scanner.arch";
    public const string ConnectTimeout = "sonar.scanner.connectTimeout";
    public const string SocketTimeout = "sonar.scanner.socketTimeout";
    public const string ResponseTimeout = "sonar.scanner.responseTimeout";
    public const string UserHome = "sonar.userHome";
    public const string ScanAllAnalysis = "sonar.scanner.scanAll";
    public const string TruststorePath = "sonar.scanner.truststorePath";
    public const string TruststorePassword = "sonar.scanner.truststorePassword";

    // SonarQube project settings
    public const string ProjectKey = "sonar.projectKey";

    public const string ProjectBranch = "sonar.branch";

    public const string ProjectName = "sonar.projectName";
    public const string ProjectVersion = "sonar.projectVersion";

    // Miscellaneous
    public const string SourceEncoding = "sonar.sourceEncoding";

    public const string ProjectBaseDir = "sonar.projectBaseDir";
    public const string PullRequestBase = "sonar.pullrequest.base";
    public const string PullRequestCacheBasePath = "sonar.pullrequest.cache.basepath";
    public const string WorkingDirectory = "sonar.working.directory";
    public const string PluginCacheDirectory = "sonar.plugin.cache.directory";
    public const string Verbose = "sonar.verbose";
    public const string LogLevel = "sonar.log.level";

    public const string Organization = "sonar.organization";
    public const string Region = "sonar.region";

    public const string VsCoverageXmlReportsPaths = "sonar.cs.vscoveragexml.reportsPaths";
    public const string VsTestReportsPaths = "sonar.cs.vstest.reportsPaths";

    public const string ClientCertPath = "sonar.clientcert.path";
    public const string ClientCertPassword = "sonar.clientcert.password";

    public const string HttpTimeout = "sonar.http.timeout";
    public const string Sources = "sonar.sources";
    public const string Tests = "sonar.tests";

    // JVM properties
    public const string JavaxNetSslTrustStore = "javax.net.ssl.trustStore";
    public const string JavaxNetSslTrustStorePassword = "javax.net.ssl.trustStorePassword";
    public const string JavaxNetSslTrustStoreType = "javax.net.ssl.trustStoreType";

    /// <summary>
    /// Strings that are used to indicate arguments that contain sensitive data that should not be logged.
    /// </summary>
    public static readonly IEnumerable<string> SensitivePropertyKeys =
    [
        SonarToken,
        SonarPassword,
        SonarUserName,
        ClientCertPassword,
        TruststorePassword,
        JavaxNetSslTrustStorePassword
    ];
}

public static class SonarPropertiesDefault
{
    public static readonly ReadOnlyCollection<string> TruststorePasswords = new(["changeit", "sonar"]);

    public static readonly string TruststorePath = Path.Combine("ssl", "truststore.p12");
}
