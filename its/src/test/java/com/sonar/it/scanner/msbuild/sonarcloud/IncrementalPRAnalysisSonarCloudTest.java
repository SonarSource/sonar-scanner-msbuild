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
import com.sonar.orchestrator.http.HttpException;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import com.sonar.orchestrator.util.StreamConsumer;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.BufferedWriter;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.io.StringWriter;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import static com.sonar.it.scanner.msbuild.utils.AzureDevOpsUtils.getEnvBuildDirectory;
import static com.sonar.it.scanner.msbuild.utils.AzureDevOpsUtils.isRunningUnderAzureDevOps;
import static org.assertj.core.api.Assertions.assertThat;
import static org.awaitility.Awaitility.await;

class IncrementalPRAnalysisSonarCloudTest {
  private final static Logger LOG = LoggerFactory.getLogger(IncrementalPRAnalysisSonarCloudTest.class);
  private final static Integer COMMAND_TIMEOUT = 2 * 60 * 1000;
  private final static String SCANNER_PATH = "../build/sonarscanner-net-framework/SonarScanner.MSBuild.exe";
  private final static String[] prArguments = {
    "/d:sonar.pullrequest.base=master",
    "/d:sonar.pullrequest.branch=pull-request-branch",
    "/d:sonar.pullrequest.key=pull-request-key"
  };
  private final static String SONARCLOUD_ORGANIZATION = System.getenv("SONARCLOUD_ORGANIZATION");
  private final static String SONARCLOUD_PROJECT_KEY = System.getenv("SONARCLOUD_PROJECT_KEY");
  private final static String SONARCLOUD_URL = System.getenv("SONARCLOUD_URL");
  private final static String SONARCLOUD_API_URL = System.getenv("SONARCLOUD_API_URL");

  @TempDir
  public Path basePath;

  @Test
  void master_emptyCache() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, "IncrementalPRAnalysis");
    var logWriter = new StringWriter();
    StreamConsumer.Pipe logsConsumer = new StreamConsumer.Pipe(logWriter);

    runBeginStep(projectDir, logsConsumer);

    assertThat(logWriter.toString()).contains(
      "Processing analysis cache",
      "Incremental PR analysis: Base branch parameter was not provided.",
      "Cache data is empty. A full analysis will be performed.");
  }

  @Test
  void prWithoutChanges_producesUnchangedFilesWithAllFiles() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, "IncrementalPRAnalysis");

    runAnalysis(projectDir); // Initial build - master.
    var logs = runAnalysis(projectDir, prArguments); // PR analysis.

    // Verify that the file hashes are considered and all of them will be skipped.
    // The (?s) flag indicates that the dot special character ( . ) should additionally match the following
    // line terminator ("newline") characters in a string, which it would not match otherwise.
    // We do not know the total number of cache entries because other plugin can cache as well.
    assertThat(logs).matches("(?s).*Incremental PR analysis: 3 files out of \\d+ are unchanged.*");
    Path unchangedFilesPath = getUnchangedFilesPath(projectDir);
    assertThat(Files.readString(unchangedFilesPath)).contains("Unchanged1.cs", "Unchanged2.cs", "WithChanges.cs");
  }

  @Test
  void prWithChanges_detectsUnchangedFile() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, "IncrementalPRAnalysis");

    runAnalysis(projectDir); // Initial build - master.
    changeFile(projectDir, "IncrementalPRAnalysis\\WithChanges.cs"); // Change a file to force analysis.
    runAnalysis(projectDir, prArguments); // PR analysis.

    assertOnlyWithChangesFileIsConsideredChanged(projectDir);
  }

  @Test
  void prWithChanges_basedOnDifferentBranchThanMaster_detectsUnchangedFiles() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, "IncrementalPRAnalysis");

    runAnalysis(projectDir, "/d:sonar.branch.name=different-branch"); // Initial build - different branch.
    changeFile(projectDir, "IncrementalPRAnalysis\\WithChanges.cs"); // Change a file to force analysis.
    runAnalysis(projectDir, "/d:sonar.pullrequest.base=different-branch", "/d:sonar.pullrequest.branch=second-pull-request-branch", "/d:sonar.pullrequest.key=second-pull-request-key");

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

  private static String runAnalysis(Path projectDir, String... arguments) {
    var logWriter = new StringWriter();
    StreamConsumer.Pipe logsConsumer = new StreamConsumer.Pipe(logWriter);

    runBeginStep(projectDir, logsConsumer, arguments);
    runBuild(projectDir);
    runEndStep(projectDir, logsConsumer);
    var logs = logWriter.toString();
    waitForTaskProcessing(logs);
    return logs;
  }

  private static Path getUnchangedFilesPath(Path projectDir) {
    Path buildDirectory = isRunningUnderAzureDevOps() ? Path.of(getEnvBuildDirectory()) : projectDir;
    return buildDirectory.resolve(".sonarqube\\conf\\UnchangedFiles.txt").toAbsolutePath();
  }

  private static void runBeginStep(Path projectDir, StreamConsumer.Pipe logsConsumer, String... additionalArguments) {
    var beginCommand = Command.create(new File(SCANNER_PATH).getAbsolutePath())
      .setDirectory(projectDir.toFile())
      .addArgument("begin")
      .addArgument("/o:" + SONARCLOUD_ORGANIZATION)
      .addArgument("/k:" + SONARCLOUD_PROJECT_KEY)
      .addArgument("/d:sonar.host.url=" + SONARCLOUD_URL)
      .addArgument("/d:sonar.scanner.sonarcloudUrl=" + SONARCLOUD_URL)
      .addArgument("/d:sonar.scanner.apiBaseUrl=" + SONARCLOUD_API_URL)
      .addArgument("/d:sonar.login=%SONARCLOUD_PROJECT_TOKEN%") // SonarCloud does not support yet sonar.token
      .addArgument("/d:sonar.projectBaseDir=" + projectDir.toAbsolutePath())
      .addArgument("/d:sonar.verbose=true");

    for (var argument : additionalArguments)
    {
      beginCommand.addArgument(argument);
    }

    LOG.info("Scanner path: " + SCANNER_PATH);
    LOG.info("Command line: " + beginCommand.toCommandLine());
    var beginResult = CommandExecutor.create().execute(beginCommand, logsConsumer, COMMAND_TIMEOUT);
    assertThat(beginResult).isZero();
  }

  private static void runEndStep(Path projectDir, StreamConsumer.Pipe logConsumer) {
    var endCommand = Command.create(new File(SCANNER_PATH).getAbsolutePath())
      .setDirectory(projectDir.toFile())
      .addArgument("end")
      .addArgument("/d:sonar.login=%SONARCLOUD_PROJECT_TOKEN%"); // SonarCloud does not support yet sonar.token

    var endResult = CommandExecutor.create().execute(endCommand, logConsumer, COMMAND_TIMEOUT);
    assertThat(endResult).isZero();
  }

  private static void runBuild(Path projectDir) {
    var result = TestUtils.runDotnetCommand(projectDir, "build", "--no-incremental");
    assertThat(result.isSuccess()).isTrue();
  }

  private static void waitForTaskProcessing(String logs) {
    Pattern pattern = Pattern.compile("INFO: More about the report processing at (.*)");
    Matcher matcher = pattern.matcher(logs);
    if (matcher.find())
    {
      var uri = URI.create(matcher.group(1));
      var client = HttpClient.newHttpClient();

      await()
        .pollInterval(Duration.ofSeconds(5))
        .atMost(Duration.ofSeconds(120))
        .until(() -> {
          try
          {
            LOG.info("Pooling for task status using {}", uri);
            var request = HttpRequest.newBuilder(uri).header("Authorization", "Bearer " + System.getenv("SONARCLOUD_PROJECT_TOKEN")).build();
            var response = client.send(request, HttpResponse.BodyHandlers.ofString());
            return response.statusCode() == 200 && response.body().contains("\"status\":\"SUCCESS\"");
          }
          catch (HttpException ex) {
            return false;
          }
        });
    }
  }

  private static String getMSBuildPath() {
    var msBuildPath = System.getenv("MSBUILD_PATH");
    return msBuildPath == null
      ? TestUtils.MSBUILD_DEFAULT_PATH
      : msBuildPath;
  }
}
