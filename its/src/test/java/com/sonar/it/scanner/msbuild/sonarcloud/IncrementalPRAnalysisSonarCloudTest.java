/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
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
import com.sonar.it.scanner.msbuild.utils.VstsUtils;
import com.sonar.orchestrator.http.HttpException;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import com.sonar.orchestrator.util.StreamConsumer;
import org.junit.ClassRule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;
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

import static org.assertj.core.api.Assertions.assertThat;
import static org.awaitility.Awaitility.await;

public class IncrementalPRAnalysisSonarCloudTest {
  private final static Logger LOG = LoggerFactory.getLogger(IncrementalPRAnalysisSonarCloudTest.class);
  private final static Integer COMMAND_TIMEOUT = 2 * 60 * 1000;
  private final static String SCANNER_PATH = System.getenv("SCANNER_PATH") == null
    ? "../build/sonarscanner-msbuild-net46/SonarScanner.MSBuild.exe" // On the local machine, the scanner is prepared by ci-build.ps1 script.
    : System.getenv("SCANNER_PATH");
  private final static String[] prArguments = {
    "/d:sonar.pullrequest.base=master",
    "/d:sonar.pullrequest.branch=pull-request-branch",
    "/d:sonar.pullrequest.key=pull-request-key"
  };
  private final static String SONARCLOUD_ORGANIZATION = System.getenv("SONARCLOUD_ORGANIZATION");
  private final static String SONARCLOUD_PROJECT_KEY = System.getenv("SONARCLOUD_PROJECT_KEY");
  private final static String SONARCLOUD_URL = System.getenv("SONARCLOUD_URL");
  private final static String SONARCLOUD_PROJECT_TOKEN = System.getenv("SONARCLOUD_PROJECT_TOKEN");

  @ClassRule
  public static TemporaryFolder temp = TestUtils.createTempFolder();

  @Test
  public void incrementalPrAnalysis_master() throws IOException {
    var projectDir = TestUtils.projectDir(temp, "IncrementalPRAnalysis");
    var logWriter = new StringWriter();
    StreamConsumer.Pipe logsConsumer = new StreamConsumer.Pipe(logWriter);

    runBeginStep(projectDir, logsConsumer);

    assertThat(logWriter.toString()).contains(
      "Processing analysis cache",
      "Incremental PR analysis: Base branch parameter was not provided.",
      "Cache data is empty. A full analysis will be performed.");
  }

  @Test
  public void incrementalPrAnalysis_prWithoutChanges_producesUnchangedFilesWithAllFiles() throws IOException {
    var projectDir = TestUtils.projectDir(temp, "IncrementalPRAnalysis");
    var logWriter = new StringWriter();
    StreamConsumer.Pipe logsConsumer = new StreamConsumer.Pipe(logWriter);

    // Initial build - master
    runBeginStep(projectDir, logsConsumer);
    runBuild(projectDir, logsConsumer);
    runEndStep(projectDir, logsConsumer);
    var logs = logWriter.toString();
    assertThat(logs).doesNotContain("Incremental PR analysis: 3 files out of 3 are unchanged.");

    waitForTaskProcessing(logs);

    // Second build - PR
    runBeginStep(projectDir, logsConsumer, prArguments);
    runBuild(projectDir, logsConsumer);
    runEndStep(projectDir, logsConsumer);

    // Verify that the file hashes are considered and all of them will be skipped.
    assertThat(logWriter.toString()).contains("Incremental PR analysis: 3 files out of 3 are unchanged.");
    Path unchangedFilesPath = getUnchangedFilesPath(projectDir);
    assertThat(Files.readString(unchangedFilesPath)).contains("Unchanged1.cs", "Unchanged2.cs", "WithChanges.cs");
  }

  @Test
  public void incrementalPrAnalysis_pr_with_changes() throws IOException {
    var projectDir = TestUtils.projectDir(temp, "IncrementalPRAnalysis");
    var logWriter = new StringWriter();
    StreamConsumer.Pipe logsConsumer = new StreamConsumer.Pipe(logWriter);

    // Initial build - master.
    runBeginStep(projectDir, logsConsumer);
    runBuild(projectDir, logsConsumer);
    runEndStep(projectDir, logsConsumer);

    // Change a file to force analysis.
    File fileToBeChanged = projectDir.resolve("IncrementalPRAnalysis\\WithChanges.cs").toFile();
    BufferedWriter writer = new BufferedWriter(new FileWriter(fileToBeChanged, true));
    writer.append(' ');
    writer.close();

    waitForTaskProcessing(logWriter.toString());

    // Second build - PR.
    runBeginStep(projectDir, logsConsumer, prArguments);
    runBuild(projectDir, logsConsumer);
    runEndStep(projectDir, logsConsumer);

    // Assert that `WithChanges.cs` file is considered modified and will be analyzed.
    Path unchangedFilesPath = getUnchangedFilesPath(projectDir);
    LOG.info("UnchangedFiles: " + unchangedFilesPath.toAbsolutePath());
    assertThat(unchangedFilesPath).exists();
    assertThat(Files.readString(unchangedFilesPath))
      .contains("Unchanged1.cs")
      .contains("Unchanged2.cs")
      .doesNotContain("WithChanges.cs");
  }

  private static Path getUnchangedFilesPath(Path projectDir) {
    Path buildDirectory = VstsUtils.isRunningUnderVsts() ? Path.of(VstsUtils.getEnvBuildDirectory()) : projectDir;
    return buildDirectory.resolve(".sonarqube\\conf\\UnchangedFiles.txt").toAbsolutePath();
  }

  private static void runBeginStep(Path projectDir, StreamConsumer.Pipe logsConsumer, String... additionalArguments) {
    var beginCommand = Command.create(new File(SCANNER_PATH).getAbsolutePath())
      .setDirectory(projectDir.toFile())
      .addArgument("begin")
      .addArgument("/o:" + SONARCLOUD_ORGANIZATION)
      .addArgument("/k:" + SONARCLOUD_PROJECT_KEY)
      .addArgument("/d:sonar.host.url=" + SONARCLOUD_URL)
      .addArgument("/d:sonar.login=" + SONARCLOUD_PROJECT_TOKEN)
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
      .addArgument("/d:sonar.login=" + System.getenv("SONARCLOUD_PROJECT_TOKEN"));

    var endResult = CommandExecutor.create().execute(endCommand, logConsumer, COMMAND_TIMEOUT);
    assertThat(endResult).isZero();
  }

  private static void runBuild(Path projectDir, StreamConsumer.Pipe logConsumer) {
    var buildCommand = Command.create(getMSBuildPath()).addArgument("/t:restore,build").setDirectory(projectDir.toFile());
    int buildResult = CommandExecutor.create().execute(buildCommand, logConsumer, COMMAND_TIMEOUT);
    assertThat(buildResult).isZero();
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
            LOG.info("Pooling for task status using " + uri);
            var request = HttpRequest.newBuilder(uri).header("Authorization", "Bearer " + SONARCLOUD_PROJECT_TOKEN).build();
            var response = client.send(request, HttpResponse.BodyHandlers.ofString());
            return response.statusCode() == 200 && response.body().contains("\"status\":\"SUCCESS\"");
          }
          catch (HttpException ex) {
            return false;
          }
        });
    }
  }

  private static String getMSBuildPath(){
    var msBuildPath = System.getenv("MSBUILD_PATH");
    return msBuildPath == null
      ? TestUtils.MSBUILD_DEFAULT_PATH
      : msBuildPath;
  }
}
