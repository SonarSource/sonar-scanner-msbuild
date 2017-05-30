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
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.util.ZipUtils;
import java.io.File;
import java.net.URL;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.List;
import java.util.stream.Collectors;
import javax.annotation.CheckForNull;
import javax.annotation.Nullable;
import org.apache.commons.io.FileUtils;
import org.junit.Assume;
import org.junit.Before;
import org.junit.BeforeClass;
import org.junit.ClassRule;
import org.junit.Ignore;
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
 * Only cpp, without C# plugin
 *
 */
@Ignore("Do not pass on cix")
public class CppTest {
  private static final String PROJECT_KEY = "cpp";
  private static final String FILE_KEY = "cpp:cpp:A8B8B694-4489-4D82-B9A0-7B63BF0B8FCE:ConsoleApp.cpp";

  @BeforeClass
  public static void checkSkip() {
    Assume.assumeTrue("Disable for old scanner (needs C# plugin installed to get the payload)",
      TestUtils.getScannerVersion() == null || !TestUtils.getScannerVersion().equals("2.1.0.0"));
  }

  @ClassRule
  public static Orchestrator ORCHESTRATOR = Orchestrator.builderEnv()
    .setOrchestratorProperty("cppVersion", "LATEST_RELEASE")
    .addPlugin("cpp")
    .activateLicense("cpp")
    .build();

  @ClassRule
  public static TemporaryFolder temp = new TemporaryFolder();

  @Before
  public void cleanup() {
    ORCHESTRATOR.resetData();
  }

  @Test
  public void testCppOnly() throws Exception {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/CppSolution/TestQualityProfileCpp.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "Cpp");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cpp", "ProfileForTestCpp");

    Path projectDir = TestUtils.projectDir(temp, "CppSolution");
    File wrapperOutDir = new File(projectDir.toFile(), "out");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("Cpp")
      .setProjectVersion("1.0")
      .setProperty("sonar.cfamily.build-wrapper-output", wrapperOutDir.toString()));
    File buildWrapper = temp.newFile();
    File buildWrapperDir = temp.newFolder();
    FileUtils.copyURLToFile(new URL(ORCHESTRATOR.getServer().getUrl() + "/static/cpp/build-wrapper-win-x86.zip"), buildWrapper);
    ZipUtils.unzip(buildWrapper, buildWrapperDir);

    TestUtils.runMSBuildWithBuildWrapper(ORCHESTRATOR, projectDir, new File(buildWrapperDir, "build-wrapper-win-x86/build-wrapper-win-x86-64.exe"),
      wrapperOutDir, "/t:Rebuild");

    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("end"));
    assertThat(result.getLogs()).doesNotContain("Invalid character encountered in file");

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();

    List<String> keys = issues.stream().map(i -> i.ruleKey()).collect(Collectors.toList());
    assertThat(keys).containsAll(Arrays.asList("cpp:S106"));

    assertThat(getMeasureAsInteger(PROJECT_KEY, "ncloc")).isEqualTo(15);
    assertThat(getMeasureAsInteger(FILE_KEY, "ncloc")).isEqualTo(8);
  }

  @CheckForNull
  static Integer getMeasureAsInteger(String componentKey, String metricKey) {
    WsMeasures.Measure measure = getMeasure(componentKey, metricKey);
    return (measure == null) ? null : Integer.parseInt(measure.getValue());
  }

  @CheckForNull
  static WsMeasures.Measure getMeasure(@Nullable String componentKey, String metricKey) {
    WsMeasures.ComponentWsResponse response = newWsClient().measures().component(new ComponentWsRequest()
      .setComponentKey(componentKey)
      .setMetricKeys(Arrays.asList(metricKey)));
    List<WsMeasures.Measure> measures = response.getComponent().getMeasuresList();
    return measures.size() == 1 ? measures.get(0) : null;
  }

  static WsClient newWsClient() {
    return WsClientFactories.getDefault().newClient(HttpConnector.newBuilder()
      .url(ORCHESTRATOR.getServer().getUrl())
      .build());
  }
}
