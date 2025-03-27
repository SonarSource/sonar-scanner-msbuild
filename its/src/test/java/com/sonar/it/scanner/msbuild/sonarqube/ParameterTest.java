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
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.EnvironmentVariable;
import com.sonar.it.scanner.msbuild.utils.QualityProfiles;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.ScannerCommand;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.locator.FileLocation;
import java.io.IOException;
import java.nio.file.Path;
import java.util.Collections;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.sonarqube.ws.Issues.Issue;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.junit.jupiter.api.Assertions.assertTrue;

@ExtendWith({ServerTests.class, ContextExtension.class})
class ParameterTest {
  final static Logger LOG = LoggerFactory.getLogger(ParameterTest.class);

  private static final String SONAR_RULES_PREFIX = "csharpsquid:";

  @TempDir
  public Path basePath;

  @Test
  void testExcludedAndTest_AnalyzeTestProject() throws Exception {
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    Path projectDir = TestUtils.projectDir(basePath, "ExcludedTest");
    ScannerCommand build = TestUtils.newScannerBegin(ORCHESTRATOR, "ExcludedTest_False", projectDir, token, ScannerClassifier.NET_FRAMEWORK)
      // don't exclude test projects
      .setProperty("sonar.dotnet.excludeTestProjects", "false");

    testExcludedAndTest(build, "ExcludedTest_False", projectDir, token, 1);
  }

  @Test
  void testExcludedAndTest_ExcludeTestProject() throws Exception {
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    Path projectDir = TestUtils.projectDir(basePath, "ExcludedTest");
    ScannerCommand build = TestUtils.newScannerBegin(ORCHESTRATOR, "ExcludedTest_True", projectDir, token, ScannerClassifier.NET_FRAMEWORK)
      // exclude test projects
      .setProperty("sonar.dotnet.excludeTestProjects", "true");

    testExcludedAndTest(build, "ExcludedTest_True", projectDir, token, 0);
  }

  @Test
  void testExcludedAndTest_simulateAzureDevopsEnvironmentSetting_ExcludeTestProject() throws Exception {
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    Path projectDir = TestUtils.projectDir(basePath, "ExcludedTest");
    EnvironmentVariable sonarQubeScannerParams = new EnvironmentVariable("SONARQUBE_SCANNER_PARAMS", "{\"sonar.dotnet.excludeTestProjects\":\"true\",\"sonar.verbose\":\"true\"}");
    ScannerCommand build = TestUtils.newScannerBegin(ORCHESTRATOR, "ExcludedTest_True_FromAzureDevOps", projectDir, token, ScannerClassifier.NET_FRAMEWORK);

    testExcludedAndTest(build, "ExcludedTest_True_FromAzureDevOps", projectDir, token, 0, Collections.singletonList(sonarQubeScannerParams));
  }

  @Test
  void testExcludedAndTest_simulateAzureDevopsEnvironmentSettingMalformedJson_LogsWarning() throws Exception {
    String projectKey = "ExcludedTest_MalformedJson_FromAzureDevOps";
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    Path projectDir = TestUtils.projectDir(basePath, "ExcludedTest");
    ORCHESTRATOR.getServer().provisionProject(projectKey, projectKey);
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", QualityProfiles.CS_S1134);

    ScannerCommand beginStep = TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK);
    beginStep.execute(ORCHESTRATOR);

    EnvironmentVariable sonarQubeScannerParams = new EnvironmentVariable("SONARQUBE_SCANNER_PARAMS", "{\"sonar.dotnet.excludeTestProjects\" }");
    BuildResult msBuildResult = TestUtils.runMSBuild(ORCHESTRATOR, projectDir, Collections.singletonList(sonarQubeScannerParams), 60 * 1000, "/t:Restore,Rebuild");

    assertThat(msBuildResult.isSuccess()).isTrue();
    assertThat(msBuildResult.getLogs()).contains("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS' because 'Invalid character after parsing " +
      "property name. Expected ':' but got: }. Path '', line 1, position 36.'.");
  }

  @Test
  void testScannerRespectsSonarQubeScannerParams() throws Exception {
    var projectKeyName = "testScannerRespectsSonarqubeScannerParams";
    var token = TestUtils.getNewToken(ORCHESTRATOR);
    var projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");

    var scannerParamsValue = Json.object()
      .add("sonar.buildString", "testValue")  // can be queried from the server via web_api/api/project_analyses/search
      .add("sonar.projectBaseDir", projectDir.toString())
      .toString();
    var sonarQubeScannerParams = new EnvironmentVariable("SONARQUBE_SCANNER_PARAMS", scannerParamsValue);

    var beginResult = TestUtils.newScannerBegin(ORCHESTRATOR, projectKeyName, projectDir, token, ScannerClassifier.NET_FRAMEWORK)
      .setProperty("sonar.projectBaseDir", null) // Undo default IT behavior: do NOT set sonar.projectBaseDir here, only from SONARQUBE_SCANNER_PARAMS.
      .setDebugLogs()
      .setEnvironmentVariable(sonarQubeScannerParams.name(), sonarQubeScannerParams.value())
      .execute(ORCHESTRATOR);
    assertThat(beginResult.isSuccess()).isTrue();

    TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

    var endResult = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKeyName, token, List.of(sonarQubeScannerParams));
    var endLogs = endResult.getLogs();
    assertThat(endResult.isSuccess()).isTrue();
    assertThat(endLogs).contains("Using user supplied project base directory: '" + projectDir);
    assertThat(endLogs).contains("sonar.buildString=testValue");
    assertThat(endLogs).contains("sonar.projectBaseDir=" + projectDir.toString().replace("\\", "\\\\"));

    var webApiResponse = ORCHESTRATOR.getServer()
      .newHttpCall("api/project_analyses/search")
      .setParam("project", projectKeyName)
      .execute();

    assertThat(webApiResponse.isSuccessful()).isTrue();

    var analyses = Json.parse(webApiResponse.getBodyAsString()).asObject().get("analyses").asArray();
    assertThat(analyses).hasSize(1);

    var firstAnalysis = analyses.get(0).asObject();
    assertThat(firstAnalysis.names()).contains("buildString");
    assertThat(firstAnalysis.get("buildString").asString()).isEqualTo("testValue");
  }

  @Test
  void testParameters() throws Exception {
    String projectKey = "testParameters";
    ORCHESTRATOR.getServer().provisionProject(projectKey, "parameters");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", QualityProfiles.CS_S107);

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token).execute(ORCHESTRATOR);
    TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);
    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);
    assertTrue(result.isSuccess());

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, projectKey);
    // 1 * csharpsquid:S1134 (line 34)
    assertThat(issues).hasSize(1);
    assertThat(issues.get(0).getMessage()).isEqualTo("Method has 3 parameters, which is greater than the 2 authorized.");
    assertThat(issues.get(0).getRule()).isEqualTo(SONAR_RULES_PREFIX + "S107");
  }

  @Test
  void testVerbose() throws IOException {
    String projectKey = "testVerbose";
    ORCHESTRATOR.getServer().provisionProject(projectKey, "verbose");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", QualityProfiles.CS_S1134);

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    BuildResult result = TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token)
      .setDebugLogs()
      .execute(ORCHESTRATOR);

    assertThat(result.getLogs()).contains("Downloading from http://");
    assertThat(result.getLogs()).contains("sonar.verbose=true was specified - setting the log verbosity to 'Debug'");
  }

  @Test
  void testHelp() throws IOException {
    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    BuildResult result = ScannerCommand.createHelpStep(ScannerClassifier.NET_FRAMEWORK, projectDir).execute(ORCHESTRATOR);

    assertTrue(result.isSuccess());
    assertThat(result.getLogs()).contains("Usage");
    assertThat(result.getLogs()).contains("SonarScanner.MSBuild.exe");
  }

  @Test
  void testAllProjectsExcluded() throws Exception {
    String projectKey = "testAllProjectsExcluded";
    ORCHESTRATOR.getServer().provisionProject(projectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", QualityProfiles.CS_S1134);

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK).execute(ORCHESTRATOR);
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Restore,Rebuild", "/p:ExcludeProjectsFromAnalysis=true");
    BuildResult result = TestUtils.newScannerEnd(ORCHESTRATOR, projectDir, token).execute(ORCHESTRATOR);

    assertThat(result.isSuccess()).isFalse();
    assertThat(result.getLogs()).contains("The exclude flag has been set so the project will not be analyzed.");
    assertThat(result.getLogs()).contains("No analysable projects were found. SonarQube analysis will not be performed. Check the build summary report for details.");
  }

  @Test
  void checkSourcesTestsIgnored() throws Exception {
    String projectName = "SourcesTestsIgnored";
    Path projectDir = TestUtils.projectDir(basePath, projectName);
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    TestUtils.newScannerBegin(ORCHESTRATOR, projectName, projectDir, token, ScannerClassifier.NET)
      .setProperty("sonar.sources", "Program.cs") // user-defined sources and tests are not passed to the cli.
      .setProperty("sonar.tests", "Program.cs")   // If they were passed, it results to double-indexing error.
      .execute(ORCHESTRATOR);
    TestUtils.runDotnetCommand(projectDir, "build", "--no-incremental");
    var result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectName, token);

    assertTrue(result.isSuccess());
    if (ORCHESTRATOR.getServer().version().isGreaterThan(9, 9)) {
      assertThat(TestUtils.projectIssues(ORCHESTRATOR, projectName)).hasSize(4);
    } else {
      assertThat(TestUtils.projectIssues(ORCHESTRATOR, projectName)).hasSize(3);
    }
  }

  private void testExcludedAndTest(ScannerCommand build, String projectKeyName, Path projectDir, String token, int expectedTestProjectIssues) {
    testExcludedAndTest(build, projectKeyName, projectDir, token, expectedTestProjectIssues, Collections.EMPTY_LIST);
  }

  private void testExcludedAndTest(ScannerCommand scanner, String projectKeyName, Path projectDir, String token, int expectedTestProjectIssues,
    List<EnvironmentVariable> environmentVariables) {
    String normalProjectKey = projectKeyName + ":Normal/Program.cs";
    String testProjectKey = projectKeyName + ":Test/UnitTest1.cs";

    ORCHESTRATOR.getServer().provisionProject(projectKeyName, projectKeyName);
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKeyName, "cs", QualityProfiles.CS_S1134_S2699);

    scanner.execute(ORCHESTRATOR);

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, environmentVariables, 60 * 1000, "/t:Restore,Rebuild");

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKeyName, token);

    assertTrue(result.isSuccess());

    // Dump debug info
    LOG.info("normalProjectKey = " + normalProjectKey);
    LOG.info("testProjectKey = " + testProjectKey);

    // One issue is in the normal project, one is in test project (when analyzed)
    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, projectKeyName);
    assertThat(issues).hasSize(1 + expectedTestProjectIssues);

    issues = TestUtils.projectIssues(ORCHESTRATOR, normalProjectKey);
    assertThat(issues).hasSize(1);

    issues = TestUtils.projectIssues(ORCHESTRATOR, testProjectKey);
    assertThat(issues).hasSize(expectedTestProjectIssues);

    // excluded project doesn't exist in SonarQube

    assertThat(TestUtils.getMeasureAsInteger(projectKeyName, "ncloc", ORCHESTRATOR)).isEqualTo(30);
    assertThat(TestUtils.getMeasureAsInteger(normalProjectKey, "ncloc", ORCHESTRATOR)).isEqualTo(30);
    assertThat(TestUtils.getMeasureAsInteger(testProjectKey, "ncloc", ORCHESTRATOR)).isNull();
  }
}
