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

import com.sonar.it.scanner.msbuild.utils.EnvironmentVariable;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import java.io.IOException;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.stream.Stream;
import org.jetbrains.annotations.NotNull;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.Arguments;
import org.junit.jupiter.params.provider.MethodSource;
import org.sonarqube.ws.Issues;

import static com.sonar.it.scanner.msbuild.sonarqube.Tests.ORCHESTRATOR;
import static com.sonar.it.scanner.msbuild.utils.AzureDevOpsUtils.getEnvBuildDirectory;
import static com.sonar.it.scanner.msbuild.utils.AzureDevOpsUtils.isRunningUnderAzureDevOps;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;
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
    TestUtils.reset(ORCHESTRATOR);
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
    assertThat(endStepResult.getLogs()).contains("Coverage Report Statistics: 2 files, 1 main files, 1 main files with coverage, 1 test files, 0 project excluded files, 0 other " +
      "language files.");
  }

  @Test
  void dotCover_CoverageDirectoryIsNotImported() throws Exception {
    var projectName = "DotCoverExcludedCoverage";
    var projectDir = TestUtils.projectDir(basePath, projectName);
    var token = TestUtils.getNewToken(ORCHESTRATOR);

    ORCHESTRATOR.getServer().provisionProject(projectName, projectName);
    var scanner = TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(projectName)
      .setProjectName(projectName)
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setProperty("sonar.verbose", "true")
      .setProperty("sonar.cs.dotcover.reportsPaths", "dotCover.Output.html")
      .setProjectVersion("1.0");
    var beginStepResult = ORCHESTRATOR.executeBuild(scanner);
    assertTrue(beginStepResult.isSuccess());

    TestUtils.runDotnetCommand(projectDir, "build", "--no-incremental");
    var endStepResult = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectName, token);
    assertTrue(endStepResult.isSuccess());
    assertThat(TestUtils.projectIssues(ORCHESTRATOR, projectName)).hasSize(1)
      .extracting(Issues.Issue::getRule)
      .containsOnly("csharpsquid:S2699");
  }

  private static Stream<Arguments> parameterizedArgumentsForExclusions() {
    return Stream.of(
      Arguments.of("coverage.xml", "", "", "", false),
      Arguments.of("coverage.xml", "", "**/Excluded.js", "", true),
      Arguments.of("coverage.xml", "", "", "**/Excluded.js", true),
      Arguments.of("coverage.xml", "", "**/Excluded.js", "**/Excluded.js", true),
      Arguments.of("", "", "", "", false),
      Arguments.of("", "", "**/Excluded.js", "", true),
      Arguments.of("", "", "", "**/Excluded.js", true),
      Arguments.of("", "", "**/Excluded.js", "**/Excluded.js", true),
      Arguments.of("", "coverage.xml", "", "", false),
      Arguments.of("", "coverage.xml", "**/Excluded.js", "", true),
      Arguments.of("", "coverage.xml", "", "**/Excluded.js", true),
      Arguments.of("", "coverage.xml", "**/Excluded.js", "**/Excluded.js", true),
      Arguments.of("localCoverage.xml", "serverCoverage.xml", "", "", false),
      Arguments.of("localCoverage.xml", "serverCoverage.xml", "**/Excluded.js", "", true),
      Arguments.of("localCoverage.xml", "serverCoverage.xml", "", "**/Excluded.js", true),
      Arguments.of("localCoverage.xml", "serverCoverage.xml", "**/Excluded.js", "**/Excluded.js", true)
    );
  }

  // Context: https://sonarsource.atlassian.net/browse/SCAN4NET-48
  @ParameterizedTest
  @MethodSource("parameterizedArgumentsForExclusions")
  void whenAddingCoverage_ExclusionsAreRespected(
    String localCoverageReportPath,
    String serverCoverageReportPath,
    String localExclusions,
    String serverExclusions,
    boolean isFileExcluded) throws Exception {
    var projectName = "ExclusionsAndCoverage";
    var projectKey = java.util.UUID.randomUUID().toString();
    var projectDir = TestUtils.projectDir(basePath, projectName);
    var token = TestUtils.getNewToken(ORCHESTRATOR);

    var server = ORCHESTRATOR.getServer();
    server.provisionProject(projectKey, projectName);

    var scanner = TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(projectKey)
      .setProjectName(projectName)
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setProperty("sonar.verbose", "true")
      .setProjectVersion("1.0");

    if (!localExclusions.isEmpty()) // You cannot provide an empty /d:sonar.exclusions="" argument
    {
      scanner.setProperty("sonar.exclusions", localExclusions);
    }
    if (!localCoverageReportPath.isEmpty()) {
      scanner.setProperty("sonar.cs.vscoveragexml.reportsPaths", localCoverageReportPath);
    }
    if (!serverExclusions.isEmpty()) {
      TestUtils.updateSetting(ORCHESTRATOR, projectKey, "sonar.exclusions", List.of(serverExclusions));
    }
    if (!serverCoverageReportPath.isEmpty()) {
      TestUtils.updateSetting(ORCHESTRATOR, projectKey, "sonar.cs.vscoveragexml.reportsPaths", List.of(serverCoverageReportPath));
    }

    var beginStepResult = ORCHESTRATOR.executeBuild(scanner);
    assertTrue(beginStepResult.isSuccess());

    TestUtils.runDotnetCommand(projectDir, "build", "--no-incremental");
    var endStepResult = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);
    assertTrue(endStepResult.isSuccess());

    if (isFileExcluded) {
      assertThat(TestUtils.projectIssues(ORCHESTRATOR, projectKey)).extracting(Issues.Issue::getRule, Issues.Issue::getComponent)
        .contains(tuple("csharpsquid:S1118", projectKey + ":ExclusionsAndCoverage/Calculator.cs"))
        .doesNotContain(tuple("javascript:S1529", projectKey + ":ExclusionsAndCoverage/Excluded.js"));
    } else {
      assertThat(TestUtils.projectIssues(ORCHESTRATOR, projectKey)).extracting(Issues.Issue::getRule, Issues.Issue::getComponent)
        .contains(tuple("csharpsquid:S1118", projectKey + ":ExclusionsAndCoverage/Calculator.cs"))
        .contains(tuple("javascript:S1529", projectKey + ":ExclusionsAndCoverage/Excluded.js"));
    }
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
