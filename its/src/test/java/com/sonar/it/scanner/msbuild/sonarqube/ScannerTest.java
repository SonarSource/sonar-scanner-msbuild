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
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.sonarqube.ws.Issues.Issue;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assumptions.assumeThat;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assumptions.assumeTrue;

@ExtendWith({ServerTests.class, ContextExtension.class})
class ScannerTest {
  private static final String SONAR_RULES_PREFIX = "csharpsquid:";

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

    assertThat(TestUtils.getComponent(context.projectKey + ":Common.cs")).isNotNull();
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
    // ensure that the Environment Variable parsing happens for .NET Core versions
    var context = AnalysisContext.forServer("DuplicateAnalyzerReferences", ScannerClassifier.NET);
    context.begin.setEnvironmentVariable("SONARQUBE_SCANNER_PARAMS", "{}");
    context.build.addArgument("-v:m").setTimeout(5 * 60 * 1000);
    var logs = context.runAnalysis().end().getLogs();
    var issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);

    assertThat(logs).doesNotContain("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS'");
    assertThat(issues).hasSize(3)
      .extracting(Issue::getRule)
      .containsExactlyInAnyOrder(
        SONAR_RULES_PREFIX + "S1481", // Program.cs line 7
        SONAR_RULES_PREFIX + "S1186", // Program.cs line 10
        SONAR_RULES_PREFIX + "S1481"); // Generator.cs line 18

    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "lines", ORCHESTRATOR)).isEqualTo(40);
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "ncloc", ORCHESTRATOR)).isEqualTo(30);
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "files", ORCHESTRATOR)).isEqualTo(2);
  }
}
