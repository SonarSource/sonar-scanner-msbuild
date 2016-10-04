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

import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.junit.SingleStartExternalResource;
import com.sonar.orchestrator.locator.FileLocation;
import java.io.IOException;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.List;
import java.util.stream.Collectors;

import org.junit.Before;
import org.junit.ClassRule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;
import org.sonar.wsclient.issue.Issue;
import org.sonar.wsclient.issue.IssueQuery;
import org.sonar.wsclient.services.Measure;
import org.sonar.wsclient.services.Resource;
import org.sonar.wsclient.services.ResourceQuery;

import static com.sonar.it.scanner.msbuild.TestSuite.*;
import static org.assertj.core.api.Assertions.assertThat;

public class ScannerMSBuildTest {
  private static final String PROJECT_KEY = "my.project";
  private static final String MODULE_KEY = "my.project:my.project:1049030E-AC7A-49D0-BEDC-F414C5C7DDD8";
  private static final String FILE_KEY = MODULE_KEY + ":Foo.cs";

  @ClassRule
  public static TemporaryFolder temp = TestSuite.temp;

  @ClassRule
  public static SingleStartExternalResource resource = TestSuite.resource;
  
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
    assertThat(issues).hasSize(4);
    assertThat(getFileMeasure("ncloc").getIntValue()).isEqualTo(23);
    assertThat(getProjectMeasure("ncloc").getIntValue()).isEqualTo(37);
    assertThat(getFileMeasure("lines").getIntValue()).isEqualTo(58);
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
    assertThat(issues).hasSize(4);
    
    issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create().componentRoots(normalProjectKey)).list();
    assertThat(issues).hasSize(4);
    
    issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create().componentRoots(testProjectKey)).list();
    assertThat(issues).hasSize(0);

    assertThat(getProjectMeasure("ncloc").getIntValue()).isEqualTo(45);
    assertThat(getProjectMeasure("ncloc", normalProjectKey).getIntValue()).isEqualTo(45);
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
    // 4 CS, 1 VBNET
    //assertThat(issues).hasSize(5);
    
    // FxCop rules do not show up for VB.NET because that version of the plugin no longer contains them (moved to FxCop plugin)
    List<String> keys = issues.stream().map(i -> i.ruleKey()).collect(Collectors.toList());
    assertThat(keys).containsAll(Arrays.asList("vbnet:S3385", 
      "fxcop:DoNotRaiseReservedExceptionTypes", 
      "fxcop:DoNotPassLiteralsAsLocalizedParameters", 
      "csharpsquid:S2228", 
      "csharpsquid:S1134"));
    
    assertThat(getProjectMeasure("ncloc").getIntValue()).isEqualTo(65);
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
  public void testFxCopCustom() throws Exception {
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
    assertThat(issues).hasSize(1);
    assertThat(issues.get(0).ruleKey()).isEqualTo("fxcop:customfxcop");
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

    assertThat(result.getLogs()).contains("Downloading from http://localhost");
    assertThat(result.getLogs()).contains("sonar.verbose=true was specified - setting the log verbosity to 'Debug'");
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

  private Measure getFileMeasure(String metricKey) {
    return getProjectMeasure(metricKey, FILE_KEY);
  }
  
  private Measure getProjectMeasure(String metricKey, String projectKey) {
    Resource resource = ORCHESTRATOR.getServer().getWsClient().find(ResourceQuery.createForMetrics(projectKey, metricKey));
    return resource != null ? resource.getMeasure(metricKey) : null;
  }

  private Measure getProjectMeasure(String metricKey) {
    return getProjectMeasure(metricKey, PROJECT_KEY);
  }

}
