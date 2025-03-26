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
import com.sonar.it.scanner.msbuild.utils.Property;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.ScannerCommand;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;

import static org.assertj.core.api.Assertions.assertThat;
import static org.junit.jupiter.api.Assertions.assertFalse;

@ExtendWith({CloudTests.class, ContextExtension.class})
class JreProvisioningTest {
  private static final String DIRECTORY_NAME = "JreProvisioning";
  private static final Property activateProvisioning = new Property("sonar.scanner.skipJreProvisioning", null); // Default ScannerCommand behavior turns it off

  @TempDir
  public Path basePath;

  @Test
  void different_hostUrl_sonarcloudUrl_logsAndExitsEarly() {
    var result = ScannerCommand.createBeginStep(ScannerClassifier.NET_FRAMEWORK, CloudConstants.SONARCLOUD_TOKEN, basePath, "AnyKey")
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
  void jreProvisioning_skipProvisioning_doesNotDownloadJre() {
    var context = AnalysisContext.forCloud(DIRECTORY_NAME);
    var logs = context.begin.execute(null).getLogs(); // sonar.scanner.skipJreProvisioning=true is the default behavior of ScannerCommand in ITs

    assertThat(logs).contains(
      "JreResolver: Resolving JRE path.",
      "JreResolver: sonar.scanner.skipJreProvisioning is set, skipping JRE provisioning.");
    assertThat(logs).doesNotContain(
      "JreResolver: Cache miss.",
      "JreResolver: Cache hit",
      "JreResolver: Cache failure.");
  }

  @Test
  void jreProvisioning_endToEnd_cacheMiss_downloadsJre() {
    var context = AnalysisContext.forCloud(DIRECTORY_NAME);
    context.begin
      .setProperty(activateProvisioning)
      .setProperty("sonar.userHome", context.projectDir.toAbsolutePath().toString());
    var result = context.runAnalysis();

    var root = context.projectDir.toAbsolutePath().toString().replace("\\", "\\\\");
    var beginLogs = result.begin().getLogs();
    assertThat(beginLogs).contains(
      "JreResolver: Resolving JRE path.",
      "Downloading from " + CloudConstants.SONARCLOUD_API_URL + "/analysis/jres?os=windows&arch=x64...",
      "Response received from " + CloudConstants.SONARCLOUD_API_URL + "/analysis/jres?os=windows&arch=x64...",
      "JreResolver: Cache miss. Attempting to download JRE.",
      "Starting the Java Runtime Environment download.");
    TestUtils.matchesSingleLine(beginLogs, "Downloading Java JRE from https://.+/jres/.+.zip");
    TestUtils.matchesSingleLine(beginLogs, "The checksum of the downloaded file is '.+' and the expected checksum is '.+'");
    TestUtils.matchesSingleLine(beginLogs, "Starting extracting the Java runtime environment from archive '" + root + "\\\\cache.+' to folder '" + root + "\\\\cache.+'");
    TestUtils.matchesSingleLine(beginLogs, "Moving extracted Java runtime environment from '" + root + "\\\\cache.+' to '" + root + "\\\\cache" + ".+_extracted'");
    TestUtils.matchesSingleLine(beginLogs, "The Java runtime environment was successfully added to '" + root + "\\\\cache.+_extracted'");
    TestUtils.matchesSingleLine(beginLogs, "JreResolver: Download success. JRE can be found at '" + root + "\\\\cache.+_extracted.+java.exe'");
    var endLogs = result.end().getLogs();
    TestUtils.matchesSingleLine(endLogs, "Setting the JAVA_HOME for the scanner cli to " + root + "\\\\cache.+_extracted.+");
    TestUtils.matchesSingleLine(endLogs, "Overwriting the value of environment variable 'JAVA_HOME'. Old value: .+, new value: " + root + "\\\\cache.+extracted.+");
  }

  @Test
  void jreProvisioning_endToEnd_cacheHit_reusesJre() {
    var context = AnalysisContext.forCloud(DIRECTORY_NAME);
    var root = context.projectDir.toAbsolutePath().toString().replace("\\", "\\\\");
    context.begin
      .setProperty(activateProvisioning)
      .setProperty("sonar.userHome", context.projectDir.toAbsolutePath().toString());

    // first analysis, cache misses and downloads the JRE
    var cacheMissLogs = context.runAnalysis().begin().getLogs();
    assertThat(cacheMissLogs).contains(
      "JreResolver: Cache miss",
      "Starting the Java Runtime Environment download.");
    assertThat(cacheMissLogs).doesNotContain(
      "JreResolver: Cache hit",
      "JreResolver: Cache failure");

    // second analysis, cache hits and does not download the JRE
    var cacheHitLogs = context.runAnalysis().begin().getLogs();
    TestUtils.matchesSingleLine(cacheHitLogs,
      "JreResolver: Cache hit '" + root + "\\\\cache.+_extracted.+java.exe'");
    assertThat(cacheHitLogs).doesNotContain(
      "JreResolver: Cache miss",
      "Starting the Java Runtime Environment download.");
  }

  @Test
  void jreProvisioning_endToEnd_parameters_propagated() {
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
