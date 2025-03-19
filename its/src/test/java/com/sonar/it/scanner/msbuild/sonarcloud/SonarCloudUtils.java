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

import com.sonar.it.scanner.msbuild.utils.Property;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.ScannerCommand;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.http.HttpException;
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
import static org.junit.jupiter.api.Assertions.assertTrue;

public class SonarCloudUtils {
  private final static Logger LOG = LoggerFactory.getLogger(SonarCloudUtils.class);

  public static BuildResult runAnalysis(Path projectDir, String projectKey, Property... properties) {
    var beginResult = runBeginStep(projectDir, projectKey, properties);
    runBuild(projectDir);
    runEndStep(projectDir);
    return beginResult;
  }

  public static BuildResult runBeginStep(Path projectDir, String projectKey, Property... properties) {
    var beginCommand = ScannerCommand.createBeginStep(ScannerClassifier.NET_FRAMEWORK, "%SONARCLOUD_PROJECT_TOKEN%", projectDir, projectKey)
      .setOrganization(Constants.SONARCLOUD_ORGANIZATION)
      .setProperty("sonar.scanner.sonarcloudUrl", Constants.SONARCLOUD_URL)
      .setProperty("sonar.scanner.apiBaseUrl", Constants.SONARCLOUD_API_URL)
      .setDebugLogs(true);

    for (var property : properties) {
      beginCommand.setProperty(property.name(), property.value());
    }

    var result = beginCommand.execute(null);
    assertTrue(result.isSuccess());
    return result;
  }

  public static BuildResult runEndStep(Path projectDir) {
    var result = ScannerCommand.createEndStep(ScannerClassifier.NET_FRAMEWORK, "%SONARCLOUD_PROJECT_TOKEN%", projectDir).execute(null);
    assertTrue(result.isSuccess());
    waitForTaskProcessing(result.getLogs());
    return result;
  }

  public static void runBuild(Path projectDir) {
    var result = TestUtils.runDotnetCommand(projectDir, "build", "--no-incremental");
    assertThat(result.isSuccess()).isTrue();
  }

  private static void waitForTaskProcessing(String logs) {
    Pattern pattern = Pattern.compile("INFO: More about the report processing at (.*)");
    Matcher matcher = pattern.matcher(logs);
    if (matcher.find()) {
      var uri = URI.create(matcher.group(1));
      var client = HttpClient.newHttpClient();

      await()
        .pollInterval(Duration.ofSeconds(5))
        .atMost(Duration.ofSeconds(120))
        .until(() -> {
          try {
            LOG.info("Pooling for task status using {}", uri);
            var request = HttpRequest.newBuilder(uri).header("Authorization", "Bearer " + System.getenv("SONARCLOUD_PROJECT_TOKEN")).build();
            var response = client.send(request, HttpResponse.BodyHandlers.ofString());
            return response.statusCode() == 200 && response.body().contains("\"status\":\"SUCCESS\"");
          } catch (HttpException ex) {
            return false;
          }
        });
    }
  }
}
