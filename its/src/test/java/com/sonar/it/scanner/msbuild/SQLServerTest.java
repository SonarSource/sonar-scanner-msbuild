/*
 * Scanner for MSBuild :: Integration Tests
 * Copyright (C) 2016-2019 SonarSource SA
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

import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.container.Edition;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.locator.MavenLocation;
import java.nio.file.Path;
import java.util.Collections;
import java.util.List;
import javax.annotation.CheckForNull;
import org.junit.Before;
import org.junit.ClassRule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;
import org.sonarqube.ws.Issues.Issue;
import org.sonarqube.ws.WsMeasures;
import org.sonarqube.ws.client.HttpConnector;
import org.sonarqube.ws.client.WsClient;
import org.sonarqube.ws.client.WsClientFactories;
import org.sonarqube.ws.client.measure.ComponentWsRequest;

import static java.util.Objects.requireNonNull;
import static org.assertj.core.api.Assertions.assertThat;

public class SQLServerTest {
  private static final String PROJECT_KEY = "my.project";

  @ClassRule
  public static Orchestrator ORCHESTRATOR = Orchestrator.builderEnv()
    .setSonarVersion(TestUtils.replaceLtsVersion(System.getProperty("sonar.runtimeVersion", "LATEST_RELEASE")))
    .setEdition(Edition.DEVELOPER)
    .addPlugin(MavenLocation.of("org.sonarsource.dotnet","sonar-csharp-plugin", "LATEST_RELEASE"))
    .activateLicense()
    .build();

  @ClassRule
  public static TemporaryFolder temp = TestUtils.createTempFolder();

  @Before
  public void setUp() {
    ORCHESTRATOR.resetData();
  }

  @Test
  public void should_find_issues_in_cs_files() throws Exception {
    Path projectDir = TestUtils.projectDir(temp, "SQLServerSolution");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("sample")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("end"));

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(3);
    assertThat(getMeasureAsInteger(getFileKey(), "ncloc")).isEqualTo(19);
    assertThat(getMeasureAsInteger(PROJECT_KEY, "ncloc")).isEqualTo(25);
    assertThat(getMeasureAsInteger(getFileKey(), "lines")).isEqualTo(23);
  }

  @CheckForNull
  private static Integer getMeasureAsInteger(String componentKey, String metricKey) {
    WsMeasures.Measure measure = getMeasure(componentKey, metricKey);
    return (measure == null) ? null : Integer.parseInt(measure.getValue());
  }

  @CheckForNull
  private static WsMeasures.Measure getMeasure(String componentKey, String metricKey) {
    WsMeasures.ComponentWsResponse response = newWsClient().measures().component(new ComponentWsRequest()
      .setComponentKey(componentKey)
      .setMetricKeys(Collections.singletonList(metricKey)));
    List<WsMeasures.Measure> measures = response.getComponent().getMeasuresList();
    return measures.size() == 1 ? measures.get(0) : null;
  }

  private static WsClient newWsClient() {
    return WsClientFactories.getDefault().newClient(HttpConnector.newBuilder()
      .url(ORCHESTRATOR.getServer().getUrl())
      .build());
  }

  private static String getFileKey() {
    return TestUtils.hasModules(ORCHESTRATOR) ? "my.project:my.project:692D7F66-3DC3-4FE3-9274-DD9A1CA06482:util/SqlStoredProcedure1.cs" : "my.project:util/SqlStoredProcedure1.cs";
  }
}
