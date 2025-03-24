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

import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.http.HttpException;
import java.io.BufferedWriter;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.awaitility.Awaitility.await;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assumptions.assumeTrue;

@ExtendWith(ServerTests.class)
class IncrementalPRAnalysisTest {
  final static Logger LOG = LoggerFactory.getLogger(IncrementalPRAnalysisTest.class);

  @TempDir
  public Path basePath;

  @Test
  void incrementalPrAnalysis_NoCache() throws IOException {
    String projectKey = "incremental-pr-analysis-no-cache";
    Path projectDir = TestUtils.projectDir(basePath, "IncrementalPRAnalysis");
    File unexpectedUnchangedFiles = new File(projectDir.resolve(".sonarqube\\conf\\UnchangedFiles.txt").toString());
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    BuildResult result = TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token)
      .setDebugLogs(true) // To assert debug logs too
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
  void incrementalPrAnalysis_ProducesUnchangedFiles() throws IOException {
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(9, 9)); // Public cache API was introduced in 9.9

    String projectKey = "IncrementalPRAnalysis";
    String baseBranch = TestUtils.getDefaultBranchName(ORCHESTRATOR);
    Path projectDir = TestUtils.projectDir(basePath, projectKey);
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token).execute(ORCHESTRATOR);
    TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

    BuildResult firstAnalysisResult = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);
    assertTrue(firstAnalysisResult.isSuccess());

    waitForCacheInitialization(projectKey, baseBranch);

    File fileToBeChanged = projectDir.resolve("IncrementalPRAnalysis\\WithChanges.cs").toFile();
    BufferedWriter writer = new BufferedWriter(new FileWriter(fileToBeChanged, true));
    writer.append(' ');
    writer.close();

    BuildResult result = TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token)
      .setDebugLogs(true) // To assert debug logs too
      .setProperty("sonar.pullrequest.base", baseBranch)
      .execute(ORCHESTRATOR);

    assertTrue(result.isSuccess());
    assertThat(result.getLogs()).contains("Processing analysis cache");
    assertThat(result.getLogs()).contains("Downloading cache. Project key: IncrementalPRAnalysis, branch: " + baseBranch + ".");

    Path expectedUnchangedFiles = projectDir.resolve(".sonarqube\\conf\\UnchangedFiles.txt");
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
          ORCHESTRATOR.getServer().newHttpCall("api/analysis_cache/get").setParam("project", projectKey).setParam("branch", baseBranch).setAuthenticationToken(ORCHESTRATOR.getDefaultAdminToken()).execute();
          return true;
        } catch (HttpException ex) {
          return false; // if the `execute()` method is not successful it throws HttpException
        }
      });
  }
}
