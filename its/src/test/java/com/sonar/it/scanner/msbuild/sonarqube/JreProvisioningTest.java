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
import com.sonar.it.scanner.msbuild.utils.TempDirectory;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.junit.jupiter.api.Assumptions.assumeTrue;

@ExtendWith({ServerTests.class, ContextExtension.class})
public class JreProvisioningTest {
  private static final String DIRECTORY_NAME = "JreProvisioning";

  @Test
  void jreProvisioning_endToEnd_cacheMiss_downloadsJre() {
    // provisioning does not exist before 10.6
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(10, 6));
    try (var userHome = new TempDirectory("junit-JRE-miss-")) {
      var context = createContext(userHome);
      context.build.useDotNet();
      var result = context.runAnalysis();
      var beginLogs = result.begin().getLogs();
      var endLogs = result.end().getLogs();
      var root = userHome.toString().replace("\\", "\\\\");

      assertThat(beginLogs).contains(
        "JreResolver: Resolving JRE path.",
        "Downloading from " + ORCHESTRATOR.getServer().getUrl() + "/api/v2/analysis/jres?os=windows&arch=x64...",
        "Response received from " + ORCHESTRATOR.getServer().getUrl() + "/api/v2/analysis/jres?os=windows&arch=x64...",
        "JreResolver: Cache miss. Attempting to download JRE.",
        "Starting the Java Runtime Environment download.");
      TestUtils.matchesSingleLine(beginLogs, "Downloading Java JRE from analysis/jres/.+");
      TestUtils.matchesSingleLine(beginLogs, "The checksum of the downloaded file is '.+' and the expected checksum is '.+'");
      TestUtils.matchesSingleLine(beginLogs,
        "Starting extracting the Java runtime environment from archive '" + root + "\\\\cache.+' to folder '" + root +
          "\\\\cache.+'");
      TestUtils.matchesSingleLine(beginLogs,
        "Moving extracted Java runtime environment from '" + root + "\\\\cache.+' to '" + root + "\\\\cache" +
          ".+_extracted'");
      TestUtils.matchesSingleLine(beginLogs, "The Java runtime environment was successfully added to '" + root + "\\\\cache.+_extracted'");
      TestUtils.matchesSingleLine(beginLogs, "JreResolver: Download success. JRE can be found at '" + root + "\\\\cache.+_extracted.+java" +
        ".exe'");

      TestUtils.matchesSingleLine(endLogs, "Setting the JAVA_HOME for the scanner cli to " + root + "\\\\cache.+_extracted.+");
      TestUtils.matchesSingleLine(endLogs, "Overwriting the value of environment variable 'JAVA_HOME'. Old value: .+, new value: " + root +
        "\\\\cache.+extracted.+");
    }
  }

  @Test
  void jreProvisioning_endToEnd_cacheHit_reusesJre() {
    // provisioning does not exist before 10.6
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(10, 6));
    try (var userHome = new TempDirectory("junit-JRE-hit-")) {
      var context = createContext(userHome);
      // first analysis, cache misses and downloads the JRE
      var firstBegin = context.begin.execute(ORCHESTRATOR);
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
      TestUtils.matchesSingleLine(secondBegin.getLogs(),
        "JreResolver: Cache hit '" + userHome.toString().replace("\\", "\\\\") + "\\\\cache.+_extracted.+java.exe'");
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
