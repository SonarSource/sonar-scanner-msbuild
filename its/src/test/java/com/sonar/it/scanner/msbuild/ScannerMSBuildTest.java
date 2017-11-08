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
import com.sonar.orchestrator.locator.MavenLocation;
import java.io.IOException;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.List;
import java.util.stream.Collectors;
import javax.annotation.CheckForNull;
import javax.annotation.Nullable;
import org.junit.Assume;
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
import static org.junit.Assert.assertTrue;

/**
 * csharpPlugin.version: csharp plugin to modify (installing scanner payload) and use. If not specified, uses 5.1. 
 * vbnetPlugin.version: vbnet plugin to use. It not specified, it fails
 * scannerForMSBuild.version: scanner to use. If not specified, uses the one built in ../
 * scannerForMSBuildPayload.version: scanner to embed in the csharp plugin. If not specified, uses the one built in ../
 * sonar.runtimeVersion: SQ to use
 */
public class ScannerMSBuildTest {
  // Should be the same version than in pom.xml
  static final String LICENSE_PLUGIN_VERSION = "3.1.0.1132";
  private static final String PROJECT_KEY = "my.project";
  private static final String MODULE_KEY = "my.project:my.project:1049030E-AC7A-49D0-BEDC-F414C5C7DDD8";
  private static final String FILE_KEY = MODULE_KEY + ":Foo.cs";

  @ClassRule
  public static Orchestrator ORCHESTRATOR = Orchestrator.builderEnv()
    .setOrchestratorProperty("csharpVersion", "LATEST_RELEASE")
    .addPlugin("csharp")
    .addPlugin(FileLocation.of(TestUtils.getCustomRoslynPlugin().toFile()))
    .setOrchestratorProperty("vbnetVersion", "LATEST_RELEASE")
    .addPlugin("vbnet")
    .addPlugin(MavenLocation.of("com.sonarsource.license", "sonar-dev-license-plugin", LICENSE_PLUGIN_VERSION))
    .activateLicense()
    .build();

  @ClassRule
  public static TemporaryFolder temp = new TemporaryFolder();

  @Before
  public void setUp() {
    ORCHESTRATOR.resetData();
  }

  @Test
  public void testSample() throws Exception {

    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("sample")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("end"));

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    assertThat(issues).hasSize(2);
    assertThat(getMeasureAsInteger(FILE_KEY, "ncloc")).isEqualTo(23);
    assertThat(getMeasureAsInteger(PROJECT_KEY, "ncloc")).isEqualTo(37);
    assertThat(getMeasureAsInteger(FILE_KEY, "lines")).isEqualTo(58);
  }

  @Test
  public void testHelpMessage() throws IOException {
    Assume.assumeTrue(TestUtils.getScannerVersion() == null);

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("/?"));

    assertThat(result.getLogs()).contains("Usage:");
    assertTrue(result.isSuccess());
  }

  @Test
  public void testNoProjectNameAndVersion() throws Exception {
    Assume.assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals("6.1"));

    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("end"));

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    assertThat(issues).hasSize(2);
    assertThat(getMeasureAsInteger(FILE_KEY, "ncloc")).isEqualTo(23);
    assertThat(getMeasureAsInteger(PROJECT_KEY, "ncloc")).isEqualTo(37);
    assertThat(getMeasureAsInteger(FILE_KEY, "lines")).isEqualTo(58);
  }

  @Test
  public void testExcludedAndTest() throws Exception {
    String normalProjectKey = "my.project:my.project:B93B287C-47DB-4406-9EAB-653BCF7D20DC";
    String testProjectKey = "my.project:my.project:2DC588FC-16FB-42F8-9FDA-193852E538AF";

    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "excludedAndTest");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(temp, "ExcludedTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("excludedAndTest")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("end"));

    // all issues and nloc are in the normal project
    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    assertThat(issues).hasSize(2);

    issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create().componentRoots(normalProjectKey)).list();
    assertThat(issues).hasSize(2);

    issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create().componentRoots(testProjectKey)).list();
    assertThat(issues).hasSize(0);

    // excluded project doesn't exist in SonarQube

    assertThat(getMeasureAsInteger(PROJECT_KEY, "ncloc")).isEqualTo(45);
    assertThat(getMeasureAsInteger(normalProjectKey, "ncloc")).isEqualTo(45);
  }

  @Test
  public void testMultiLanguage() throws Exception {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ConsoleMultiLanguage/TestQualityProfileCSharp.xml"));
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ConsoleMultiLanguage/TestQualityProfileVBNet.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "multilang");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTestCSharp");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "vbnet", "ProfileForTestVBNet");

    Path projectDir = TestUtils.projectDir(temp, "ConsoleMultiLanguage");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("multilang")
      .setProjectVersion("1.0")
      .setDebugLogs(true));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("end"));

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    // 2 CS, 2 vbnet
    assertThat(issues).hasSize(4);

    List<String> keys = issues.stream().map(i -> i.ruleKey()).collect(Collectors.toList());
    assertThat(keys).containsAll(Arrays.asList("vbnet:S3385",
      "vbnet:S2358",
      "csharpsquid:S2228",
      "csharpsquid:S1134"));

    // Program.cs 30
    // Properties/AssemblyInfo.cs 15
    // Ny Properties/AssemblyInfo.cs 13
    // Module1.vb 10
    assertThat(getMeasureAsInteger(PROJECT_KEY, "ncloc")).isEqualTo(68);
  }

  @Test
  public void testParameters() throws Exception {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfileParameters.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "parameters");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTestParameters");

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("parameters")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("end"));

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    assertThat(issues).hasSize(1);
    assertThat(issues.get(0).message()).isEqualTo("Method has 3 parameters, which is greater than the 2 authorized.");
    assertThat(issues.get(0).ruleKey()).isEqualTo("csharpsquid:S107");
  }

  @Test
  public void testVerbose() throws IOException {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "verbose");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("verbose")
      .setProjectVersion("1.0")
      .addArgument("/d:sonar.verbose=true"));

    assertThat(result.getLogs()).contains("Downloading from http://");
    assertThat(result.getLogs()).contains("sonar.verbose=true was specified - setting the log verbosity to 'Debug'");
  }

  @Test
  public void testHelp() throws IOException {

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("/?"));

    assertThat(result.getLogs()).contains("Usage:");
    assertThat(result.getLogs()).contains("SonarQube.Scanner.MSBuild.exe");
  }

  @Test
  public void testAllProjectsExcluded() throws Exception {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("sample")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild", "/p:ExcludeProjectsFromAnalysis=true");

    BuildResult result = ORCHESTRATOR.executeBuildQuietly(TestUtils.newScanner(projectDir)
      .addArgument("end"));

    assertThat(result.isSuccess()).isFalse();
    assertThat(result.getLogs()).contains("The exclude flag has been set so the project will not be analyzed by SonarQube.");
    assertThat(result.getLogs()).contains("No analysable projects were found. SonarQube analysis will not be performed. Check the build summary report for details.");
  }

  @Test
  public void testNoActiveRule() throws IOException {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestEmptyQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "empty");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "EmptyProfileForTest");

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("empty")
      .setProjectVersion("1.0")
      .addArgument("/d:sonar.verbose=true"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    BuildResult result = ORCHESTRATOR.executeBuildQuietly(TestUtils.newScanner(projectDir)
      .addArgument("end"));

    assertThat(result.isSuccess()).isTrue();
    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    assertThat(issues).isEmpty();
  }

  @Test
  public void excludeAssemblyAttribute() throws Exception {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(temp, "AssemblyAttribute");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("sample")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("end"));

    assertThat(result.getLogs()).doesNotContain("File is not under the project directory and cannot currently be analysed by SonarQube");
    assertThat(result.getLogs()).doesNotContain("AssemblyAttributes.cs");
  }

  @Test
  public void testCustomRoslynAnalyzer() throws Exception {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfileCustomRoslyn.xml"));
    ORCHESTRATOR.getServer().provisionProject("foo", "Foo");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile("foo", "cs", "ProfileForTestCustomRoslyn");

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("begin")
      .setProjectKey("foo")
      .setProjectName("Foo")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("end"));

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    assertThat(issues).hasSize(2 + 37 + 1);
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
