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
import com.sonar.it.scanner.msbuild.utils.AzureDevOps;
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.stream.Stream;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.Arguments;
import org.junit.jupiter.params.provider.MethodSource;
import org.sonarqube.ws.Issues;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;
import static org.junit.jupiter.api.Assertions.assertTrue;

@ExtendWith({ServerTests.class, ContextExtension.class})
class CodeCoverageTest {
  @TempDir
  public Path basePath;

  @Test
  void whenRunningOutsideAzureDevops_coverageIsNotImported() throws Exception {
    var buildDirectory = basePath.resolve("CodeCoverage.BuildDirectory");
    Files.createDirectories(buildDirectory);
    var endLogs = createContextWithCoverage("whenRunningOutsideAzureDevops_coverageIsNotImported", buildDirectory).runAnalysis().end().getLogs();

    if (ORCHESTRATOR.getServer().version().isGreaterThan(9, 9)) {
      assertThat(endLogs).contains(
        "'C# Tests Coverage Report Import' skipped because of missing configuration requirements.",
        "Accessed configuration:",
        "- sonar.cs.dotcover.reportsPaths: <empty>",
        "- sonar.cs.ncover3.reportsPaths: <empty>",
        "- sonar.cs.vscoveragexml.reportsPaths: <empty>",
        "- sonar.cs.opencover.reportsPaths: <empty>");
    } else {
      assertThat(endLogs).contains("C# Tests Coverage Report Import' skipped because one of the required properties is missing");
    }
  }

  @Test
  void whenRunningOnAzureDevops_coverageIsImported() throws IOException {
    var buildDirectory = basePath.resolve("CodeCoverage.BuildDirectory");  // Simulate different build directory on Azure DevOps
    Files.createDirectories(buildDirectory);
    var context = createContextWithCoverage("whenRunningOnAzureDevops_coverageIsImported", buildDirectory);
    // Simulate Azure Devops: SonarQube.Integration.ImportBefore.targets determines paths based on these environment variables.
    var endLogs = context
      .setEnvironmentVariable(AzureDevOps.TF_BUILD, "true")
      .setEnvironmentVariable(AzureDevOps.BUILD_BUILDURI, "fake-uri")  //Must have value (can be anything)
      .setEnvironmentVariable(AzureDevOps.AGENT_BUILDDIRECTORY, buildDirectory.toString())   // The tests results should be present in a child "TestResults" folder.
      .setEnvironmentVariable(AzureDevOps.BUILD_SOURCESDIRECTORY, context.projectDir.toString())
      .runAnalysis()
      .end()
      .getLogs();

    assertThat(endLogs).contains("Coverage report conversion completed successfully.");
    assertThat(endLogs).containsPattern("Converting coverage file '.*.coverage' to '.*.coveragexml'.");
    assertThat(endLogs).containsPattern("Parsing the Visual Studio coverage XML report .*coveragexml");
    assertThat(endLogs).contains("Coverage Report Statistics: 2 files, 1 main files, 1 main files with coverage, 1 test files, 0 project excluded files, 0 other language files.");
  }

  @Test
  void dotCover_CoverageDirectoryIsNotImported() throws Exception {
    var projectName = "DotCoverExcludedCoverage";
    var projectDir = TestUtils.projectDir(basePath, projectName);
    var token = TestUtils.getNewToken(ORCHESTRATOR);

    ORCHESTRATOR.getServer().provisionProject(projectName, projectName);
    var scanner = TestUtils.newScannerBegin(ORCHESTRATOR, projectName, projectDir, token)
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setProperty("sonar.verbose", "true")
      .setProperty("sonar.cs.dotcover.reportsPaths", "dotCover.Output.html");
    var beginStepResult = scanner.execute(ORCHESTRATOR);
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
    var scanner = TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token).setDebugLogs();

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

    var beginStepResult = scanner.execute(ORCHESTRATOR);
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

  private AnalysisContext createContextWithCoverage(String projectKey, Path buildDirectory) {
    var context = AnalysisContext.forServer("CodeCoverage");
    context.begin.setDebugLogs(); // For assertions
    // --collect "Code Coverage" parameter produces a binary coverage file ".coverage" that needs to be converted to an XML ".coveragexml" file by the end step
    context.build.useDotNet("test").addArgument("--collect", "Code Coverage", "--logger", "trx", "--results-directory", buildDirectory + "\\TestResults");
    return context;
  }
}
