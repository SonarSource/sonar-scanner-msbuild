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

import com.sonar.it.scanner.msbuild.utils.Property;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import java.io.IOException;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static org.assertj.core.api.Assertions.assertThat;

class JreProvisioningTest {
  private static final Logger LOG = LoggerFactory.getLogger(JreProvisioningTest.class);
  private static final String SONARCLOUD_PROJECT_KEY = "team-lang-dotnet_jre-provisioning";
  private static final String PROJECT_NAME = "JreProvisioning";

  @TempDir
  public Path basePath;
//
//  @Test
//  void different_hostUrl_sonarcloudUrl_logsAndExitsEarly() {
//    var logWriter = new StringWriter();
//    StreamConsumer.Pipe logsConsumer = new StreamConsumer.Pipe(logWriter);
//
//    var beginCommand = Command.create(new File(Constants.SCANNER_PATH).getAbsolutePath())
//      .addArgument("begin")
//      .addArgument("/o:org")
//      .addArgument("/k:project")
//      .addArgument("/d:sonar.host.url=http://localhost:4242")
//      .addArgument("/d:sonar.scanner.sonarcloudUrl=" + Constants.SONARCLOUD_URL);
//
//    LOG.info("Scanner path: {}", Constants.SCANNER_PATH);
//    LOG.info("Command line: {}", beginCommand.toCommandLine());
//    var beginResult = CommandExecutor.create().execute(beginCommand, logsConsumer, Constants.COMMAND_TIMEOUT);
//    assertThat(beginResult).isOne();
//
//    assertThat(logWriter.toString()).contains(
//      "The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set and are different. Please set either 'sonar.host" +
//        ".url' for SonarQube or 'sonar.scanner.sonarcloudUrl' for SonarCloud.");
//  }
//
//  @Test
//  void jreProvisioning_skipProvisioning_doesNotDownloadJre() throws IOException {
//    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);
//    var result = SonarCloudUtils.runBeginStep(projectDir, SONARCLOUD_PROJECT_KEY); // sonar.scanner.skipJreProvisioning=true is the default behavior of ScannerCommand in ITs
//
//    assertThat(result.getLogs()).contains(
//      "JreResolver: Resolving JRE path.",
//      "JreResolver: sonar.scanner.skipJreProvisioning is set, skipping JRE provisioning.");
//    assertThat(result.getLogs()).doesNotContain(
//      "JreResolver: Cache miss.",
//      "JreResolver: Cache hit",
//      "JreResolver: Cache failure.");
//  }
//
//  @Test
//  void jreProvisioning_endToEnd_cacheMiss_downloadsJre() throws IOException {
//    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);
//
//    var logs = SonarCloudUtils.runAnalysis(projectDir, SONARCLOUD_PROJECT_KEY, new Property("sonar.userHome", projectDir.toAbsolutePath().toString()));
//
//    var root = projectDir.toAbsolutePath().toString().replace("\\", "\\\\");
//    // begin step
//    assertThat(logs).contains(
//      "JreResolver: Resolving JRE path.",
//      "Downloading from " + Constants.SONARCLOUD_API_URL + "/analysis/jres?os=windows&arch=x64...",
//      "Response received from " + Constants.SONARCLOUD_API_URL + "/analysis/jres?os=windows&arch=x64...",
//      "JreResolver: Cache miss. Attempting to download JRE.",
//      "Starting the Java Runtime Environment download.");
//    TestUtils.matchesSingleLine(logs, "Downloading Java JRE from https://.+/jres/.+.zip");
//    TestUtils.matchesSingleLine(logs, "The checksum of the downloaded file is '.+' and the expected checksum is '.+'");
//    TestUtils.matchesSingleLine(logs,
//      "Starting extracting the Java runtime environment from archive '" + root + "\\\\cache.+' to folder '" + root +
//        "\\\\cache.+'");
//    TestUtils.matchesSingleLine(logs,
//      "Moving extracted Java runtime environment from '" + root + "\\\\cache.+' to '" + root + "\\\\cache" +
//        ".+_extracted'");
//    TestUtils.matchesSingleLine(logs, "The Java runtime environment was successfully added to '" + root + "\\\\cache.+_extracted'");
//    TestUtils.matchesSingleLine(logs, "JreResolver: Download success. JRE can be found at '" + root + "\\\\cache.+_extracted.+java.exe'");
//    // end step
//    TestUtils.matchesSingleLine(logs, "Setting the JAVA_HOME for the scanner cli to " + root + "\\\\cache.+_extracted.+");
//    TestUtils.matchesSingleLine(logs, "Overwriting the value of environment variable 'JAVA_HOME'. Old value: .+, new value: " + root +
//      "\\\\cache.+extracted.+");
//  }

  @Test
  void jreProvisioning_endToEnd_cacheHit_reusesJre() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);
    var root = projectDir.toAbsolutePath().toString().replace("\\", "\\\\");
    var property = new Property("sonar.userHome", projectDir.toAbsolutePath().toString());

    // first analysis, cache misses and downloads the JRE
    var cacheMissLogs = SonarCloudUtils.runAnalysis(projectDir, SONARCLOUD_PROJECT_KEY, property).getLogs();
    assertThat(cacheMissLogs).contains(
      "JreResolver: Cache miss",
      "Starting the Java Runtime Environment download.");
    assertThat(cacheMissLogs).doesNotContain(
      "JreResolver: Cache hit",
      "JreResolver: Cache failure");

    // second analysis, cache hits and does not download the JRE
    var cacheHitLogs = SonarCloudUtils.runAnalysis(projectDir, SONARCLOUD_PROJECT_KEY, property).getLogs();
    TestUtils.matchesSingleLine(cacheHitLogs,
      "JreResolver: Cache hit '" + root + "\\\\cache.+_extracted.+java.exe'");
    assertThat(cacheHitLogs).doesNotContain(
      "JreResolver: Cache miss",
      "Starting the Java Runtime Environment download.");
  }
//
//  @Test
//  void jreProvisioning_endToEnd_parameters_propagated() throws IOException {
//    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);
//
//    SonarCloudUtils.runBeginStep(
//      projectDir,
//      SONARCLOUD_PROJECT_KEY,
//      new Property("sonar.scanner.os", "windows"),
//      new Property("sonar.scanner.arch", "x64"),
//      new Property("sonar.scanner.skipJreProvisioning", "true"),
//      new Property("sonar.scanner.connectTimeout", "42"),
//      new Property("sonar.scanner.socketTimeout", "100"),
//      new Property("sonar.scanner.responseTimeout", "500"),
//      new Property("sonar.userHome", projectDir.toAbsolutePath().toString()));
//
//    SonarCloudUtils.runBuild(projectDir);
//    var result = SonarCloudUtils.runEndStep(projectDir);
//
//    assertThat(result.getLogs()).contains(
//      "Dumping content of sonar-project.properties",
//      "sonar.scanner.sonarcloudUrl=" + Constants.SONARCLOUD_URL,
//      "sonar.scanner.apiBaseUrl=" + Constants.SONARCLOUD_API_URL,
//      "sonar.scanner.os=windows",
//      "sonar.scanner.arch=x64",
//      "sonar.scanner.skipJreProvisioning=true",
//      "sonar.scanner.connectTimeout=42",
//      "sonar.scanner.socketTimeout=100",
//      "sonar.scanner.responseTimeout=500",
//      "sonar.userHome=" + projectDir.toAbsolutePath().toString().replace("\\", "\\\\"));
//  }
}
