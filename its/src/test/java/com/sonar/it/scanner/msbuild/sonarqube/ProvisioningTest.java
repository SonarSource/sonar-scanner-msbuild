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
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.ScannerCommand;
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
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;

@ExtendWith({ServerTests.class, ContextExtension.class})
class ProvisioningTest {
  private static final String DIRECTORY_NAME = "JreProvisioning";

  @ParameterizedTest
  @ValueSource(booleans = {true, false})
  // provisioning does not exist before 10.6, and all newer versions support the scanner-engine download. We need to make sure the
  // combination of JRE cache miss with scanner-cli invocation and scanner-engine download both work as expected
  @ServerMinVersion("10.6")
  void cacheMiss_DownloadsCache(Boolean useSonarScannerCLI) {
    try (var userHome = new TempDirectory("junit-cache-miss-")) { // context.projectDir has a test name in it and that leads to too long path
      var context = createContext(userHome);
      context.begin.setProperty("sonar.scanner.useSonarScannerCLI", useSonarScannerCLI.toString()); // The downloaded JRE needs to be used by scanner-cli and scanner-engine
      context.build.useDotNet();
      // JAVA_HOME might not be set in the environment, so we set it to a non-existing path
      // so we can test that we updated it correctly
      var oldJavaHome = Optional.ofNullable(System.getenv("JAVA_HOME")).orElse(Paths.get("somewhere", "else").toString());
      context.end.setEnvironmentVariable("JAVA_HOME", oldJavaHome);
      // If this fails with "Error: could not find java.dll", the temp & JRE cache path is too long
      var result = context.runAnalysis();

      ProvisioningAssertions.cacheMissAssertions(result, ORCHESTRATOR.getServer().getUrl() + "/api/v2", userHome.toString(), oldJavaHome, false, useSonarScannerCLI);
    }
  }

  @Test
  // provisioning does not exist before 10.6
  @ServerMinVersion("10.6")
  void cacheHit_ReusesCachedFiles() {
    try (var userHome = new TempDirectory("junit-cache-hit-")) {  // context.projectDir has a test name in it and that leads to too long path
      var context = createContext(userHome);
      // first analysis, cache misses and downloads the JRE & scanner-engine
      var cacheMiss = context.begin.execute(ORCHESTRATOR);

      ProvisioningAssertions.assertCacheMissBeginStep(cacheMiss, ORCHESTRATOR.getServer().getUrl() + "/api/v2", userHome.toString(), false, false);

      // second analysis, cache hits and does not download the JRE or scanner-engine
      var cacheHit = context.begin.execute(ORCHESTRATOR);

      ProvisioningAssertions.cacheHitAssertions(cacheHit, userHome.toString());
    }
  }

  @Test
  // provisioning does not exist before 10.6
  @ServerMinVersion("10.6")
  void scannerEngineJarPathSet_DoesNotDownloadFromServer() throws IOException {
    try (var userHome = new TempDirectory("junit-Engine-JarPathSet-")) {  // context.projectDir has a test name in it and that leads to too long path
      var context = createContext(userHome);
      var engineJarFolder = Path.of(ORCHESTRATOR.getServer().getHome().getAbsolutePath(), "lib", "scanner"); // this must be a file that exists.
      try (Stream<Path> paths = Files.list(engineJarFolder)) {
        var scannerJarPath = paths
          .findFirst()
          .orElseThrow();

        var result = context.begin
          .setProperty("sonar.scanner.engineJarPath", scannerJarPath.toString())
          .execute(ORCHESTRATOR);

        assertThat(result.getLogs())
          .contains(
            "EngineResolver: Resolving Scanner Engine path.",
            String.format("Using local sonar engine provided by sonar.scanner.engineJarPath=%s", scannerJarPath))
          .doesNotContain(
            "EngineResolver: Cache miss.",
            "EngineResolver: Cache hit",
            "EngineResolver: Cache failure.");
      }
    }
  }

  @Test
  @ServerMinVersion("2025.6")
  void jreAutoProvisioning_disabled() {
    // sonar.jreAutoProvisioning.disabled is a server wide setting and errors with "Setting 'sonar.jreAutoProvisioning.disabled' cannot be set on a Project"
    // We need our own server instance here so we do not interfere with other JRE tests.
    var orchestrator = ServerTests.orchestratorBuilder()
      .setServerProperty("sonar.jreAutoProvisioning.disabled", "true")
      .build();
    orchestrator.start();
    try {
      var begin = ScannerCommand.createBeginStep(
          ScannerClassifier.NET,
          orchestrator.getDefaultAdminToken(),
          TestUtils.projectDir(ContextExtension.currentTempDir(), DIRECTORY_NAME),
          ContextExtension.currentTestName())
        .setDebugLogs()
        .setProperty("sonar.scanner.skipJreProvisioning", "false")
        .execute(orchestrator);
      assertThat(begin.getLogs())
        .contains("JreResolver: Resolving JRE path.")
        .contains("WARNING: JRE Metadata could not be retrieved from analysis/jres")
        .contains("JreResolver: Metadata could not be retrieved.")
        .as("An empty list of JREs is supposed to be invalid. Therefore a single retry is attempted.")
        .containsOnlyOnce("JreResolver: Resolving JRE path. Retrying...");
    } finally {
      orchestrator.stop();
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
