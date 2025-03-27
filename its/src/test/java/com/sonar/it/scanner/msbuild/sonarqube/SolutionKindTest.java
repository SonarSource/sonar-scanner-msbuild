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
import com.sonar.it.scanner.msbuild.utils.AnalysisResult;
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import java.io.IOException;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.List;
import java.util.stream.Collectors;
import org.apache.commons.io.FileUtils;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.sonarqube.ws.Ce;
import org.sonarqube.ws.Components;
import org.sonarqube.ws.Issues.Issue;
import org.sonarqube.ws.client.WsClient;
import org.sonarqube.ws.client.components.ShowRequest;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assumptions.assumeFalse;
import static org.junit.jupiter.api.Assumptions.assumeTrue;

@ExtendWith({ServerTests.class, ContextExtension.class})
class SolutionKindTest {
  private static final String SONAR_RULES_PREFIX = "csharpsquid:";

  @TempDir
  public Path basePath;

  @Test
  void testXamlCompilation() throws IOException {
    // We can't build with MSBuild 15
    // error MSB4018: System.InvalidOperationException: This implementation is not part of the Windows Platform FIPS validated cryptographic algorithms.
    // at System.Security.Cryptography.MD5CryptoServiceProvider..ctor()
    assumeFalse(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017"));

    var context = AnalysisContext.forServer("XamarinApplication");
    context.runAnalysis();

    List<Issue> issues = TestUtils.projectIssues(context.orchestrator, context.projectKey);
    assertThat(issues)
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(
        tuple(SONAR_RULES_PREFIX + "S927", context.projectKey + ":XamarinApplication.iOS/AppDelegate.cs"),
        tuple(SONAR_RULES_PREFIX + "S927", context.projectKey + ":XamarinApplication.iOS/AppDelegate.cs"),
        tuple(SONAR_RULES_PREFIX + "S1118", context.projectKey + ":XamarinApplication.iOS/Main.cs"),
        tuple(SONAR_RULES_PREFIX + "S1186", context.projectKey + ":XamarinApplication.iOS/Main.cs"),
        tuple(SONAR_RULES_PREFIX + "S1186", context.projectKey + ":XamarinApplication/App.xaml.cs"),
        tuple(SONAR_RULES_PREFIX + "S1186", context.projectKey + ":XamarinApplication/App.xaml.cs"),
        tuple(SONAR_RULES_PREFIX + "S1186", context.projectKey + ":XamarinApplication/App.xaml.cs"),
        tuple(SONAR_RULES_PREFIX + "S1134", context.projectKey + ":XamarinApplication/MainPage.xaml.cs"),
        tuple("external_roslyn:CS0618", context.projectKey + ":XamarinApplication.iOS/Main.cs"));

    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "lines", context.orchestrator)).isEqualTo(149);
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "ncloc", context.orchestrator)).isEqualTo(93);
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "files", context.orchestrator)).isEqualTo(6);
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey + ":XamarinApplication.iOS", "lines", context.orchestrator)).isEqualTo(97);
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey + ":XamarinApplication", "lines", context.orchestrator)).isEqualTo(52);
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
    var analysisContext = AnalysisContext.forServer("CSharpAllFlat");
    analysisContext.build.addArgument("CSharpAllFlat.sln");
    analysisContext.runAnalysis();

    assertThat(getComponent(analysisContext.projectKey + ":Common.cs")).isNotNull();
  }

  @Test
  void testCSharpSharedFiles() throws IOException {
    var analysisContext = AnalysisContext.forServer("CSharpSharedFiles");
    analysisContext.runAnalysis();

    assertThat(getComponent(analysisContext.projectKey + ":Common.cs"))
      .isNotNull();
    assertThat(getComponent(analysisContext.projectKey + ":ClassLib1/Class1.cs"))
      .isNotNull();
    assertThat(getComponent(analysisContext.projectKey + ":ClassLib2/Class2.cs"))
      .isNotNull();
  }

  @Test
  void testCSharpSharedProjectType() throws IOException {
    var analysisContext = AnalysisContext.forServer("CSharpSharedProjectType");
    analysisContext.runAnalysis();

    assertThat(getComponent(analysisContext.projectKey + ":SharedProject/TestEventInvoke.cs"))
      .isNotNull();
    assertThat(getComponent(analysisContext.projectKey + ":ConsoleApp1/Program.cs"))
      .isNotNull();
    assertThat(getComponent(analysisContext.projectKey + ":ConsoleApp2/Program.cs"))
      .isNotNull();
  }

  @Test
  void testCSharpFramework48() throws IOException {
    var folderName = "CSharp.Framework.4.8";
    assumeFalse(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017")); // We can't run .NET Core SDK under VS 2017 CI context
    var analysisContext = AnalysisContext.forServer(folderName);
    TestUtils.runNuGet(analysisContext.orchestrator, analysisContext.projectDir, false, "restore");
    var analysisResult = analysisContext.runAnalysis();

    assertUIWarnings(analysisResult);
    List<Issue> issues = TestUtils.projectIssues(analysisContext.orchestrator, analysisContext.projectKey);
    assertThat(issues).hasSize(2)
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(
        tuple(SONAR_RULES_PREFIX + "S1134", analysisContext.projectKey + ":Main/Common.cs"),
        tuple(SONAR_RULES_PREFIX + "S2699", analysisContext.projectKey + ":UTs/CommonTest.cs"));
  }

  @Test
  void testCSharpSdk8() throws IOException {
    validateCSharpSdk("CSharp.SDK.8");
  }

  @Test
  void testScannerNet8NoAnalysisWarnings() throws IOException {
    // dotnet sdk tests should run only on VS 2022
    assumeTrue(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022"));

    var analysisContext = AnalysisContext.forServer("CSharp.SDK.8");
    var analysisResult = analysisContext.runAnalysis();

    assertThat(analysisResult.logs()).doesNotContain("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS'");
    assertUIWarnings(analysisResult);
  }

  @Test
  void testCSharpSdkLatest() throws IOException {
    validateCSharpSdk("CSharp.SDK.Latest");
  }

  private void validateCSharpSdk(String folderName) throws IOException {
    // dotnet sdk tests should run only on VS 2022
    assumeTrue(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022"));

    var analysisContext = AnalysisContext.forServer(folderName);
    analysisContext.runAnalysis();

    List<Issue> issues = TestUtils.projectIssues(analysisContext.orchestrator, analysisContext.projectKey);

    assertThat(issues).hasSize(2)
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(
        tuple(SONAR_RULES_PREFIX + "S1134", analysisContext.projectKey + ":Main/Common.cs"),
        tuple(SONAR_RULES_PREFIX + "S2699", analysisContext.projectKey + ":UTs/CommonTest.cs"));
    // The AspNetCoreMvc/Views/Home/Index.cshtml contains an external CS0219 issue
    // which is currently not imported due to the fact that the generated code Index.cshtml.g.cs is in the object folder.
  }

  private void assertUIWarnings(AnalysisResult analysisResult) {
    // AnalysisWarningsSensor was implemented starting from analyzer version 8.39.0.47922 (https://github.com/SonarSource/sonar-dotnet-enterprise/commit/39baabb01799aa1945ac5c80d150f173e6ada45f)
    // So it's available from SQ 9.9 onwards
    if (ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(9, 9)) {
      var warnings = TestUtils.getAnalysisWarningsTask(ORCHESTRATOR, analysisResult.end());
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

  private void assertProjectFileContains(String projectName, String textToLookFor) throws IOException {
    Path projectPath = TestUtils.projectDir(basePath, projectName);
    Path csProjPath = projectPath.resolve("RazorWebApplication\\RazorWebApplication.csproj");
    String str = FileUtils.readFileToString(csProjPath.toFile(), "utf-8");
    assertThat(str.indexOf(textToLookFor)).isPositive();
  }

  private void validateRazorProject(String project) {
    var analysisContext = AnalysisContext.forServer(project);
    var result = analysisContext.runAnalysis();

    List<Issue> issues = TestUtils.projectIssues(analysisContext.orchestrator, analysisContext.projectKey);
    List<String> ruleKeys = issues.stream().map(Issue::getRule).collect(Collectors.toList());

    assertThat(ruleKeys).containsAll(Arrays.asList(SONAR_RULES_PREFIX + "S1118", SONAR_RULES_PREFIX + "S1186"));

    assertThat(TestUtils.getMeasureAsInteger(analysisContext.projectKey, "lines", analysisContext.orchestrator)).isEqualTo(49);
    assertThat(TestUtils.getMeasureAsInteger(analysisContext.projectKey, "ncloc", analysisContext.orchestrator)).isEqualTo(39);
    assertThat(TestUtils.getMeasureAsInteger(analysisContext.projectKey, "files", analysisContext.orchestrator)).isEqualTo(2);
  }

  private static Components.Component getComponent(String componentKey) {
    return newWsClient().components().show(new ShowRequest().setComponent(componentKey)).getComponent();
  }

  private static WsClient newWsClient() {
    return TestUtils.newWsClient(ORCHESTRATOR);
  }
}
