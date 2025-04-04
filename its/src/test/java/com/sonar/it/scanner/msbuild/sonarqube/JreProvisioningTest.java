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
package com.sonar.it.scanner.msbuild.sonarqube;

import com.sonar.it.scanner.msbuild.utils.AnalysisContext;
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.OSPlatform;
import com.sonar.it.scanner.msbuild.utils.TempDirectory;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import java.nio.file.Paths;
import java.util.Optional;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.junit.jupiter.api.Assumptions.assumeTrue;

@ExtendWith({ServerTests.class, ContextExtension.class})
public class JreProvisioningTest {
  private static final String DIRECTORY_NAME = "JreProvisioning";

  @Test
  void cacheMiss_DownloadsJre() {
    // provisioning does not exist before 10.6
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(10, 6));
    try (var userHome = new TempDirectory("junit-JRE-miss-")) { // context.projectDir has a test name in it and that leads to too long path
      var context = createContext(userHome);
      context.build.useDotNet();
      // JAVA_HOME might not be set in the environment, so we set it to a non-existing path
      // so we can test that we updated it correctly
      var oldJavaHome = Optional.ofNullable(System.getenv("JAVA_HOME")).orElse(Paths.get("somewhere", "else").toString());
      context.end.setEnvironmentVariable("JAVA_HOME", oldJavaHome);
      // If this fails with "Error: could not find java.dll", the temp & JRE cache path is too long
      var result = context.runAnalysis();
      var beginLogs = result.begin().getLogs();
      var endLogs = result.end().getLogs();
      var os = OSPlatform.current().name().toLowerCase();
      var arch = OSPlatform.currentArchitecture().toLowerCase();
      var cacheFolderPattern = userHome.toString();
      oldJavaHome = oldJavaHome.replace("\\", "\\\\");
      cacheFolderPattern = cacheFolderPattern.replace("\\", "\\\\") + "[\\\\/]cache.+";

      assertThat(beginLogs).contains(
        "JreResolver: Resolving JRE path.",
        "Downloading from " + ORCHESTRATOR.getServer().getUrl() + "/api/v2/analysis/jres?os=" + os + "&arch=" + arch + "...",
        "Response received from " + ORCHESTRATOR.getServer().getUrl() + "/api/v2/analysis/jres?os=" + os + "&arch=" + arch + "...",
        "JreResolver: Cache miss. Attempting to download JRE.",
        "Starting the Java Runtime Environment download.");
      TestUtils.matchesSingleLine(beginLogs, "Downloading Java JRE from analysis/jres/.+");
      TestUtils.matchesSingleLine(beginLogs, "The checksum of the downloaded file is '.+' and the expected checksum is '.+'");
      TestUtils.matchesSingleLine(beginLogs, "Starting extracting the Java runtime environment from archive '" + cacheFolderPattern + "' to folder '" + cacheFolderPattern + "'");
      TestUtils.matchesSingleLine(beginLogs, "Moving extracted Java runtime environment from '" + cacheFolderPattern + "' to '" + cacheFolderPattern + "_extracted'");
      TestUtils.matchesSingleLine(beginLogs, "The Java runtime environment was successfully added to '" + cacheFolderPattern + "_extracted'");
      TestUtils.matchesSingleLine(beginLogs, "JreResolver: Download success. JRE can be found at '" + cacheFolderPattern + "_extracted.+java(?:\\.exe)?'");
      TestUtils.matchesSingleLine(endLogs, "Setting the JAVA_HOME for the scanner cli to " + cacheFolderPattern + "_extracted.+");
      TestUtils.matchesSingleLine(endLogs, "Overwriting the value of environment variable 'JAVA_HOME'. Old value: " + oldJavaHome + ", new value: " + cacheFolderPattern + "extracted.+");
    }
  }

  @Test
  void cacheHit_ReusesJre() {
    // provisioning does not exist before 10.6
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(10, 6));
    try (var userHome = new TempDirectory("junit-JRE-hit-")) {  // context.projectDir has a test name in it and that leads to too long path
      var context = createContext(userHome);
      // first analysis, cache misses and downloads the JRE
      var firstBegin = context.begin.execute(ORCHESTRATOR);
      var javaPattern = userHome.toString().replace("\\", "\\\\") + "[\\\\/]cache.+_extracted.+java(?:\\.exe)?";
      assertThat(firstBegin.isSuccess()).isTrue();
      assertThat(firstBegin.getLogs()).contains(
        "JreResolver: Cache miss",
        "Starting the Java Runtime Environment download.");
      assertThat(firstBegin.getLogs()).doesNotContain(
        "JreResolver: Cache hit",
        "JreResolver: Cache failure");

      // second analysis, cache hits and does not download the JRE
      var secondBegin = context.begin.execute(ORCHESTRATOR);
      assertThat(secondBegin.isSuccess()).isTrue();
      // JreResolver: Cache hit '/tmp/junit-JRE-hit-9040286380298486524/cache/4086cc7cb2d9e7810141f255063caad10a8a018db5e6b47fa5394c506ab65bff/OpenJDK17U-jre_x64_linux_hotspot_17.0.13_11.tar.gz_extracted/jdk-17.0.13+11-jre/bin/java'
      TestUtils.matchesSingleLine(secondBegin.getLogs(),
        "JreResolver: Cache hit '" + javaPattern + "'");
      assertThat(secondBegin.getLogs()).doesNotContain(
        "JreResolver: Cache miss",
        "JreResolver: Cache failure",
        "Starting the Java Runtime Environment download.");
    }
  }

  private static AnalysisContext createContext(TempDirectory userHome) {
    var context = AnalysisContext.forServer(DIRECTORY_NAME);
    context.begin
      .setProperty("sonar.userHome", userHome.toString())
      .setProperty("sonar.scanner.skipJreProvisioning", null)  // Undo the default IT behavior and use the default scanner behavior.
      .setDebugLogs();
    return context;
  }
}
