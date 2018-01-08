/*
 * Scanner for MSBuild :: Integration Tests
 * Copyright (C) 2016-2018 SonarSource SA
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
import com.sonar.orchestrator.locator.FileLocation;
import java.nio.file.Path;
import java.util.Collections;
import java.util.List;
import javax.annotation.CheckForNull;
import org.junit.Before;
import org.junit.ClassRule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;
import org.sonar.wsclient.issue.Issue;
import org.sonar.wsclient.issue.IssueQuery;
import org.sonarqube.ws.WsMeasures;
import org.sonarqube.ws.client.HttpConnector;
import org.sonarqube.ws.client.WsClient;
import org.sonarqube.ws.client.WsClientFactories;
import org.sonarqube.ws.client.measure.ComponentWsRequest;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * csharpPlugin.version: csharp plugin to modify (installing scanner payload) and use. If not specified, uses 5.1. 
 * scannerForMSBuild.version: scanner to use. If not specified, uses the one built in ../
 * scannerForMSBuildPayload.version: scanner to embed in the csharp plugin. If not specified, uses the one built in ../
 * sonar.runtimeVersion: SQ to use
 */
public class SQLServerTest {
  private static final String PROJECT_KEY = "my.project";
  private static final String MODULE_KEY = "my.project:my.project:692D7F66-3DC3-4FE3-9274-DD9A1CA06482";
  private static final String FILE_KEY = MODULE_KEY + ":util/SqlStoredProcedure1.cs";

  @ClassRule
  public static Orchestrator ORCHESTRATOR = Orchestrator.builderEnv()
    .setOrchestratorProperty("csharpVersion", "LATEST_RELEASE")
    .addPlugin("csharp")
    .addPlugin(FileLocation.of(TestUtils.getCustomRoslynPlugin().toFile()))
    .build();

  @ClassRule
  public static TemporaryFolder temp = new TemporaryFolder();

  @Before
  public void setUp() {
    ORCHESTRATOR.resetData();
  }

  @Test
  public void should_find_issues_in_cs_files() throws Exception {
    Path projectDir = TestUtils.projectDir(temp, "SQLServerSolution");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("sample")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("end"));

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    assertThat(issues).hasSize(3);
    assertThat(getMeasureAsInteger(FILE_KEY, "ncloc")).isEqualTo(19);
    assertThat(getMeasureAsInteger(PROJECT_KEY, "ncloc")).isEqualTo(25);
    assertThat(getMeasureAsInteger(FILE_KEY, "lines")).isEqualTo(23);
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
}
