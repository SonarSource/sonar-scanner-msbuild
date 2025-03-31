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
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.http.HttpException;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.StandardOpenOption;
import java.time.Duration;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.sonarqube.ws.client.analysiscache.GetRequest;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.awaitility.Awaitility.await;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assumptions.assumeTrue;

@ExtendWith({ServerTests.class, ContextExtension.class})
class IncrementalPRAnalysisTest {
  final static Logger LOG = LoggerFactory.getLogger(IncrementalPRAnalysisTest.class);

  @Test
  void noCache_DoesNotProduceUnchangedFiles() {
    var context = AnalysisContext.forServer("IncrementalPRAnalysis");
    var unexpectedUnchangedFiles = context.projectDir.resolve(".sonarqube\\conf\\UnchangedFiles.txt");
    var result = context.begin
      .setDebugLogs() // To assert debug logs too
      .setProperty("sonar.pullrequest.base", "base-branch")
      .execute(ORCHESTRATOR);

    assertTrue(result.isSuccess());
    assertThat(unexpectedUnchangedFiles).doesNotExist();
    assertThat(result.getLogs()).contains("Processing analysis cache");

    if (ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(9, 9)) {
      assertThat(result.getLogs()).contains("Cache data is empty. A full analysis will be performed.");
    } else {
      assertThat(result.getLogs()).contains("Incremental PR analysis is available starting with SonarQube 9.9 or later.");
    }
  }

  @Test
  void withCache_ProducesUnchangedFiles() throws IOException {
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(9, 9)); // Public cache API was introduced in 9.9

    var context = AnalysisContext.forServer("IncrementalPRAnalysis");
    String baseBranch = TestUtils.getDefaultBranchName(ORCHESTRATOR);
    context.runAnalysis();  // First analysis to populate the cache
    waitForCacheInitialization(context.projectKey, baseBranch);

    Files.writeString(context.projectDir.resolve("IncrementalPRAnalysis\\WithChanges.cs"), " // File modification", StandardOpenOption.APPEND);
    var result = context.begin
      .setDebugLogs() // To assert debug logs too
      .setProperty("sonar.pullrequest.base", baseBranch)
      .execute(ORCHESTRATOR);

    assertTrue(result.isSuccess());
    assertThat(result.getLogs()).contains("Processing analysis cache");
    assertThat(result.getLogs()).contains("Downloading cache. Project key: " + context.projectKey + ", branch: " + baseBranch + ".");

    var expectedUnchangedFiles = context.projectDir.resolve(".sonarqube\\conf\\UnchangedFiles.txt");
    LOG.info("UnchangedFiles: " + expectedUnchangedFiles.toAbsolutePath());
    assertThat(expectedUnchangedFiles).exists();
    assertThat(Files.readString(expectedUnchangedFiles))
      .contains("Unchanged1.cs")
      .contains("Unchanged2.cs")
      .doesNotContain("WithChanges.cs"); // Was modified
  }

  private void waitForCacheInitialization(String projectKey, String baseBranch) {
    await()
      .pollInterval(Duration.ofSeconds(1))
      .atMost(Duration.ofSeconds(120))
      .until(() -> {
        try {
          TestUtils.newWsClient(ORCHESTRATOR).analysisCache().get(new GetRequest().setProject(projectKey).setBranch(baseBranch)).close();
          return true;
        } catch (HttpException ex) {
          return false; // if the `analysisCache().get()` method is not successful it throws HttpException
        }
      });
  }
}
