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
import com.sonar.it.scanner.msbuild.utils.EnvironmentVariable;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.ScannerCommand;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.http.HttpException;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import java.io.BufferedWriter;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.LinkOption;
import java.nio.file.Path;
import java.time.Duration;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.function.Function;
import java.util.stream.Collectors;
import org.apache.commons.io.FileUtils;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.sonarqube.ws.Ce;
import org.sonarqube.ws.Components;
import org.sonarqube.ws.Issues;
import org.sonarqube.ws.Issues.Issue;
import org.sonarqube.ws.client.WsClient;
import org.sonarqube.ws.client.components.ShowRequest;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;
import static org.awaitility.Awaitility.await;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assumptions.assumeFalse;
import static org.junit.jupiter.api.Assumptions.assumeTrue;

@ExtendWith(ServerTests.class)
class ScannerMSBuildTest {
  final static Logger LOG = LoggerFactory.getLogger(ScannerMSBuildTest.class);

  private static final String SONAR_RULES_PREFIX = "csharpsquid:";

  @TempDir
  public Path basePath;

  @Test
  void testSample() throws Exception {
    String projectKey = "testSample";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKey, projectKey);
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", "ProfileForTest");

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token).execute(ORCHESTRATOR);
    TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);
    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);

    assertTrue(result.isSuccess());
    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, projectKey);
    // 1 * csharpsquid:S1134 (line 34)
    assertThat(issues).hasSize(1);
    assertThat(TestUtils.getMeasureAsInteger(projectKey, "ncloc", ORCHESTRATOR)).isEqualTo(25);
    assertThat(TestUtils.getMeasureAsInteger(projectKey + ":ProjectUnderTest/Foo.cs", "ncloc", ORCHESTRATOR)).isEqualTo(25);
    assertThat(TestUtils.getMeasureAsInteger(projectKey + ":ProjectUnderTest/Foo.cs", "lines", ORCHESTRATOR)).isEqualTo(52);
  }

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
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKey, projectKey);
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", "ProfileForTest");

    ScannerCommand beginStep = TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK);
    beginStep.execute(ORCHESTRATOR);

    EnvironmentVariable sonarQubeScannerParams = new EnvironmentVariable("SONARQUBE_SCANNER_PARAMS", "{\"sonar.dotnet.excludeTestProjects\" }");
    BuildResult msBuildResult = TestUtils.runMSBuild(ORCHESTRATOR, projectDir, Collections.singletonList(sonarQubeScannerParams), 60 * 1000, "/t:Restore,Rebuild");

    assertThat(msBuildResult.isSuccess()).isTrue();
    assertThat(msBuildResult.getLogs()).contains("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS' because 'Invalid character after parsing " +
      "property name. Expected ':' but got: }. Path '', line 1, position 36.'.");
  }

  @Test
  void testScannerRespectsSonarqubeScannerParams() throws Exception {
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
      .setDebugLogs(true)
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
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfileParameters.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKey, "parameters");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", "ProfileForTestParameters");

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
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKey, "verbose");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    BuildResult result = TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token)
      .setDebugLogs(true)
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
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", "ProfileForTest");

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
  void testNoActiveRule() throws IOException {
    String projectKey = "testNoActiveRule";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestEmptyQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKey, "empty");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", "EmptyProfileForTest");

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK).execute(ORCHESTRATOR);
    TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);
    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);

    assertThat(result.isSuccess()).isTrue();

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, projectKey);
    assertThat(issues).isEmpty();
  }

  @Test
  void excludeAssemblyAttribute() throws Exception {
    String projectKey = "excludeAssemblyAttribute";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(basePath, "AssemblyAttribute");
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK).execute(ORCHESTRATOR);
    TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);
    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);

    assertThat(result.getLogs()).doesNotContain("File is not under the project directory and cannot currently be analysed by SonarQube");
    assertThat(result.getLogs()).doesNotContain("AssemblyAttributes.cs");
  }

  @Test
  void testXamlCompilation() throws IOException {
    // We can't build with MSBuild 15
    // error MSB4018: System.InvalidOperationException: This implementation is not part of the Windows Platform FIPS validated cryptographic algorithms.
    // at System.Security.Cryptography.MD5CryptoServiceProvider..ctor()
    assumeFalse(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017"));

    var projectKey = "XamarinApplication";
    BuildResult result = runAnalysis(projectKey, true);
    assertTrue(result.isSuccess());

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, projectKey);
    assertThat(issues)
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(
        tuple(SONAR_RULES_PREFIX + "S927", "XamarinApplication:XamarinApplication.iOS/AppDelegate.cs"),
        tuple(SONAR_RULES_PREFIX + "S927", "XamarinApplication:XamarinApplication.iOS/AppDelegate.cs"),
        tuple(SONAR_RULES_PREFIX + "S1118", "XamarinApplication:XamarinApplication.iOS/Main.cs"),
        tuple(SONAR_RULES_PREFIX + "S1186", "XamarinApplication:XamarinApplication.iOS/Main.cs"),
        tuple(SONAR_RULES_PREFIX + "S1186", "XamarinApplication:XamarinApplication/App.xaml.cs"),
        tuple(SONAR_RULES_PREFIX + "S1186", "XamarinApplication:XamarinApplication/App.xaml.cs"),
        tuple(SONAR_RULES_PREFIX + "S1186", "XamarinApplication:XamarinApplication/App.xaml.cs"),
        tuple(SONAR_RULES_PREFIX + "S1134", "XamarinApplication:XamarinApplication/MainPage.xaml.cs"),
        tuple("external_roslyn:CS0618", "XamarinApplication:XamarinApplication.iOS/Main.cs"));

    assertThat(TestUtils.getMeasureAsInteger("XamarinApplication", "lines", ORCHESTRATOR)).isEqualTo(149);
    assertThat(TestUtils.getMeasureAsInteger("XamarinApplication", "ncloc", ORCHESTRATOR)).isEqualTo(93);
    assertThat(TestUtils.getMeasureAsInteger("XamarinApplication", "files", ORCHESTRATOR)).isEqualTo(6);
    assertThat(TestUtils.getMeasureAsInteger("XamarinApplication:XamarinApplication.iOS", "lines", ORCHESTRATOR)).isEqualTo(97);
    assertThat(TestUtils.getMeasureAsInteger("XamarinApplication:XamarinApplication", "lines", ORCHESTRATOR)).isEqualTo(52);
  }

  @Test
  void testRazorCompilationNet9WithoutSourceGenerators() throws IOException {
    assumeTrue(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022")); // We can't build without MsBuild17
    String projectName = "RazorWebApplication.net9.withoutSourceGenerators";
    assertProjectFileContains(projectName, "<UseRazorSourceGenerator>false</UseRazorSourceGenerator>");
    validateRazorProject(projectName);
  }

  @Test
  void testRazorCompilationNet9WithSourceGenerators() throws IOException {
    assumeTrue(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022")); // We can't build without MsBuild17
    String projectName = "RazorWebApplication.net9.withSourceGenerators";
    assertProjectFileContains(projectName, "<UseRazorSourceGenerator>true</UseRazorSourceGenerator>");
    validateRazorProject(projectName);
  }

  @Test
  void testCSharpAllFlat() throws IOException {
    runAnalysis("CSharpAllFlat");

    assertThat(getComponent("CSharpAllFlat:Common.cs")).isNotNull();
  }

  @Test
  void testTargetUninstall() throws IOException {
    var projectKey = "testTargetUninstall";
    Path projectDir = TestUtils.projectDir(basePath, "CSharpAllFlat");
    TestUtils.runAnalysis(projectDir, projectKey, false);
    // Run the build for a second time - should not fail after uninstalling targets
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild", "CSharpAllFlat.sln");

    assertThat(getComponent(projectKey + ":Common.cs")).isNotNull();
  }

  @Test
  void testCSharpSharedFiles() throws IOException {
    runAnalysis("CSharpSharedFiles");

    assertThat(getComponent("CSharpSharedFiles:Common.cs"))
      .isNotNull();
    String class1ComponentId = TestUtils.hasModules(ORCHESTRATOR)
      ? "CSharpSharedFiles:CSharpSharedFiles:D8FEDBA2-D056-42FB-B146-5A409727B65D:Class1.cs"
      : "CSharpSharedFiles:ClassLib1/Class1.cs";
    assertThat(getComponent(class1ComponentId))
      .isNotNull();
    String class2ComponentId = TestUtils.hasModules(ORCHESTRATOR)
      ? "CSharpSharedFiles:CSharpSharedFiles:72CD6ED2-481A-4828-BA15-8CD5F0472A77:Class2.cs"
      : "CSharpSharedFiles:ClassLib2/Class2.cs";
    assertThat(getComponent(class2ComponentId))
      .isNotNull();
  }

  @Test
  void testCSharpSharedProjectType() throws IOException {
    runAnalysis("CSharpSharedProjectType");

    assertThat(getComponent("CSharpSharedProjectType:SharedProject/TestEventInvoke.cs"))
      .isNotNull();
    String programComponentId1 = TestUtils.hasModules(ORCHESTRATOR)
      ? "CSharpSharedProjectType:CSharpSharedProjectType:36F96F66-8136-46C0-B83B-EFAE05A8FFC1:Program.cs"
      : "CSharpSharedProjectType:ConsoleApp1/Program.cs";
    assertThat(getComponent(programComponentId1))
      .isNotNull();
    String programComponentId2 = TestUtils.hasModules(ORCHESTRATOR)
      ? "CSharpSharedProjectType:CSharpSharedProjectType:F96D8AA1-BCE1-4655-8D65-08F2A5FAC15B:Program.cs"
      : "CSharpSharedProjectType:ConsoleApp2/Program.cs";
    assertThat(getComponent(programComponentId2))
      .isNotNull();
  }

  @Test
  void testCSharpSharedFileWithOneProjectWithoutProjectBaseDir() throws IOException {
    runAnalysis("CSharpSharedFileWithOneProject");
    var projectKey = "CSharpSharedFileWithOneProject";
    var projectDir = TestUtils.projectDir(basePath, projectKey);
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK)
      .setProperty("sonar.sourceEncoding", "UTF-8")
      .setProperty("sonar.projectBaseDir", projectDir.resolve("ClassLib1").toAbsolutePath().toString()) // Common.cs file is outside of this base path and will not be uploaded to SQ
      .execute(ORCHESTRATOR);
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Restore,Rebuild");
    TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);

    assertThat(TestUtils.listComponents(ORCHESTRATOR, projectKey))
      .extracting(Components.Component::getKey)
      .containsExactlyInAnyOrder("CSharpSharedFileWithOneProject:Class1.cs"); // Common.cs is not present
  }

  @Test
  void testCSharpSharedFileWithOneProjectUsingProjectBaseDirAbsolute() throws IOException {
    runCSharpSharedFileWithOneProjectUsingProjectBaseDir(
      projectDir -> {
        try {
          return projectDir.toRealPath(LinkOption.NOFOLLOW_LINKS).toString();
        } catch (IOException e) {
          e.printStackTrace();
        }
        return null;
      });
  }

  @Test
  void testCSharpFramework48() throws IOException {
    var folderName = "CSharp.Framework.4.8";
    assumeFalse(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017")); // We can't run .NET Core SDK under VS 2017 CI context
    BuildResult buildResult = runAnalysis(folderName, true);

    assertUIWarnings(buildResult);
    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, folderName);
    assertThat(issues).hasSize(2)
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(
        tuple(SONAR_RULES_PREFIX + "S1134", folderName + ":Main/Common.cs"),
        tuple(SONAR_RULES_PREFIX + "S2699", folderName + ":UTs/CommonTest.cs"));
  }

  @Test
  void testCSharpSdk8() throws IOException {
    validateCSharpSdk("CSharp.SDK.8");
  }

  @Test
  void testScannerNet8NoAnalysisWarnings() throws IOException {
    // dotnet sdk tests should run only on VS 2022
    assumeTrue(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022"));

    BuildResult buildResult = runAnalysis("CSharp.SDK.8");

    assertThat(buildResult.getLogs()).doesNotContain("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS'");
    assertUIWarnings(buildResult);
  }

  @Test
  void testCSharpSdkLatest() throws IOException {
    validateCSharpSdk("CSharp.SDK.Latest");
  }

  /* TODO: This test doesn't work as expected. Relative path will create sub-folders on SonarQube and so files are not
           located where you expect them.
  @Test
  public void testCSharpSharedFileWithOneProjectUsingProjectBaseDirRelative() throws IOException {
    runCSharpSharedFileWithOneProjectUsingProjectBaseDir(projectDir -> "..\\..");
  } */

  @Test
  void testCSharpSharedFileWithOneProjectUsingProjectBaseDirAbsoluteShort() throws IOException {
    runCSharpSharedFileWithOneProjectUsingProjectBaseDir(Path::toString);
  }

  @Test
  void testProjectTypeDetectionWithWrongCasingReferenceName() throws IOException {
    BuildResult buildResult = runAnalysis("DotnetProjectTypeDetection");
    assertThat(buildResult.getLogs()).contains("Found 1 MSBuild C# project: 1 TEST project.");
  }

  @Test
  void testDuplicateAnalyzersWithSameNameAreNotRemoved() throws IOException {
    assumeTrue(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022")); // We can't build without MsBuild17
    var projectKey = "DuplicateAnalyzerReferences";
    Path projectDir = TestUtils.projectDir(basePath, projectKey);
    BuildResult buildResult = runNetCoreBeginBuildAndEnd(projectDir);

    assertThat(buildResult.getLogs()).doesNotContain("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS'");

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, projectKey);
    assertThat(issues).hasSize(3)
      .extracting(Issue::getRule)
      .containsExactlyInAnyOrder(
        SONAR_RULES_PREFIX + "S1481", // Program.cs line 7
        SONAR_RULES_PREFIX + "S1186", // Program.cs line 10
        SONAR_RULES_PREFIX + "S1481"); // Generator.cs line 18

    assertThat(TestUtils.getMeasureAsInteger("DuplicateAnalyzerReferences", "lines", ORCHESTRATOR)).isEqualTo(40);
    assertThat(TestUtils.getMeasureAsInteger("DuplicateAnalyzerReferences", "ncloc", ORCHESTRATOR)).isEqualTo(30);
    assertThat(TestUtils.getMeasureAsInteger("DuplicateAnalyzerReferences", "files", ORCHESTRATOR)).isEqualTo(2);
  }

  @Test
  void whenEachProjectIsOnDifferentDrives_AnalysisFails() throws IOException {
    try {
      Path projectDir = TestUtils.projectDir(basePath, "TwoDrivesTwoProjects");
      TestUtils.createVirtualDrive("Z:", projectDir, "DriveZ");

      BuildResult buildResult = runAnalysisWithoutProjectBasedDir(projectDir);

      assertThat(buildResult.isSuccess()).isFalse();
      assertThat(buildResult.getLogs()).contains("Generation of the sonar-properties file failed. Unable to complete the analysis.");
    } finally {
      TestUtils.deleteVirtualDrive("Z:");
    }
  }

  @Test
  void whenMajorityOfProjectsIsOnSameDrive_AnalysisSucceeds() throws IOException {
    try {
      var projectKey = "TwoDrivesThreeProjects";
      Path projectDir = TestUtils.projectDir(basePath, projectKey);
      TestUtils.createVirtualDrive("Y:", projectDir, "DriveY");

      BuildResult buildResult = runAnalysisWithoutProjectBasedDir(projectDir);
      assertThat(buildResult.isSuccess()).isTrue();
      assertThat(buildResult.getLogs()).contains("Using longest common projects path as a base directory: '" + projectDir);
      assertThat(buildResult.getLogs()).contains("WARNING: Directory 'Y:\\Subfolder' is not located under the base directory '" + projectDir + "' and will not be analyzed.");
      assertThat(buildResult.getLogs()).contains("WARNING: File 'Y:\\Subfolder\\Program.cs' is not located under the base directory '" + projectDir +
        "' and will not be analyzed.");
      assertThat(buildResult.getLogs()).contains("File was referenced by the following projects: 'Y:\\Subfolder\\DriveY.csproj'.");
      assertThat(TestUtils.projectIssues(ORCHESTRATOR, projectKey)).hasSize(2)
        .extracting(Issues.Issue::getRule, Issues.Issue::getComponent)
        .containsExactlyInAnyOrder(
          tuple("vbnet:S6145", projectKey),
          tuple(SONAR_RULES_PREFIX + "S1134", projectKey + ":DefaultDrive/Program.cs")
        );
    } finally {
      TestUtils.deleteVirtualDrive("Y:");
    }
  }

  @Test
  void testAzureFunctions_WithWrongBaseDirectory_AnalysisSucceeds() throws IOException {
    Path projectDir = TestUtils.projectDir(basePath, "ReproAzureFunctions");
    BuildResult buildResult = runAnalysisWithoutProjectBasedDir(projectDir);

    assertThat(buildResult.isSuccess()).isTrue();
    var temporaryFolderRoot = basePath.getParent().toFile().getCanonicalFile().toString();
    assertThat(buildResult.getLogs()).contains(" '" + temporaryFolderRoot);
  }

  @Test
  void incrementalPrAnalysis_NoCache() throws IOException {
    String projectKey = "incremental-pr-analysis-no-cache";
    Path projectDir = TestUtils.projectDir(basePath, "IncrementalPRAnalysis");
    File unexpectedUnchangedFiles = new File(projectDir.resolve(".sonarqube\\conf\\UnchangedFiles.txt").toString());
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    BuildResult result = TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token)
      .setDebugLogs(true) // To assert debug logs too
      .setProperty("sonar.pullrequest.base", "base-branch")
      .execute(ORCHESTRATOR);

    assertTrue(result.isSuccess());
    assertThat(unexpectedUnchangedFiles).doesNotExist();
    assertThat(result.getLogs()).contains("Processing analysis cache");

    if (ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(9, 9)) {
      assertThat(result.getLogs()).contains("Cache data is empty. A full analysis will be performed.");
    } else {
      assertThat(result.getLogs()).contains("Incremental PR analysis is available starting with SonarQube 9.9 or later.");
    }
  }

  @Test
  void incrementalPrAnalysis_ProducesUnchangedFiles() throws IOException {
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(9, 9)); // Public cache API was introduced in 9.9

    String projectKey = "IncrementalPRAnalysis";
    String baseBranch = TestUtils.getDefaultBranchName(ORCHESTRATOR);
    Path projectDir = TestUtils.projectDir(basePath, projectKey);
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token).execute(ORCHESTRATOR);
    TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

    BuildResult firstAnalysisResult = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);
    assertTrue(firstAnalysisResult.isSuccess());

    waitForCacheInitialization(projectKey, baseBranch);

    File fileToBeChanged = projectDir.resolve("IncrementalPRAnalysis\\WithChanges.cs").toFile();
    BufferedWriter writer = new BufferedWriter(new FileWriter(fileToBeChanged, true));
    writer.append(' ');
    writer.close();

    BuildResult result = TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token)
      .setDebugLogs(true) // To assert debug logs too
      .setProperty("sonar.pullrequest.base", baseBranch)
      .execute(ORCHESTRATOR);

    assertTrue(result.isSuccess());
    assertThat(result.getLogs()).contains("Processing analysis cache");
    assertThat(result.getLogs()).contains("Downloading cache. Project key: IncrementalPRAnalysis, branch: " + baseBranch + ".");

    Path expectedUnchangedFiles = projectDir.resolve(".sonarqube\\conf\\UnchangedFiles.txt");
    LOG.info("UnchangedFiles: " + expectedUnchangedFiles.toAbsolutePath());
    assertThat(expectedUnchangedFiles).exists();
    assertThat(Files.readString(expectedUnchangedFiles))
      .contains("Unchanged1.cs")
      .contains("Unchanged2.cs")
      .doesNotContain("WithChanges.cs"); // Was modified
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

  private void waitForCacheInitialization(String projectKey, String baseBranch) {
    await()
      .pollInterval(Duration.ofSeconds(1))
      .atMost(Duration.ofSeconds(120))
      .until(() -> {
        try {
          ORCHESTRATOR.getServer().newHttpCall("api/analysis_cache/get").setParam("project", projectKey).setParam("branch", baseBranch).setAuthenticationToken(ORCHESTRATOR.getDefaultAdminToken()).execute();
          return true;
        } catch (HttpException ex) {
          return false; // if the `execute()` method is not successful it throws HttpException
        }
      });
  }

  private void validateCSharpSdk(String folderName) throws IOException {
    // dotnet sdk tests should run only on VS 2022
    assumeTrue(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022"));

    runAnalysis(folderName);

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, folderName);

    assertThat(issues).hasSize(2)
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(
        tuple(SONAR_RULES_PREFIX + "S1134", folderName + ":Main/Common.cs"),
        tuple(SONAR_RULES_PREFIX + "S2699", folderName + ":UTs/CommonTest.cs"));
    // The AspNetCoreMvc/Views/Home/Index.cshtml contains an external CS0219 issue
    // which is currently not imported due to the fact that the generated code Index.cshtml.g.cs is in the object folder.
  }

  private void assertUIWarnings(BuildResult buildResult) {
    // AnalysisWarningsSensor was implemented starting from analyzer version 8.39.0.47922 (https://github.com/SonarSource/sonar-dotnet-enterprise/commit/39baabb01799aa1945ac5c80d150f173e6ada45f)
    // So it's available from SQ 9.9 onwards
    if (ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(9, 9)) {
      var warnings = TestUtils.getAnalysisWarningsTask(ORCHESTRATOR, buildResult);
      assertThat(warnings.getStatus()).isEqualTo(Ce.TaskStatus.SUCCESS);
      var warningsList = warnings.getWarningsList();
      assertThat(warningsList.stream().anyMatch(
        // The warning is appended to the timestamp, we want to assert only the message
        x -> x.endsWith("Multi-Language analysis is enabled. If this was not intended and you have issues such as hitting your LOC limit or analyzing unwanted files, please set " +
          "\"/d:sonar.scanner.scanAll=false\" in the begin step.")
      )).isTrue();
      assertThat(warningsList.size()).isEqualTo(1);
    }
  }

  private void runCSharpSharedFileWithOneProjectUsingProjectBaseDir(Function<Path, String> getProjectBaseDir)
    throws IOException {
    String folderName = "CSharpSharedFileWithOneProject";
    Path projectDir = TestUtils.projectDir(basePath, folderName);

    String token = TestUtils.getNewToken(ORCHESTRATOR);
    TestUtils.newScannerBegin(ORCHESTRATOR, folderName, projectDir, token)
      .setProperty("sonar.projectBaseDir", getProjectBaseDir.apply(projectDir))
      .setDebugLogs(true)
      .execute(ORCHESTRATOR);

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Restore,Rebuild");

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, folderName, token);
    assertTrue(result.isSuccess());
    assertThat(getComponent(folderName + ":Common.cs"))
      .isNotNull();
    String class1ComponentId = TestUtils.hasModules(ORCHESTRATOR)
      ? folderName + ":" + folderName + ":D8FEDBA2-D056-42FB-B146-5A409727B65D:Class1.cs"
      : folderName + ":ClassLib1/Class1.cs";
    assertThat(getComponent(class1ComponentId))
      .isNotNull();
  }

  private BuildResult runAnalysisWithoutProjectBasedDir(Path projectDir) {
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    String folderName = projectDir.getFileName().toString();
    TestUtils.newScannerBegin(ORCHESTRATOR, folderName, projectDir, token, ScannerClassifier.NET)
      .setProperty("sonar.projectBaseDir", null)  // Do NOT set "sonar.projectBaseDir" for this test. We need to remove the default value
      .setDebugLogs(true)
      .setProperty("sonar.sourceEncoding", "UTF-8")
      .execute(ORCHESTRATOR);

    BuildResult buildResult = TestUtils.runDotnetCommand(projectDir, "build", folderName + ".sln", "--no-incremental");
    assertThat(buildResult.getLastStatus()).isZero();

    return TestUtils.newScannerEnd(ORCHESTRATOR, projectDir, ScannerClassifier.NET, token).execute(ORCHESTRATOR);
  }

  private void assertProjectFileContains(String projectName, String textToLookFor) throws IOException {
    Path projectPath = TestUtils.projectDir(basePath, projectName);
    Path csProjPath = projectPath.resolve("RazorWebApplication\\RazorWebApplication.csproj");
    String str = FileUtils.readFileToString(csProjPath.toFile(), "utf-8");
    assertThat(str.indexOf(textToLookFor)).isPositive();
  }

  private BuildResult runAnalysis(String folderName) throws IOException {
    return runAnalysis(folderName, false);
  }

  private BuildResult runAnalysis(String folderName, Boolean useNuGet) throws IOException {
    Path projectDir = TestUtils.projectDir(basePath, folderName);
    return TestUtils.runAnalysis(projectDir, folderName, useNuGet);
  }

  private BuildResult runNetCoreBeginBuildAndEnd(Path projectDir) {
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    String folderName = projectDir.getFileName().toString();
    ScannerCommand scanner = TestUtils.newScannerBegin(ORCHESTRATOR, folderName, projectDir, token, ScannerClassifier.NET)
      // ensure that the Environment Variable parsing happens for .NET Core versions
      .setEnvironmentVariable("SONARQUBE_SCANNER_PARAMS", "{}")
      .setProperty("sonar.sourceEncoding", "UTF-8");

    scanner.execute(ORCHESTRATOR);

    // build project
    String[] arguments = new String[]{"build", folderName + ".sln"};
    int status = CommandExecutor.create().execute(Command.create("dotnet")
      .addArguments(arguments)
      // verbosity level: change 'm' to 'd' for detailed logs
      .addArguments("-v:m")
      .addArgument("/warnaserror:AD0001")
      .setDirectory(projectDir.toFile()), 5 * 60 * 1000);

    assertThat(status).isZero();

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Restore,Rebuild", folderName + ".sln");
    return TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, folderName, token, ScannerClassifier.NET, Collections.emptyList(), Collections.emptyList());
  }

  private void validateRazorProject(String projectKey) throws IOException {
    ORCHESTRATOR.getServer().provisionProject(projectKey, projectKey);

    if (TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017")) {
      return; // We can't build razor under VS 2017 CI context
    }

    Path projectDir = TestUtils.projectDir(basePath, projectKey);
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK).execute(ORCHESTRATOR);
    TestUtils.runNuGet(ORCHESTRATOR, projectDir, false, "restore");
    TestUtils.runDotnetCommand(projectDir, "build", "--no-incremental");
    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);

    assertTrue(result.isSuccess());

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, projectKey);
    List<String> ruleKeys = issues.stream().map(Issue::getRule).collect(Collectors.toList());

    assertThat(ruleKeys).containsAll(Arrays.asList(SONAR_RULES_PREFIX + "S1118", SONAR_RULES_PREFIX + "S1186"));

    assertThat(TestUtils.getMeasureAsInteger(projectKey, "lines", ORCHESTRATOR)).isEqualTo(49);
    assertThat(TestUtils.getMeasureAsInteger(projectKey, "ncloc", ORCHESTRATOR)).isEqualTo(39);
    assertThat(TestUtils.getMeasureAsInteger(projectKey, "files", ORCHESTRATOR)).isEqualTo(2);
  }

  private void testExcludedAndTest(ScannerCommand build, String projectKeyName, Path projectDir, String token, int expectedTestProjectIssues) {
    testExcludedAndTest(build, projectKeyName, projectDir, token, expectedTestProjectIssues, Collections.EMPTY_LIST);
  }

  private void testExcludedAndTest(ScannerCommand scanner, String projectKeyName, Path projectDir, String token, int expectedTestProjectIssues,
    List<EnvironmentVariable> environmentVariables) {
    String normalProjectKey = TestUtils.hasModules(ORCHESTRATOR)
      ? String.format("%1$s:%1$s:B93B287C-47DB-4406-9EAB-653BCF7D20DC", projectKeyName)
      : String.format("%1$s:Normal/Program.cs", projectKeyName);
    String testProjectKey = TestUtils.hasModules(ORCHESTRATOR)
      ? String.format("%1$s:%1$s:2DC588FC-16FB-42F8-9FDA-193852E538AF", projectKeyName)
      : String.format("%1$s:Test/UnitTest1.cs", projectKeyName);

    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ExcludedTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKeyName, projectKeyName);
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKeyName, "cs", "ProfileForTest");

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

  private static Components.Component getComponent(String componentKey) {
    return newWsClient().components().show(new ShowRequest().setComponent(componentKey)).getComponent();
  }

  private static WsClient newWsClient() {
    return TestUtils.newWsClient(ORCHESTRATOR);
  }

  private List<Issue> filter(List<Issue> issues, String ruleIdPrefix) {
    return issues
      .stream()
      .filter(x -> x.getRule().startsWith(ruleIdPrefix))
      .collect(Collectors.toList());
  }
}
