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
import java.io.File;
import java.io.StringWriter;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.file.Path;
import java.time.Duration;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static org.assertj.core.api.Assertions.assertThat;
import static org.awaitility.Awaitility.await;

public class SonarCloudUtils {
  private final static Logger LOG = LoggerFactory.getLogger(SonarCloudUtils.class);

  public static String runAnalysis(Path projectDir, String projectKey, String... arguments) {
    var logWriter = new StringWriter();
    StreamConsumer.Pipe logsConsumer = new StreamConsumer.Pipe(logWriter);

    runBeginStep(projectDir, projectKey, logsConsumer, arguments);
    runBuild(projectDir);
    runEndStep(projectDir, logsConsumer);
    var logs = logWriter.toString();
    waitForTaskProcessing(logs);
    return logs;
  }

  public static void runBeginStep(Path projectDir, String projectKey, StreamConsumer.Pipe logsConsumer, String... additionalArguments) {
    var beginCommand = Command.create(new File(Constants.SCANNER_PATH).getAbsolutePath())
      .setDirectory(projectDir.toFile())
      .addArgument("begin")
      .addArgument("/o:" + Constants.SONARCLOUD_ORGANIZATION)
      .addArgument("/k:" + projectKey)
      .addArgument("/d:sonar.host.url=" + Constants.SONARCLOUD_URL)
      .addArgument("/d:sonar.scanner.sonarcloudUrl=" + Constants.SONARCLOUD_URL)
      .addArgument("/d:sonar.scanner.apiBaseUrl=" + Constants.SONARCLOUD_API_URL)
      .addArgument("/d:sonar.login=%SONARCLOUD_PROJECT_TOKEN%") // SonarCloud does not support yet sonar.token
      .addArgument("/d:sonar.projectBaseDir=" + projectDir.toAbsolutePath())
      .addArgument("/d:sonar.verbose=true");

    for (var argument : additionalArguments)
    {
      beginCommand.addArgument(argument);
    }

    LOG.info("Scanner path: " + Constants.SCANNER_PATH);
    LOG.info("Command line: " + beginCommand.toCommandLine());
    var beginResult = CommandExecutor.create().execute(beginCommand, logsConsumer, Constants.COMMAND_TIMEOUT);
    assertThat(beginResult).isZero();
  }

  private static void runEndStep(Path projectDir, StreamConsumer.Pipe logConsumer) {
    var endCommand = Command.create(new File(Constants.SCANNER_PATH).getAbsolutePath())
      .setDirectory(projectDir.toFile())
      .addArgument("end")
      .addArgument("/d:sonar.login=%SONARCLOUD_PROJECT_TOKEN%"); // SonarCloud does not support yet sonar.token

    var endResult = CommandExecutor.create().execute(endCommand, logConsumer, Constants.COMMAND_TIMEOUT);
    assertThat(endResult).isZero();
  }

  public static void runBuild(Path projectDir) {
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
}