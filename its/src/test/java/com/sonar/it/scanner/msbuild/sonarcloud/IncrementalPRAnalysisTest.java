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

import com.sonar.it.scanner.msbuild.utils.AzureDevOpsUtils;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.util.StreamConsumer;
import java.io.BufferedWriter;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.io.StringWriter;
import java.nio.file.Files;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static com.sonar.it.scanner.msbuild.utils.AzureDevOpsUtils.isRunningUnderAzureDevOps;
import static org.assertj.core.api.Assertions.assertThat;

class IncrementalPRAnalysisTest {
  private final static Logger LOG = LoggerFactory.getLogger(IncrementalPRAnalysisTest.class);
  private final static String SONARCLOUD_PROJECT_KEY = "team-lang-dotnet_incremental-pr-analysis";
  private final static String PROJECT_NAME = "IncrementalPRAnalysis";
  private final static String[] prArguments = {
    "/d:sonar.pullrequest.base=master",
    "/d:sonar.pullrequest.branch=pull-request-branch",
    "/d:sonar.pullrequest.key=pull-request-key"
  };

  @TempDir
  public Path basePath;

  @Test
  void master_emptyCache() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);
    var logWriter = new StringWriter();
    StreamConsumer.Pipe logsConsumer = new StreamConsumer.Pipe(logWriter);

    SonarCloudUtils.runBeginStep(projectDir, SONARCLOUD_PROJECT_KEY, logsConsumer);

    assertThat(logWriter.toString()).contains(
      "Processing analysis cache",
      "Incremental PR analysis: Base branch parameter was not provided.",
      "Cache data is empty. A full analysis will be performed.");
  }

  @Test
  void prWithoutChanges_producesUnchangedFilesWithAllFiles() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);

    SonarCloudUtils.runAnalysis(projectDir, SONARCLOUD_PROJECT_KEY); // Initial build - master.
    var logs = SonarCloudUtils.runAnalysis(projectDir, SONARCLOUD_PROJECT_KEY, prArguments); // PR analysis.

    // Verify that the file hashes are considered and all of them will be skipped.
    // We do not know the total number of cache entries because other plugin can cache as well.
    TestUtils.matchesSingleLine(logs, "Incremental PR analysis: 3 files out of \\d+ are unchanged");
    Path unchangedFilesPath = getUnchangedFilesPath(projectDir);
    assertThat(Files.readString(unchangedFilesPath)).contains("Unchanged1.cs", "Unchanged2.cs", "WithChanges.cs");
  }

  @Test
  void prWithChanges_detectsUnchangedFile() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);

    SonarCloudUtils.runAnalysis(projectDir, SONARCLOUD_PROJECT_KEY); // Initial build - master.
    changeFile(projectDir, "IncrementalPRAnalysis\\WithChanges.cs"); // Change a file to force analysis.
    SonarCloudUtils.runAnalysis(projectDir, SONARCLOUD_PROJECT_KEY, prArguments); // PR analysis.

    assertOnlyWithChangesFileIsConsideredChanged(projectDir);
  }

  @Test
  void prWithChanges_basedOnDifferentBranchThanMaster_detectsUnchangedFiles() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);

    SonarCloudUtils.runAnalysis(projectDir, SONARCLOUD_PROJECT_KEY, "/d:sonar.branch.name=different-branch"); // Initial build - different branch.
    changeFile(projectDir, "IncrementalPRAnalysis\\WithChanges.cs"); // Change a file to force analysis.
    SonarCloudUtils.runAnalysis(projectDir, SONARCLOUD_PROJECT_KEY, "/d:sonar.pullrequest.base=different-branch", "/d:sonar.pullrequest.branch=second-pull-request-branch", "/d:sonar.pullrequest.key=second-pull-request-key");

    assertOnlyWithChangesFileIsConsideredChanged(projectDir);
  }

  private static void assertOnlyWithChangesFileIsConsideredChanged(Path projectDir) throws IOException {
    Path unchangedFilesPath = getUnchangedFilesPath(projectDir);
    LOG.info("UnchangedFiles: " + unchangedFilesPath.toAbsolutePath());
    assertThat(unchangedFilesPath).exists();
    assertThat(Files.readString(unchangedFilesPath))
      .contains("Unchanged1.cs")
      .contains("Unchanged2.cs")
      .doesNotContain("WithChanges.cs");
  }

  private static void changeFile(Path projectDir, String filePath) throws IOException {
    File fileToBeChanged = projectDir.resolve(filePath).toFile();
    BufferedWriter writer = new BufferedWriter(new FileWriter(fileToBeChanged, true));
    writer.append("\nclass Appended {  /* FIXME: S1134 in third file that will have changes on PR */ }");
    writer.close();
  }

  private static Path getUnchangedFilesPath(Path projectDir) {
    Path buildDirectory = isRunningUnderAzureDevOps() ? Path.of(AzureDevOpsUtils.buildSourcesDirectory()) : projectDir;
    return buildDirectory.resolve(".sonarqube\\conf\\UnchangedFiles.txt").toAbsolutePath();
  }
}
