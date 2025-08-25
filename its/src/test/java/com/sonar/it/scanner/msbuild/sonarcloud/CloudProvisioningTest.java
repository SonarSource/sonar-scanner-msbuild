/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
package com.sonar.it.scanner.msbuild.sonarcloud;

import com.sonar.it.scanner.msbuild.utils.AnalysisContext;
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.ProvisioningAssertions;
import com.sonar.it.scanner.msbuild.utils.Property;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.ScannerCommand;
import com.sonar.it.scanner.msbuild.utils.TempDirectory;
import java.nio.file.Paths;
import java.util.Optional;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;

import static org.assertj.core.api.Assertions.assertThat;
import static org.junit.jupiter.api.Assertions.assertFalse;

@ExtendWith({CloudTests.class, ContextExtension.class})
class CloudProvisioningTest {
  private static final String DIRECTORY_NAME = "JreProvisioning";
  private static final Property activateProvisioning = new Property("sonar.scanner.skipJreProvisioning", null); // Default ScannerCommand behavior turns it off

  @Test
  void different_HostUrl_SonarcloudUrl_LogsAndExitsEarly() {
    var result = ScannerCommand.createBeginStep(ScannerClassifier.NET, CloudConstants.SONARCLOUD_TOKEN, ContextExtension.currentTempDir(), "AnyKey")
      .setOrganization("org")
      .setProperty("sonar.host.url", "http://localhost:4242")
      .setProperty("sonar.scanner.sonarcloudUrl", CloudConstants.SONARCLOUD_URL)
      .execute(null);

    assertFalse(result.isSuccess());
    assertThat(result.getLogs()).contains(
      "The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set and are different." +
      " Please set either 'sonar.host.url' for SonarQube or 'sonar.scanner.sonarcloudUrl' for SonarCloud.");
  }

  @Test
  void skipProvisioning_DoesNotDownloadJre() {
    var context = AnalysisContext.forCloud(DIRECTORY_NAME);
    var logs = context.begin.execute(null).getLogs(); // sonar.scanner.skipJreProvisioning=true is the default behavior of ScannerCommand in ITs

    assertThat(logs)
      .contains(
        "JreResolver: Resolving JRE path.",
        "JreResolver: sonar.scanner.skipJreProvisioning is set, skipping JRE provisioning.")
      .doesNotContain(
        "JreResolver: Cache miss.",
        "JreResolver: Cache hit",
        "JreResolver: Cache failure.");
  }

  @Test
  void cacheMiss_DownloadsJre() {
    var context = AnalysisContext.forCloud(DIRECTORY_NAME);
    try (var userHome = new TempDirectory("junit-JRE-miss-")) { // context.projectDir has a test name in it and that leads to too long path
      context.begin
        .setProperty(activateProvisioning)
        .setProperty("sonar.userHome", userHome.toString());
      // If this fails with "Error: could not find java.dll", the temp & JRE cache path is too long
      var oldJavaHome = Optional.ofNullable(System.getenv("JAVA_HOME")).orElse(Paths.get("somewhere", "else").toString());
      context.end.setEnvironmentVariable("JAVA_HOME", oldJavaHome);

      var result = context.runAnalysis();

      ProvisioningAssertions.cacheMissAssertions(result, CloudConstants.SONARCLOUD_API_URL, userHome.toString(), oldJavaHome, "https://[^\s]+/jres/[^\s]+\\.(?:zip|tar\\.gz)");
    }
  }

  @Test
  void cacheHit_ReusesJre() {
    var context = AnalysisContext.forCloud(DIRECTORY_NAME);
    try (var userHome = new TempDirectory("junit-JRE-hit-")) { // context.projectDir has a test name in it and that leads to too long path
      context.begin
        .setProperty(activateProvisioning)
        .setProperty("sonar.userHome", userHome.toString());

      // First analysis, cache misses and downloads the JRE
      // If this fails with "Error: could not find java.dll", the temp & JRE cache path is too long
      var cacheMissLogs = context.runAnalysis().begin().getLogs();
      assertThat(cacheMissLogs).contains(
        "JreResolver: Cache miss",
        "Starting the file download.");
      assertThat(cacheMissLogs).doesNotContain(
        "JreResolver: Cache hit",
        "JreResolver: Cache failure");

      // Second analysis, cache hits and does not download the JRE
      var secondBegin = context.runAnalysis().begin();
      ProvisioningAssertions.cacheHitAssertions(secondBegin, userHome.toString());
    }
  }

  @Test
  void parameters_Propagated() {
    var context = AnalysisContext.forCloud(DIRECTORY_NAME);
    context.begin
      .setProperty(activateProvisioning)
      .setProperty("sonar.scanner.os", "windows")
      .setProperty("sonar.scanner.arch", "x64")
      .setProperty("sonar.scanner.skipJreProvisioning", "true")
      .setProperty("sonar.scanner.connectTimeout", "42")
      .setProperty("sonar.scanner.socketTimeout", "100")
      .setProperty("sonar.scanner.responseTimeout", "500")
      .setProperty("sonar.userHome", context.projectDir.toAbsolutePath().toString());
    var logs = context.runAnalysis().end().getLogs();

    assertThat(logs).contains(
      "Dumping content of sonar-project.properties",
      "sonar.scanner.sonarcloudUrl=" + CloudConstants.SONARCLOUD_URL,
      "sonar.scanner.apiBaseUrl=" + CloudConstants.SONARCLOUD_API_URL,
      "sonar.scanner.os=windows",
      "sonar.scanner.arch=x64",
      "sonar.scanner.skipJreProvisioning=true",
      "sonar.scanner.connectTimeout=42",
      "sonar.scanner.socketTimeout=100",
      "sonar.scanner.responseTimeout=500",
      "sonar.userHome=" + context.projectDir.toAbsolutePath().toString().replace("\\", "\\\\"));
  }
}
