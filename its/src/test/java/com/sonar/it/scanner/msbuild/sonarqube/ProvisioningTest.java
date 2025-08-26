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
import com.sonar.it.scanner.msbuild.utils.ProvisioningAssertions;
import com.sonar.it.scanner.msbuild.utils.ServerMinVersion;
import com.sonar.it.scanner.msbuild.utils.TempDirectory;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Optional;
import java.util.stream.Stream;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;

@ExtendWith({ServerTests.class, ContextExtension.class})
class ProvisioningTest {
  private static final String DIRECTORY_NAME = "JreProvisioning";

  @Test
  // provisioning does not exist before 10.6
  @ServerMinVersion("10.6")
  void cacheMiss_DownloadsCache() {
    try (var userHome = new TempDirectory("junit-cache-miss-")) { // context.projectDir has a test name in it and that leads to too long path
      var context = createContext(userHome);
      context.build.useDotNet();
      // JAVA_HOME might not be set in the environment, so we set it to a non-existing path
      // so we can test that we updated it correctly
      var oldJavaHome = Optional.ofNullable(System.getenv("JAVA_HOME")).orElse(Paths.get("somewhere", "else").toString());
      context.end.setEnvironmentVariable("JAVA_HOME", oldJavaHome);
      // If this fails with "Error: could not find java.dll", the temp & JRE cache path is too long
      var result = context.runAnalysis();

      ProvisioningAssertions.cacheMissAssertions(result, ORCHESTRATOR.getServer().getUrl() + "/api/v2", userHome.toString(), oldJavaHome, "analysis/jres/[^\s]+");
    }
  }

  @Test
  // provisioning does not exist before 10.6
  @ServerMinVersion("10.6")
  void cacheHit_ReusesCachedFiles() {
    try (var userHome = new TempDirectory("junit-cache-hit-")) {  // context.projectDir has a test name in it and that leads to too long path
      var context = createContext(userHome);
      // first analysis, cache misses and downloads the JRE & scanner-engine
      var firstBegin = context.begin.execute(ORCHESTRATOR);
      assertThat(firstBegin.isSuccess()).isTrue();
      assertThat(firstBegin.getLogs()).contains(
        "JreResolver: Resolving JRE path.",
        "JreResolver: Cache miss. Attempting to download JRE",
        "JreResolver: Download success. JRE can be found at '",
        "EngineResolver: Resolving Scanner Engine path.",
        "EngineResolver: Cache miss. Attempting to download Scanner Engine",
        "EngineResolver: Download success. Scanner Engine can be found at '");

      assertThat(firstBegin.getLogs()).doesNotContain(
        "JreResolver: Cache hit",
        "JreResolver: Cache failure",
        "EngineResolver: Cache hit",
        "EngineResolver: Cache failure");

      // second analysis, cache hits and does not download the JRE or scanner-engine
      var secondBegin = context.begin.execute(ORCHESTRATOR);

      ProvisioningAssertions.cacheHitAssertions(secondBegin, userHome.toString());
    }
  }

  @Test
  // provisioning does not exist before 10.6
  @ServerMinVersion("10.6")
  void scannerEngineJarPathSet_DoesNotDownloadFromServer() throws IOException {
    try (var userHome = new TempDirectory("junit-Engine-JarPathSet-")) {  // context.projectDir has a test name in it and that leads to too long path
      var context = createContext(userHome);
      var engineJarFolder = Path.of(ORCHESTRATOR.getServer().getHome().getAbsolutePath(), "lib", "scanner").toString(); // this must be a file that exists.
      try (Stream<Path> paths = Files.list(Paths.get(engineJarFolder))) {
        var scannerJarPath = paths
          .map(Path::toString)
          .findFirst()
          .orElseThrow();

        var result = context.begin
          .setProperty("sonar.scanner.engineJarPath", scannerJarPath)
          .execute(ORCHESTRATOR);

        TestUtils.matchesSingleLine(result.getLogs(), "EngineResolver: Resolving Scanner Engine path.");
        TestUtils.matchesSingleLine(result.getLogs(), String.format("Using local sonar engine provided by sonar.scanner.engineJarPath=%s", scannerJarPath.replace("\\", "\\\\")));
      }
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
