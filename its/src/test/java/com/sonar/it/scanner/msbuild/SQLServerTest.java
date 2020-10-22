/*
 * Scanner for MSBuild :: Integration Tests
 * Copyright (C) 2016-2020 SonarSource SA
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
package com.sonar.it.scanner.msbuild;

import com.sonar.it.scanner.SonarScannerTestSuite;
import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.http.HttpMethod;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.List;
import org.junit.Before;
import org.junit.ClassRule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;
import org.sonarqube.ws.Issues.Issue;

import static org.assertj.core.api.Assertions.assertThat;

public class SQLServerTest {
  private static final String PROJECT_KEY = "my.project";

  @ClassRule
  public static Orchestrator ORCHESTRATOR = SonarScannerTestSuite.ORCHESTRATOR;

  @ClassRule
  public static TemporaryFolder temp = TestUtils.createTempFolder();

  @Before
  public void setUp(){
    TestUtils.reset(ORCHESTRATOR);
  }

  @Test
  public void should_find_issues_in_cs_files() throws Exception {
    Path projectDir = TestUtils.projectDir(temp, "SQLServerSolution");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("sample")
      .setProperty("sonar.projectBaseDir", Paths.get(projectDir.toAbsolutePath().toString(), "Database1").toString())
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY);

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(3);
    assertThat(TestUtils.getMeasureAsInteger(getFileKey(), "ncloc", ORCHESTRATOR)).isEqualTo(19);
    assertThat(TestUtils.getMeasureAsInteger(PROJECT_KEY, "ncloc", ORCHESTRATOR)).isEqualTo(25);
    assertThat(TestUtils.getMeasureAsInteger(getFileKey(), "lines", ORCHESTRATOR)).isEqualTo(23);
  }

  private static String getFileKey() {
    return TestUtils.hasModules(ORCHESTRATOR) ? "my.project:my.project:692D7F66-3DC3-4FE3-9274-DD9A1CA06482:util/SqlStoredProcedure1.cs" : "my.project:util/SqlStoredProcedure1.cs";
  }
}
