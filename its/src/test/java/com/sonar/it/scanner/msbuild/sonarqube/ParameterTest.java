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

import com.eclipsesource.json.Json;
import com.sonar.it.scanner.msbuild.utils.AnalysisContext;
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.QualityProfile;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.ScannerCommand;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import java.nio.file.Path;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.sonarqube.ws.Issues.Issue;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

@ExtendWith({ServerTests.class, ContextExtension.class})
class ParameterTest {
  private static final String SONAR_RULES_PREFIX = "csharpsquid:";

  @Test
  void excludeTestProjects_AnalyzeTestProject() {
    var context = AnalysisContext.forServer("ExcludedTest");
    context.begin.setProperty("sonar.dotnet.excludeTestProjects", "false");   // don't exclude test projects
    validate(context, 1);
  }

  @Test
  void excludeTestProjects_ExcludeTestProject() {
    var context = AnalysisContext.forServer("ExcludedTest");
    context.begin.setProperty("sonar.dotnet.excludeTestProjects", "true");   // exclude test projects
    validate(context, 0);
  }

  @Test
  void excludeTestProjects_SimulateAzureDevopsEnvironmentSetting() {
    var context = AnalysisContext.forServer("ExcludedTest")
      .setEnvironmentVariable("SONARQUBE_SCANNER_PARAMS", "{\"sonar.dotnet.excludeTestProjects\":\"true\",\"sonar.verbose\":\"true\"}");
    validate(context, 0);
  }

  @Test
  void excludeTestProjects_SimulateAzureDevopsEnvironmentSettingMalformedJson_LogsWarning() {
    var context = AnalysisContext.forServer("ExcludedTest")
      .setEnvironmentVariable("SONARQUBE_SCANNER_PARAMS", "{\"sonar.dotnet.excludeTestProjects\" }")
      .setQualityProfile(QualityProfile.CS_S1134);
    var result = context.begin.execute(ORCHESTRATOR);

    assertFalse(result.isSuccess());
    assertThat(result.getLogs()).contains("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS' because " +
      "'Invalid character after parsing property name. Expected ':' but got: }. Path '', line 1, position 36.'.");
  }

  @Test
  void withSonarQubeScannerParams() {
    var context = AnalysisContext.forServer("ProjectUnderTest");
    context.setEnvironmentVariable("SONARQUBE_SCANNER_PARAMS", Json.object()
      .add("sonar.buildString", "testValue")  // can be queried from the server via web_api/api/project_analyses/search
      .add("sonar.projectBaseDir", context.projectDir.toString())
      .toString());
    context.begin
      .setProperty("sonar.projectBaseDir", null) // Undo default IT behavior: do NOT set sonar.projectBaseDir here, only from SONARQUBE_SCANNER_PARAMS.
      .setDebugLogs();
    var logs = context.runAnalysis().end().getLogs();

    assertThat(logs).contains("Using user supplied project base directory: '" + context.projectDir);
    assertThat(logs).contains("sonar.buildString=testValue");
    assertThat(logs).contains("sonar.projectBaseDir=" + context.projectDir.toString().replace("\\", "\\\\"));

    var webApiResponse = ORCHESTRATOR.getServer()
      .newHttpCall("api/project_analyses/search")
      .setParam("project", context.projectKey)
      .execute();

    assertThat(webApiResponse.isSuccessful()).isTrue();

    var analyses = Json.parse(webApiResponse.getBodyAsString()).asObject().get("analyses").asArray();
    assertThat(analyses).hasSize(1);

    var firstAnalysis = analyses.get(0).asObject();
    assertThat(firstAnalysis.names()).contains("buildString");
    assertThat(firstAnalysis.get("buildString").asString()).isEqualTo("testValue");
  }

  @Test
  void qualityProfile_HasParametrizedRule() {
    var context = AnalysisContext.forServer("ProjectUnderTest").setQualityProfile(QualityProfile.CS_S107);
    context.runAnalysis();

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    // 1 * csharpsquid:S1134 (line 34)
    assertThat(issues).hasSize(1);
    assertThat(issues.get(0).getMessage()).isEqualTo("Method has 3 parameters, which is greater than the 2 authorized.");
    assertThat(issues.get(0).getRule()).isEqualTo(SONAR_RULES_PREFIX + "S107");
  }

  @Test
  void verboseLog() {
    var context = AnalysisContext.forServer("ProjectUnderTest").setQualityProfile(QualityProfile.CS_S1134);
    var result = context.begin.setDebugLogs().execute(ORCHESTRATOR);

    assertThat(result.getLogs()).contains("Downloading from http://");
    assertThat(result.getLogs()).contains("sonar.verbose=true was specified - setting the log verbosity to 'Debug'");
  }

  @Test
  void helpMessage() {
    Path projectDir = TestUtils.projectDir(ContextExtension.currentTempDir(), "ProjectUnderTest");
    BuildResult result = ScannerCommand.createHelpStep(ScannerClassifier.NET_FRAMEWORK, projectDir).execute(ORCHESTRATOR);

    assertTrue(result.isSuccess());
    assertThat(result.getLogs()).contains("Usage");
    assertThat(result.getLogs()).contains("SonarScanner.MSBuild.exe");
  }

  @Test
  void allProjectsExcluded() {
    var context = AnalysisContext.forServer("ProjectUnderTest").setQualityProfile(QualityProfile.CS_S1134);
    context.build.addArgument("/p:ExcludeProjectsFromAnalysis=true");
    var logs = context.runFailedAnalysis().end().getLogs();

    assertThat(logs).contains("The exclude flag has been set so the project will not be analyzed.");
    assertThat(logs).contains("No analysable projects were found. SonarQube analysis will not be performed. Check the build summary report for details.");
  }

  @Test
  void sourcesAndTests_Ignored() {
    var context = AnalysisContext.forServer("SourcesTestsIgnored");
    context.begin
      .setProperty("sonar.sources", "Program.cs") // user-defined sources and tests are not passed to the cli.
      .setProperty("sonar.tests", "Program.cs");   // If they were passed, it results to double-indexing error.
    context.build.useDotNet();
    context.runAnalysis();

    if (ORCHESTRATOR.getServer().version().isGreaterThan(9, 9)) {
      assertThat(TestUtils.projectIssues(ORCHESTRATOR, context.projectKey)).hasSize(4);
    } else {
      assertThat(TestUtils.projectIssues(ORCHESTRATOR, context.projectKey)).hasSize(3);
    }
  }

  private void validate(AnalysisContext context, int expectedTestProjectIssues) {
    context.setQualityProfile(QualityProfile.CS_S1134_S2699);
    context.runAnalysis();

    String normalProjectKey = context.projectKey + ":Normal/Program.cs";
    String testProjectKey = context.projectKey + ":Test/UnitTest1.cs";

    // One issue is in the normal project, one is in test project (when analyzed)
    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    assertThat(issues).hasSize(1 + expectedTestProjectIssues);

    issues = TestUtils.projectIssues(ORCHESTRATOR, normalProjectKey);
    assertThat(issues).hasSize(1);

    issues = TestUtils.projectIssues(ORCHESTRATOR, testProjectKey);
    assertThat(issues).hasSize(expectedTestProjectIssues);

    // The Excludedtest/Excluded project doesn't exist in SonarQube and there's nothing to assert

    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "ncloc", ORCHESTRATOR)).isEqualTo(30);
    assertThat(TestUtils.getMeasureAsInteger(normalProjectKey, "ncloc", ORCHESTRATOR)).isEqualTo(30);
    assertThat(TestUtils.getMeasureAsInteger(testProjectKey, "ncloc", ORCHESTRATOR)).isNull();
  }
}
