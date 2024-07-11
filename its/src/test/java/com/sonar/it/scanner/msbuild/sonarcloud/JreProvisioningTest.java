/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import com.sonar.orchestrator.util.StreamConsumer;
import java.io.File;
import java.io.IOException;
import java.io.StringWriter;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static org.assertj.core.api.Assertions.assertThat;

class JreProvisioningTest {
  private final static Logger LOG = LoggerFactory.getLogger(JreProvisioningTest.class);
  private final static Integer COMMAND_TIMEOUT = 2 * 60 * 1000;
  private final static String SCANNER_PATH = "../build/sonarscanner-net-framework/SonarScanner.MSBuild.exe";
  private final static String SONARCLOUD_PROJECT_KEY = "team-lang-dotnet_jre-provisioning";
  private final static String PROJECT_NAME = "JreProvisioning";

  @TempDir
  public Path basePath;

  @Test
  void different_hostUrl_sonarcloudUrl_logsAndExitsEarly() {
    var logWriter = new StringWriter();
    StreamConsumer.Pipe logsConsumer = new StreamConsumer.Pipe(logWriter);

    var beginCommand = Command.create(new File(SCANNER_PATH).getAbsolutePath())
      .addArgument("begin")
      .addArgument("/o:org")
      .addArgument("/k:project")
      .addArgument("/d:sonar.host.url=http://localhost:4242")
      .addArgument("/d:sonar.scanner.sonarcloudUrl=" + Constants.SONARCLOUD_URL);

    LOG.info("Scanner path: " + SCANNER_PATH);
    LOG.info("Command line: " + beginCommand.toCommandLine());
    var beginResult = CommandExecutor.create().execute(beginCommand, logsConsumer, COMMAND_TIMEOUT);
    assertThat(beginResult).isOne();

    assertThat(logWriter.toString()).contains(
      "The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set and are different. Please set either 'sonar.host" +
        ".url' for SonarQube or 'sonar.scanner.sonarcloudUrl' for SonarCloud.");
  }

  @Test
  void jreProvisioning_skipProvisioning_doesNotDownloadJre() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);
    var logWriter = new StringWriter();
    StreamConsumer.Pipe logsConsumer = new StreamConsumer.Pipe(logWriter);

    SonarCloudUtils.runBeginStep(projectDir,SONARCLOUD_PROJECT_KEY, logsConsumer, "/d:sonar.scanner.skipJreProvisioning=true");

    assertThat(logWriter.toString()).contains(
      "JreResolver: Resolving JRE path.",
      "JreResolver: sonar.scanner.skipJreProvisioning is set, skipping JRE provisioning.");
    assertThat(logWriter.toString()).doesNotContain(
      "JreResolver: Cache miss.",
      "JreResolver: Cache hit",
      "JreResolver: Cache failure.");
  }

  @Test
  void jreProvisioning_endToEnd_cacheMiss_downloadsJre() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);

    var logs = SonarCloudUtils.runAnalysis(projectDir, SONARCLOUD_PROJECT_KEY, "/d:sonar.userHome=" + projectDir.toAbsolutePath());

    var root = projectDir.toAbsolutePath().toString().replace("\\", "\\\\");
    // begin step
    assertThat(logs).contains(
      "JreResolver: Resolving JRE path.",
      "Downloading from " + Constants.SONARCLOUD_API_URL + "/analysis/jres?os=windows&arch=x64...",
      "Response received from " + Constants.SONARCLOUD_API_URL + "/analysis/jres?os=windows&arch=x64...",
      "JreResolver: Cache miss. Attempting to download JRE.",
      "Starting the Java Runtime Environment download.");
    TestUtils.matchesSingleLine(logs, "Downloading Java JRE from https://.+/jres/.+.zip");
    TestUtils.matchesSingleLine(logs, "The checksum of the downloaded file is '.+' and the expected checksum is '.+'");
    TestUtils.matchesSingleLine(logs,
      "Starting extracting the Java runtime environment from archive '" + root + "\\\\cache.+' to folder '" + root +
        "\\\\cache.+'");
    TestUtils.matchesSingleLine(logs,
      "Moving extracted Java runtime environment from '" + root + "\\\\cache.+' to '" + root + "\\\\cache" +
        ".+_extracted'");
    TestUtils.matchesSingleLine(logs, "The Java runtime environment was successfully added to '" + root + "\\\\cache.+_extracted'");
    TestUtils.matchesSingleLine(logs, "JreResolver: Download success. JRE can be found at '" + root + "\\\\cache.+_extracted.+java.exe'");
    // end step
    TestUtils.matchesSingleLine(logs, "Setting the JAVA_HOME for the scanner cli to " + root + "\\\\cache.+_extracted.+");
    TestUtils.matchesSingleLine(logs, "Overwriting the value of environment variable 'JAVA_HOME'. Old value: .+, new value: " + root +
      "\\\\cache.+extracted.+");
  }
}