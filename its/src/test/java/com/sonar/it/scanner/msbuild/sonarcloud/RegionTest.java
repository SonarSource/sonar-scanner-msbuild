package com.sonar.it.scanner.msbuild.sonarcloud;

import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import com.sonar.orchestrator.util.StreamConsumer;
import java.io.File;
import java.io.IOException;
import java.io.StringWriter;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static org.assertj.core.api.Assertions.assertThat;

public class RegionTest {
  private static final Logger LOG = LoggerFactory.getLogger(RegionTest.class);
  private static final String SONARCLOUD_PROJECT_KEY = "team-lang-dotnet_region-parameter";
  private static final String PROJECT_NAME = "ProjectUnderTest";

  @TempDir
  public Path basePath;

  @Test
  void region_us() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);
    var logWriter = new StringWriter();
    StreamConsumer.Pipe logsConsumer = new StreamConsumer.Pipe(logWriter);

    var beginCommand = Command.create(new File(Constants.SCANNER_PATH).getAbsolutePath())
      .setDirectory(projectDir.toFile())
      .addArgument("begin")
      .addArgument("/o:" + Constants.SONARCLOUD_ORGANIZATION)
      .addArgument("/k:" + SONARCLOUD_PROJECT_KEY)
      .addArgument("/d:sonar.region=us")
      .addArgument("/d:sonar.verbose=true");

    var beginResult = CommandExecutor.create().execute(beginCommand, logsConsumer, Constants.COMMAND_TIMEOUT);
    assertThat(beginResult).isOne(); // Indicates error
    assertThat(logWriter.toString()).containsSubsequence(
      "Server Url: https://sonarqube.us",
      "Api Url: https://api.sonarqube.us",
      "Is SonarCloud: True",
      "Downloading from https://sonarqube.us/api/settings/values?component=unknown",
      // /analysis/version returns a redirect at the moment which indicates that SQC-USA is not yet general available
      "Downloading from https://api.sonarqube.us/analysis/version",
      "Downloading from https://www.sonarsource.com/products/sonarqube/api/server/version failed. Http status code is NotFound.",
      "Pre-processing failed. Exit code: 1");
  }
}
