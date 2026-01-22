/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.sonarqube.ws.Issues.Issue;

import java.util.List;

import static com.sonar.it.scanner.msbuild.utils.SonarAssertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;

@ExtendWith({ServerTests.class, ContextExtension.class})
class WarningWave7Test {

  @Test
  @MSBuildMinVersion(16)
  void warningWave7_CS8981_isRaised() {
    var context = AnalysisContext.forServer("WarningWave7Test");
    var result = context.runAnalysis();

    assertThat(result.isSuccess()).isTrue();

    List<Issue> issues = TestUtils.projectIssues(context.orchestrator, context.projectKey);
    assertThat(issues)
      .extracting(Issue::getRule, Issue::getComponent)
      .contains(tuple("external_roslyn:CS8981", context.projectKey + ":Program.cs"));
  }
}
