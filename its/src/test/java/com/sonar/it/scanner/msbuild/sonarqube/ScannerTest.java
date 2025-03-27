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
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.QualityProfiles;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.ScannerCommand;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import java.nio.file.Path;
import java.util.Collections;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.sonarqube.ws.Components;
import org.sonarqube.ws.Issues.Issue;
import org.sonarqube.ws.client.WsClient;
import org.sonarqube.ws.client.components.ShowRequest;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assumptions.assumeThat;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assumptions.assumeTrue;

@ExtendWith({ServerTests.class, ContextExtension.class})
class ScannerTest {
  private static final String SONAR_RULES_PREFIX = "csharpsquid:";

  @TempDir
  public Path basePath;

  @Test
  void testSample() {
    // TODO: SCAN4NET-325 Remove classifying as .NET
    var context = AnalysisContext.forServer("ProjectUnderTest", ScannerClassifier.NET);
    ORCHESTRATOR.getServer().provisionProject(context.projectKey, context.projectKey);
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(context.projectKey, "cs", QualityProfiles.CS_S1134);
    var result = context.runAnalysis();

    assertTrue(result.isSuccess());
    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    // 1 * csharpsquid:S1134 (line 34)
    assertThat(issues).hasSize(1);
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "ncloc", ORCHESTRATOR)).isEqualTo(25);
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey + ":ProjectUnderTest/Foo.cs", "ncloc", ORCHESTRATOR)).isEqualTo(25);
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey + ":ProjectUnderTest/Foo.cs", "lines", ORCHESTRATOR)).isEqualTo(52);
  }


  @Test
  void testNoActiveRule() {
    var context = AnalysisContext.forServer("ProjectUnderTest", ScannerClassifier.NET);
    ORCHESTRATOR.getServer().provisionProject(context.projectKey, context.projectKey);
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(context.projectKey, "cs", QualityProfiles.CS_Empty);
    var result = context.runAnalysis();

    assertTrue(result.isSuccess());
    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    assertThat(issues).isEmpty();
  }

  @Test
  void excludeAssemblyAttribute() {
    var context = AnalysisContext.forServer("AssemblyAttribute", ScannerClassifier.NET);
    ORCHESTRATOR.getServer().provisionProject(context.projectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(context.projectKey, "cs", QualityProfiles.CS_S1134);
    var result = context.runAnalysis();

    assertTrue(result.isSuccess());
    assertThat(result.end().getLogs())
      .doesNotContain("File is not under the project directory and cannot currently be analysed by SonarQube")
      .doesNotContain("AssemblyAttributes.cs");
  }

  @Test
  void testTargetUninstall() {
    // TODO: SCAN4NET-314 Use tag
    assumeTrue(!System.getProperty("os.name").contains("Windows") || !TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017"));
    var context = AnalysisContext.forServer("CSharpAllFlat", ScannerClassifier.NET);
    context.build.addArgument("CSharpAllFlat.sln");
    context.runAnalysis();

    var result = context.build.execute();
    assertTrue(result.isSuccess());

    assertThat(getComponent(context.projectKey + ":Common.cs")).isNotNull();
  }

  @Test
  void testProjectTypeDetectionWithWrongCasingReferenceName() {
    var context = AnalysisContext.forServer("DotnetProjectTypeDetection", ScannerClassifier.NET);
    var endLogs = context.runAnalysis().end().getLogs();

    assertThat(endLogs).contains("Found 1 MSBuild C# project: 1 TEST project.");
  }

  @Test
  void testDuplicateAnalyzersWithSameNameAreNotRemoved() {
    // TODO: SCAN4NET-314 Remove this assumption to use JUnit tags
    assumeThat(System.getProperty("os.name")).contains("Windows"); // We don't want to run this on non-Windows platforms
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

  private static Components.Component getComponent(String componentKey) {
    return newWsClient().components().show(new ShowRequest().setComponent(componentKey)).getComponent();
  }

  private static WsClient newWsClient() {
    return TestUtils.newWsClient(ORCHESTRATOR);
  }
}
