/*
 * Scanner for MSBuild :: Integration Tests
 * Copyright (C) 2016-2016 SonarSource SA
 * mailto:contact AT sonarsource DOT com
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
import com.sonar.orchestrator.junit.SingleStartExternalResource;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.locator.Location;
import com.sonar.orchestrator.locator.URLLocation;
import java.io.IOException;
import java.net.MalformedURLException;
import java.net.URL;
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

  private static Orchestrator ORCHESTRATOR;

  @ClassRule
  public static TemporaryFolder temp = new TemporaryFolder();

  @ClassRule
  public static SingleStartExternalResource resource = new SingleStartExternalResource() {
    @Override
    protected void beforeAll() {
      Path customRoslyn = TestUtils.getCustomRoslynPlugin();
      ORCHESTRATOR = Orchestrator.builderEnv()
        .setOrchestratorProperty("csharpVersion", "LATEST_RELEASE")
        .addPlugin("csharp")
        .addPlugin(FileLocation.of(customRoslyn.toFile()))
        .setOrchestratorProperty("fxcopVersion", "LATEST_RELEASE")
        // TODO uncomment when sonar-fxcop 1.1 is released and available in update center
        //.addPlugin("fxcop")
        .addPlugin(getFxCopPluginUrl())
        .build();
      ORCHESTRATOR.start();
    }

    @Override
    protected void afterAll() {
      ORCHESTRATOR.stop();
    }
  };

  private static Location getFxCopPluginUrl() {
    try {
      return URLLocation.create(new URL("https://github.com/SonarQubeCommunity/sonar-fxcop/releases/download/1.1-rc1/sonar-fxcop-plugin-1.1-rc1.jar"));
    } catch (MalformedURLException e) {
      throw new IllegalStateException();
    }
  }

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

  @Test
  public void should_not_crash_when_fxcop_is_active() throws IOException {
    String response = ORCHESTRATOR.getServer().adminWsClient().post("api/rules/create",
            "name", "customfxcop",
            "severity", "MAJOR",
            "custom_key", "customfxcop",
            "markdown_description", "custom rule",
            "template_key", "fxcop:CustomRuleTemplate",
            "params", "CheckId=CA2201");

    System.out.println("RESPONSE: " + response);
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfileFxCop.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTestFxCop");

    Path projectDir = TestUtils.projectDir(temp, "SQLServerSolution");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
            .addArgument("begin")
            .setProjectKey(PROJECT_KEY)
            .setProjectName("sample")
            .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
            .addArgument("end"));
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
