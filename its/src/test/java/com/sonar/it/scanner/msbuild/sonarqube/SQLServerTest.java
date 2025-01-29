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

import com.sonar.it.scanner.msbuild.utils.TestUtils;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.List;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.sonarqube.ws.Issues.Issue;

import static com.sonar.it.scanner.msbuild.sonarqube.Tests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;

@ExtendWith(Tests.class)
class SQLServerTest {
  private static final String PROJECT_KEY = "my.project";

  @TempDir
  public Path basePath;

  @BeforeEach
  public void setUp() {
    TestUtils.reset(ORCHESTRATOR);
  }

  @Test
  void should_find_issues_in_cs_files() throws Exception {
    Path projectDir = TestUtils.projectDir(basePath, "SQLServerSolution");
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("sample")
      .setProperty("sonar.projectBaseDir", Paths.get(projectDir.toAbsolutePath().toString(), "Database1").toString())
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY, token);

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    if (ORCHESTRATOR.getServer().version().isGreaterThan(9, 9)) {
      assertThat(issues).hasSize(4);
    } else {
      assertThat(issues).hasSize(3);
    }
    assertThat(TestUtils.getMeasureAsInteger(getFileKey(), "ncloc", ORCHESTRATOR)).isEqualTo(19);
    assertThat(TestUtils.getMeasureAsInteger(PROJECT_KEY, "ncloc", ORCHESTRATOR)).isEqualTo(36);
    assertThat(TestUtils.getMeasureAsInteger(getFileKey(), "lines", ORCHESTRATOR)).isEqualTo(23);
  }

  private static String getFileKey() {
    return TestUtils.hasModules(ORCHESTRATOR) ? "my.project:my.project:692D7F66-3DC3-4FE3-9274-DD9A1CA06482:util/SqlStoredProcedure1.cs" : "my.project:util/SqlStoredProcedure1.cs";
  }
}
