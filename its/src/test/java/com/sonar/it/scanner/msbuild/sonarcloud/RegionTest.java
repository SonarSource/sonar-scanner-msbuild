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

import java.nio.file.Path;
import org.junit.jupiter.api.io.TempDir;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class RegionTest {
  private static final Logger LOG = LoggerFactory.getLogger(RegionTest.class);
  private static final String SONARCLOUD_PROJECT_KEY = "team-lang-dotnet_region-parameter";
  private static final String PROJECT_NAME = "ProjectUnderTest";

  @TempDir
  public Path basePath;
//
//  @Test
//  void region_us() throws IOException {
//    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);
//    var logWriter = new StringWriter();
//    StreamConsumer.Pipe logsConsumer = new StreamConsumer.Pipe(logWriter);
//
//    var beginCommand = Command.create(new File(Constants.SCANNER_PATH).getAbsolutePath())
//      .setDirectory(projectDir.toFile())
//      .addArgument("begin")
//      .addArgument("/o:" + Constants.SONARCLOUD_ORGANIZATION)
//      .addArgument("/k:" + SONARCLOUD_PROJECT_KEY)
//      .addArgument("/d:sonar.region=us")
//      .addArgument("/d:sonar.verbose=true");
//
//    var beginResult = CommandExecutor.create().execute(beginCommand, logsConsumer, Constants.COMMAND_TIMEOUT);
//    assertThat(beginResult).isOne(); // Indicates error
//    assertThat(logWriter.toString()).containsSubsequence(
//      "Server Url: https://sonarqube.us",
//      "Api Url: https://api.sonarqube.us",
//      "Is SonarCloud: True",
//      "Downloading from https://sonarqube.us/api/settings/values?component=unknown",
//      "Downloading from https://api.sonarqube.us/analysis/version",
//      "Using SonarCloud.",
//      "JreResolver: Resolving JRE path.",
//      "Downloading from https://sonarqube.us/api/settings/values?component=team-lang-dotnet_region-parameter...",
//      "Cannot download quality profile. Check scanner arguments and the reported URL for more information.",
//      "Pre-processing failed. Exit code: 1");
//  }
}
