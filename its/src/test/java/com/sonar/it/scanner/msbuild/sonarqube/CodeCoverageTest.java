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

import com.sonar.it.scanner.msbuild.utils.*;

import java.util.List;
import java.util.stream.Stream;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledOnOs;
import org.junit.jupiter.api.condition.OS;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.Arguments;
import org.junit.jupiter.params.provider.MethodSource;
import org.sonarqube.ws.Issues;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static com.sonar.it.scanner.msbuild.utils.SonarAssertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;

@ExtendWith({ServerTests.class, ContextExtension.class})
class CodeCoverageTest {

  @Test
  void whenRunningOutsideAzureDevops_coverageIsNotImported() {
    try (var buildDirectory = new TempDirectory("junit-CodeCoverage.BuildDirectory.Local-")) {
      var logs = createContextWithCoverage(buildDirectory, ScannerClassifier.NET).runAnalysis().end().getLogs();

      if (ORCHESTRATOR.getServer().version().isGreaterThan(9, 9)) {
        assertThat(logs).contains(
          "'C# Tests Coverage Report Import' skipped because of missing configuration requirements.",
          "Accessed configuration:",
          "- sonar.cs.dotcover.reportsPaths: <empty>",
          "- sonar.cs.ncover3.reportsPaths: <empty>",
          "- sonar.cs.vscoveragexml.reportsPaths: <empty>",
          "- sonar.cs.opencover.reportsPaths: <empty>");
      } else {
        assertThat(logs).contains("C# Tests Coverage Report Import' skipped because one of the required properties is missing");
      }
    }
  }

  @Test
  @EnabledOnOs(OS.WINDOWS)
  void whenRunningOnAzureDevops_coverageIsImported() {
    // This test concerns only the .NET framework scanner flavor.
    // The coverage report needs to be converted from a binary format to xml, and this is supported only in Azure Devops on Windows.
    try (var buildDirectory = new TempDirectory("junit-CodeCoverage.BuildDirectory.Local-")) {  // Simulate different build directory on Azure DevOps
      var context = createContextWithCoverage(buildDirectory, ScannerClassifier.NET_FRAMEWORK);
      // Simulate Azure Devops: SonarQube.Integration.ImportBefore.targets determines paths based on these environment variables.
      var logs = context
        .setEnvironmentVariable(AzureDevOps.TF_BUILD, "true")             // Simulate Azure Devops CI environment
        .setEnvironmentVariable(AzureDevOps.BUILD_BUILDURI, "fake-uri")   //Must have value (can be anything)
        .setEnvironmentVariable(AzureDevOps.AGENT_BUILDDIRECTORY, buildDirectory.toString())   // The tests results should be present in a child "TestResults" folder.
        .setEnvironmentVariable(AzureDevOps.BUILD_SOURCESDIRECTORY, context.projectDir.toString())
        .runAnalysis()
        .end()
        .getLogs();

      assertThat(logs).contains("Converting coverage reports.");
      assertThat(logs).containsPattern("Converting coverage file '.*.coverage' to '.*.coveragexml'.");
      assertThat(logs).containsPattern("Parsing the Visual Studio coverage XML report .*coveragexml");
      assertThat(logs).contains("Coverage Report Statistics: 2 files, 1 main files, 1 main files with coverage, 1 test files, 0 project excluded files, 0 other language files.");
    }
  }

  @Test
  void dotCover_CoverageDirectoryIsNotImported() {
    var context = AnalysisContext.forServer("DotCoverExcludedCoverage");
    context.begin
      .setProperty("sonar.cs.dotcover.reportsPaths", "dotCover.Output.html")
      .setDebugLogs();
    context.build.useDotNet();
    context.runAnalysis();

    assertThat(TestUtils.projectIssues(ORCHESTRATOR, context.projectKey)).hasSize(1)
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
    boolean isFileExcluded) {
    var context = AnalysisContext.forServer("ExclusionsAndCoverage");
    context.begin.setDebugLogs();
    context.build.useDotNet().setTimeout(Timeout.TWO_MINUTES);
    context.end.setTimeout(Timeout.TWO_MINUTES);
    ORCHESTRATOR.getServer().provisionProject(context.projectKey, context.projectKey);

    if (!localExclusions.isEmpty()) // You cannot provide an empty /d:sonar.exclusions="" argument
    {
      context.begin.setProperty("sonar.exclusions", localExclusions);
    }
    if (!localCoverageReportPath.isEmpty()) {
      context.begin.setProperty("sonar.cs.vscoveragexml.reportsPaths", localCoverageReportPath);
    }
    if (!serverExclusions.isEmpty()) {
      TestUtils.updateSetting(ORCHESTRATOR, context.projectKey, "sonar.exclusions", List.of(serverExclusions));
    }
    if (!serverCoverageReportPath.isEmpty()) {
      TestUtils.updateSetting(ORCHESTRATOR, context.projectKey, "sonar.cs.vscoveragexml.reportsPaths", List.of(serverCoverageReportPath));
    }
    context.runAnalysis();

    if (isFileExcluded) {
      assertThat(TestUtils.projectIssues(ORCHESTRATOR, context.projectKey)).extracting(Issues.Issue::getRule, Issues.Issue::getComponent)
        .contains(tuple("csharpsquid:S1118", context.projectKey + ":ExclusionsAndCoverage/Calculator.cs"))
        .doesNotContain(tuple("javascript:S1529", context.projectKey + ":ExclusionsAndCoverage/Excluded.js"));
    } else {
      assertThat(TestUtils.projectIssues(ORCHESTRATOR, context.projectKey)).extracting(Issues.Issue::getRule, Issues.Issue::getComponent)
        .contains(tuple("csharpsquid:S1118", context.projectKey + ":ExclusionsAndCoverage/Calculator.cs"))
        .contains(tuple("javascript:S1529", context.projectKey + ":ExclusionsAndCoverage/Excluded.js"));
    }
  }

  private AnalysisContext createContextWithCoverage(TempDirectory buildDirectory, ScannerClassifier classifier) {
    var context = AnalysisContext.forServer("CodeCoverage", classifier);
    context.begin.setDebugLogs(); // For assertions
    // --collect "Code Coverage" parameter produces a binary coverage file ".coverage" that needs to be converted to an XML ".coveragexml" file by the end step
    context.build.useDotNet("test")
      .setTimeout(Timeout.TWO_MINUTES)
      .addArgument("--collect", "Code Coverage", "--logger", "trx", "--results-directory", buildDirectory.path.resolve("TestResults").toString());
    return context;
  }
}
