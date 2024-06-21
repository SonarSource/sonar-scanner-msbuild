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
package com.sonar.it.scanner.msbuild.sonarqube;

import com.sonar.it.scanner.msbuild.utils.EnvironmentVariable;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.it.scanner.msbuild.utils.AzureDevOpsUtils;
import com.sonar.orchestrator.build.BuildResult;
import org.jetbrains.annotations.NotNull;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;

import java.io.IOException;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;

import static com.sonar.it.scanner.msbuild.sonarqube.Tests.ORCHESTRATOR;
import static com.sonar.it.scanner.msbuild.utils.AzureDevOpsUtils.getEnvBuildDirectory;
import static com.sonar.it.scanner.msbuild.utils.AzureDevOpsUtils.isRunningUnderAzureDevOps;
import static org.assertj.core.api.Assertions.assertThat;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assumptions.assumeFalse;

@ExtendWith(Tests.class)
class CodeCoverageTest {
  private static final String PROJECT_KEY = "code-coverage";
  private static final String PROJECT_NAME = "CodeCoverage";

  @TempDir
  public Path basePath;

  @BeforeEach
  public void setUp() {
    //TestUtils.reset(ORCHESTRATOR);
  }

  @Test
  void whenRunningOutsideAzureDevops_coverageIsNotImported() throws Exception {
    // When running in AzureDevOps some of the environment variables are set and cannot be overwritten.
    // Because of this, the coverage is always enabled.
    assumeFalse(isRunningUnderAzureDevOps());

    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);
    var token = TestUtils.getNewToken(ORCHESTRATOR);

    runBeginStep(projectDir, token, Collections.emptyList());
    runTestsWithCoverage(projectDir);

    var endStepResult = runEndStep(projectDir, token, Collections.emptyList());
    assertThat(endStepResult.getLogs()).contains("'C# Tests Coverage Report Import' skipped because one of the required properties is missing");
  }

  @Test
  void whenRunningOnAzureDevops_coverageIsImported() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);
    var token = TestUtils.getNewToken(ORCHESTRATOR);

    List<EnvironmentVariable> environmentVariables = isRunningUnderAzureDevOps()
      ? Collections.emptyList()
      : // In order to simulate Azure-DevOps environment, the following variables are needed:
        // TF_Build -> Must be set to true.
        // BUILD_BUILDURI -> Must have a value. The value can be anything.
        // AGENT_BUILDDIRECTORY -> The agent build directory; the tests results should be present in a child "TestResults" folder.
        Arrays.asList(
          new EnvironmentVariable("TF_Build", "true"),
          new EnvironmentVariable("BUILD_BUILDURI", "fake-uri"),
          new EnvironmentVariable("AGENT_BUILDDIRECTORY", projectDir.toString()));

    runBeginStep(projectDir, token, environmentVariables);
    runTestsWithCoverage(projectDir);

    var endStepResult = runEndStep(projectDir, token, environmentVariables);
    assertThat(endStepResult.getLogs()).contains("Coverage report conversion completed successfully.");
    assertThat(endStepResult.getLogs()).containsPattern("Converting coverage file '.*.coverage' to '.*.coveragexml'.");
    assertThat(endStepResult.getLogs()).containsPattern("Parsing the Visual Studio coverage XML report .*coveragexml");
    assertThat(endStepResult.getLogs()).contains("Coverage Report Statistics: 2 files, 1 main files, 1 main files with coverage, 1 test files, 0 project excluded files, 0 other language files.");
  }

  private static void runBeginStep(Path projectDir, String token, List<EnvironmentVariable> environmentVariables) {
      ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, PROJECT_NAME);
      var scanner = TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProjectName(PROJECT_NAME)
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProperty("sonar.verbose", "true")
        .setProjectVersion("1.0");
      for (var environmentVariable : environmentVariables) {
        scanner.setEnvironmentVariable(environmentVariable.getName(), environmentVariable.getValue());
      }
      var beginStepResult = ORCHESTRATOR.executeBuild(scanner);
      assertTrue(beginStepResult.isSuccess());
  }

  private static void runTestsWithCoverage(Path projectDir) {
    // On AzureDevops the build directory is already set, and it's different from the "projectDir"
    // In order to simulate the behavior, we need to generate the test results in the location specified by %AGENT_BUILDDIRECTORY%
    var buildDirectory = getEnvBuildDirectory() == null ? projectDir.toString() : getEnvBuildDirectory();
    // --collect "Code Coverage" parameter produces a binary coverage file ".coverage" that needs to be converted to an XML ".coveragexml" file by the end step
    var testResult = TestUtils.runDotnetCommand(projectDir, "test", "--collect", "Code Coverage", "--logger", "trx", "--results-directory", buildDirectory + "\\TestResults");
    assertTrue(testResult.isSuccess());
  }

  @NotNull
  private static BuildResult runEndStep(Path projectDir, String token, List<EnvironmentVariable> environmentVariables) {
    var endStepResult = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY, token, environmentVariables);
    assertTrue(endStepResult.isSuccess());
    return endStepResult;
  }
}
