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
import com.sonar.it.scanner.msbuild.utils.BuildCommand;
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.QualityProfile;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import java.util.Arrays;
import java.util.List;
import java.util.stream.Collectors;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.sonarqube.ws.Issues.Issue;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;
import static org.junit.jupiter.api.Assumptions.assumeFalse;

@ExtendWith({ServerTests.class, ContextExtension.class})
class ExternalIssuesTest {

  @Test
  void checkExternalIssuesVB() {
    var context = AnalysisContext.forServer("ExternalIssues.VB").setQualityProfile(QualityProfile.VB_S3385_S125);
    context.runAnalysis();

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    List<String> ruleKeys = issues.stream().map(Issue::getRule).collect(Collectors.toList());
    // The same set of Sonar issues should be reported, regardless of whether external issues are imported or not
    assertThat(ruleKeys).containsAll(Arrays.asList(
      "vbnet:S112",
      "vbnet:S3385"));

    if (ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(7, 4)) {
      // if external issues are imported, then there should also be some CodeCracker errors.
      assertThat(ruleKeys).containsAll(Arrays.asList(
        "external_roslyn:CC0021",
        "external_roslyn:CC0062"));
      assertThat(issues).hasSize(4);
    } else {
      // Not expecting any external issues
      assertThat(issues).hasSize(2);
    }
  }

  @Test
  void checkExternalIssuesCS() {
    var context = AnalysisContext.forServer("ExternalIssues.CS").setQualityProfile(QualityProfile.CS_S1134_S125);
    context.runAnalysis();

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    List<String> ruleKeys = issues.stream().map(Issue::getRule).collect(Collectors.toList());

    // The same set of Sonar issues should be reported, regardless of whether external issues are imported or not
    assertThat(ruleKeys).containsAll(Arrays.asList(
      "csharpsquid:S125",
      "csharpsquid:S1134"));

    if (ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(7, 4)) {
      // if external issues are imported, then there should also be some
      // Wintellect errors.  However, only file-level issues are imported.
      assertThat(ruleKeys).containsAll(List.of("external_roslyn:Wintellect004"));
      assertThat(issues).hasSize(3);
    } else {
      // Not expecting any external issues
      assertThat(issues).hasSize(2);
    }
  }

  @Test
  void testIgnoreIssuesDoesNotRemoveSourceGenerator() {
    assumeFalse(BuildCommand.msBuildPath().contains("2017")); // We can't run .NET Core SDK under VS 2017 CI context
    var context = AnalysisContext.forServer("IgnoreIssuesDoesNotRemoveSourceGenerator");
    context.begin.setProperty("sonar.cs.roslyn.ignoreIssues", "true");
    context.runAnalysis();

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    assertThat(issues)
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(
        tuple("csharpsquid:S1481", context.projectKey + ":ProjectWithSourceGenAndAnalyzer/Program.cs"),
        tuple("csharpsquid:S1186", context.projectKey + ":ProjectWithSourceGenAndAnalyzer/Program.cs")
      );
  }
}
