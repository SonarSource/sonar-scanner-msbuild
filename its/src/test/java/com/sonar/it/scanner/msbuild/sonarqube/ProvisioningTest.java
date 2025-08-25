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
import java.nio.file.Paths;
import java.util.Optional;
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
  void cacheMiss_DownloadsJre() {
    try (var userHome = new TempDirectory("junit-JRE-miss-")) { // context.projectDir has a test name in it and that leads to too long path
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
  void cacheHit_ReusesJre() {
    // provisioning does not exist before 10.6
    try (var userHome = new TempDirectory("junit-JRE-hit-")) {  // context.projectDir has a test name in it and that leads to too long path
      var context = createContext(userHome);
      // first analysis, cache misses and downloads the JRE
      var firstBegin = context.begin.execute(ORCHESTRATOR);
      assertThat(firstBegin.isSuccess()).isTrue();
      assertThat(firstBegin.getLogs()).contains(
        "JreResolver: Cache miss",
        "Starting the file download.");
      assertThat(firstBegin.getLogs()).doesNotContain(
        "JreResolver: Cache hit",
        "JreResolver: Cache failure");

      // second analysis, cache hits and does not download the JRE
      var secondBegin = context.begin.execute(ORCHESTRATOR);

      ProvisioningAssertions.cacheHitAssertions(secondBegin, userHome.toString());
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
