/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

import com.sonar.it.scanner.msbuild.utils.AzureDevOpsUtils;
import com.sonar.it.scanner.msbuild.utils.EnvironmentVariable;
import com.sonar.it.scanner.msbuild.utils.ProxyAuthenticator;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.build.ScannerForMSBuild;
import com.sonar.orchestrator.http.HttpException;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import com.sonar.orchestrator.util.NetworkUtils;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.BufferedWriter;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.LinkOption;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.time.Duration;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.concurrent.ConcurrentLinkedDeque;
import java.util.function.Function;
import java.util.stream.Collectors;
import org.apache.commons.io.FileUtils;
import org.eclipse.jetty.client.api.Request;
import org.eclipse.jetty.proxy.ProxyServlet;
import org.eclipse.jetty.security.ConstraintMapping;
import org.eclipse.jetty.security.ConstraintSecurityHandler;
import org.eclipse.jetty.security.HashLoginService;
import org.eclipse.jetty.security.SecurityHandler;
import org.eclipse.jetty.security.UserStore;
import org.eclipse.jetty.server.Handler;
import org.eclipse.jetty.server.HttpConfiguration;
import org.eclipse.jetty.server.HttpConnectionFactory;
import org.eclipse.jetty.server.Server;
import org.eclipse.jetty.server.ServerConnector;
import org.eclipse.jetty.server.handler.DefaultHandler;
import org.eclipse.jetty.server.handler.HandlerCollection;
import org.eclipse.jetty.servlet.ServletContextHandler;
import org.eclipse.jetty.servlet.ServletHandler;
import org.eclipse.jetty.util.security.Constraint;
import org.eclipse.jetty.util.security.Credential;
import org.eclipse.jetty.util.thread.QueuedThreadPool;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.sonarqube.ws.Ce;
import org.sonarqube.ws.Components;
import org.sonarqube.ws.Issues;
import org.sonarqube.ws.Issues.Issue;
import org.sonarqube.ws.client.WsClient;
import org.sonarqube.ws.client.components.ShowRequest;

import static com.sonar.it.scanner.msbuild.sonarqube.Tests.ORCHESTRATOR;
import static com.sonar.it.scanner.msbuild.utils.AzureDevOpsUtils.getEnvBuildDirectory;
import static com.sonar.it.scanner.msbuild.utils.AzureDevOpsUtils.getSourcesDirectory;
import static com.sonar.it.scanner.msbuild.utils.AzureDevOpsUtils.isRunningUnderAzureDevOps;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;
import static org.awaitility.Awaitility.await;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assumptions.assumeFalse;
import static org.junit.jupiter.api.Assumptions.assumeTrue;

@ExtendWith(Tests.class)
class ScannerMSBuildTest {
  final static Logger LOG = LoggerFactory.getLogger(ScannerMSBuildTest.class);

  private static final String SONAR_RULES_PREFIX = "csharpsquid:";
  // note that in the UI the prefix will be 'roslyn:'
  private static final String ROSLYN_RULES_PREFIX = "external_roslyn:";
  private static final String PROJECT_KEY = "my.project";
  private static final String PROXY_USER = "scott";
  private static final String PROXY_PASSWORD = "tiger";
  private static Server server;
  private static int httpProxyPort;

  private static final ConcurrentLinkedDeque<String> seenByProxy = new ConcurrentLinkedDeque<>();

  @TempDir
  public Path basePath;

  @BeforeEach
  public void setUp() {
    TestUtils.reset(ORCHESTRATOR);
    seenByProxy.clear();
  }

  @AfterEach
  public void stopProxy() throws Exception {
    if (server != null && server.isStarted()) {
      server.stop();
    }
  }

  @Test
  void testSample() throws Exception {
    String localProjectKey = PROJECT_KEY + ".2";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(localProjectKey, "cs", "ProfileForTest");

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(localProjectKey)
      .setProjectName("sample")
      .setProperty("sonar.projectBaseDir", Paths.get(projectDir.toAbsolutePath().toString(), "ProjectUnderTest").toString())
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, localProjectKey, token);
    assertTrue(result.isSuccess());
    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(1);
    assertLineCountForProjectUnderTest(localProjectKey);
  }

  @Test
  void testSampleWithProxyAuth() throws Exception {
    startProxy(true);
    String localProjectKey = PROJECT_KEY + ".3";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(localProjectKey, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(localProjectKey)
      .setProjectName("sample")
      .setProperty("sonar.projectBaseDir", Paths.get(projectDir.toAbsolutePath().toString(), "ProjectUnderTest").toString())
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    BuildResult result = ORCHESTRATOR.executeBuildQuietly(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("end")
      .setEnvironmentVariable("SONAR_SCANNER_OPTS", "-Dhttp.nonProxyHosts= -Dhttp.proxyHost=localhost -Dhttp.proxyPort=" + httpProxyPort));

    assertThat(result.getLastStatus()).isNotZero();
    assertThat(result.getLogs()).contains("407");
    assertThat(seenByProxy).isEmpty();

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("end")
      .setEnvironmentVariable("SONAR_SCANNER_OPTS",
        "-Dhttp.nonProxyHosts= -Dhttp.proxyHost=localhost -Dhttp.proxyPort=" + httpProxyPort + " -Dhttp.proxyUser=" + PROXY_USER + " -Dhttp.proxyPassword=" + PROXY_PASSWORD));

    TestUtils.dumpComponentList(ORCHESTRATOR, localProjectKey);
    TestUtils.dumpAllIssues(ORCHESTRATOR);

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(1);
    assertLineCountForProjectUnderTest(localProjectKey);

    assertThat(seenByProxy).isNotEmpty();
  }

  @Test
  void testHelpMessage() throws IOException {
    assumeTrue(TestUtils.getScannerVersion(ORCHESTRATOR) == null);

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    BuildResult result = ORCHESTRATOR.executeBuild(ScannerForMSBuild.create(projectDir.toFile()).addArgument("/?"));

    assertThat(result.getLogs()).contains("Usage");
    assertTrue(result.isSuccess());
  }

  @Test
  void testNoProjectNameAndVersion() throws Exception {
    String localProjectKey = PROJECT_KEY + ".4";
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(6, 1));

    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(localProjectKey, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProperty("sonar.projectBaseDir", Paths.get(projectDir.toAbsolutePath().toString(), "ProjectUnderTest").toString())
      .setProjectKey(localProjectKey));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, localProjectKey, token);

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(1);
    assertLineCountForProjectUnderTest(localProjectKey);
  }

  private void assertLineCountForProjectUnderTest(String projectKey) {
    assertThat(TestUtils.getMeasureAsInteger(getFileKey(projectKey), "ncloc", ORCHESTRATOR)).isEqualTo(23);
    assertThat(TestUtils.getMeasureAsInteger(projectKey, "ncloc", ORCHESTRATOR)).isEqualTo(37);
    assertThat(TestUtils.getMeasureAsInteger(getFileKey(projectKey), "lines", ORCHESTRATOR)).isEqualTo(49);
  }

  @Test
  void testExcludedAndTest_AnalyzeTestProject() throws Exception {
    int expectedTestProjectIssues = isTestProjectSupported() ? 1 : 0;
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    Path projectDir = TestUtils.projectDir(basePath, "ExcludedTest");
    ScannerForMSBuild build = TestUtils.newScannerBegin(ORCHESTRATOR, "ExcludedTest_False", projectDir, token, ScannerClassifier.NET_FRAMEWORK)
      // don't exclude test projects
      .setProperty("sonar.dotnet.excludeTestProjects", "false");

    testExcludedAndTest(build, "ExcludedTest_False", projectDir, token, expectedTestProjectIssues);
  }

  @Test
  void testExcludedAndTest_ExcludeTestProject() throws Exception {
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    Path projectDir = TestUtils.projectDir(basePath, "ExcludedTest");
    ScannerForMSBuild build = TestUtils.newScannerBegin(ORCHESTRATOR, "ExcludedTest_True", projectDir, token, ScannerClassifier.NET_FRAMEWORK)
      // exclude test projects
      .setProperty("sonar.dotnet.excludeTestProjects", "true");

    testExcludedAndTest(build, "ExcludedTest_True", projectDir, token, 0);
  }

  @Test
  void testExcludedAndTest_simulateAzureDevopsEnvironmentSetting_ExcludeTestProject() throws Exception {
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    Path projectDir = TestUtils.projectDir(basePath, "ExcludedTest");
    EnvironmentVariable sonarQubeScannerParams = new EnvironmentVariable("SONARQUBE_SCANNER_PARAMS", "{\"sonar.dotnet.excludeTestProjects\":\"true\",\"sonar.verbose\":\"true\"}");
    ScannerForMSBuild build = TestUtils.newScannerBegin(ORCHESTRATOR, "ExcludedTest_True_FromAzureDevOps", projectDir, token, ScannerClassifier.NET_FRAMEWORK);

    testExcludedAndTest(build, "ExcludedTest_True_FromAzureDevOps", projectDir, token, 0, Collections.singletonList(sonarQubeScannerParams));
  }

  @Test
  void testExcludedAndTest_simulateAzureDevopsEnvironmentSettingMalformedJson_LogsWarning() throws Exception {
    String projectKeyName = "ExcludedTest_MalformedJson_FromAzureDevOps";
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    Path projectDir = TestUtils.projectDir(basePath, "ExcludedTest");
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKeyName, projectKeyName);
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKeyName, "cs", "ProfileForTest");

    ScannerForMSBuild beginStep = TestUtils.newScannerBegin(ORCHESTRATOR, projectKeyName, projectDir, token, ScannerClassifier.NET_FRAMEWORK);
    ORCHESTRATOR.executeBuild(beginStep);

    EnvironmentVariable sonarQubeScannerParams = new EnvironmentVariable("SONARQUBE_SCANNER_PARAMS", "{\"sonar.dotnet.excludeTestProjects\" }");
    BuildResult msBuildResult = TestUtils.runMSBuild(ORCHESTRATOR, projectDir, Collections.singletonList(sonarQubeScannerParams), 60 * 1000, "/t:Restore,Rebuild");

    assertThat(msBuildResult.isSuccess()).isTrue();
    assertThat(msBuildResult.getLogs()).contains("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS' because 'Invalid character after parsing property name. Expected ':' but got: }. Path '', line 1, position 36.'.");
  }

  @Test
  void testMultiLanguage() throws Exception {
    String localProjectKey = PROJECT_KEY + ".12";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ConsoleMultiLanguage/TestQualityProfileCSharp.xml"));
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ConsoleMultiLanguage/TestQualityProfileVBNet.xml"));
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, "multilang");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(localProjectKey, "cs", "ProfileForTestCSharp");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(localProjectKey, "vbnet", "ProfileForTestVBNet");

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    Path projectDir = TestUtils.projectDir(basePath, "ConsoleMultiLanguage");

    ORCHESTRATOR.executeBuild(TestUtils.newScannerBegin(ORCHESTRATOR, localProjectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK));
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");
    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, localProjectKey, token);

    assertTrue(result.isSuccess());

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    // 1 CS, 2 vbnet
    assertThat(issues).hasSize(3);

    List<String> ruleKeys = issues.stream().map(Issue::getRule).collect(Collectors.toList());
    assertThat(ruleKeys).containsAll(Arrays.asList("vbnet:S3385",
      "vbnet:S2358",
      SONAR_RULES_PREFIX + "S1134"));

    // Program.cs 30
    // Properties/AssemblyInfo.cs 15
    // Ny Properties/AssemblyInfo.cs 13
    // Module1.vb 10
    assertThat(TestUtils.getMeasureAsInteger(localProjectKey, "ncloc", ORCHESTRATOR)).isEqualTo(68);
  }

  @Test
  void checkExternalIssuesVB() throws Exception {
    String localProjectKey = PROJECT_KEY + ".6";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ExternalIssues.VB/TestQualityProfileExternalIssuesVB.xml"));
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(localProjectKey, "vbnet", "ProfileForTestExternalIssuesVB");

    Path projectDir = TestUtils.projectDir(basePath, "ExternalIssues.VB");
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    ORCHESTRATOR.executeBuild(TestUtils.newScannerBegin(ORCHESTRATOR, localProjectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK));
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");
    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, localProjectKey, token);

    assertTrue(result.isSuccess());
    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    List<String> ruleKeys = issues.stream().map(Issue::getRule).collect(Collectors.toList());

    // The same set of Sonar issues should be reported, regardless of whether
    // external issues are imported or not
    assertThat(ruleKeys).containsAll(Arrays.asList(
      "vbnet:S112",
      "vbnet:S3385"));

    if (ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(7, 4)) {
      // if external issues are imported, then there should also be some CodeCracker errors.
      assertThat(ruleKeys).containsAll(Arrays.asList(
        ROSLYN_RULES_PREFIX + "CC0021",
        ROSLYN_RULES_PREFIX + "CC0062"));

      assertThat(issues).hasSize(4);

    } else {
      // Not expecting any external issues
      assertThat(issues).hasSize(2);
    }
  }

  @Test
  void testParameters() throws Exception {
    String localProjectKey = PROJECT_KEY + ".7";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfileParameters.xml"));
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, "parameters");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(localProjectKey, "cs", "ProfileForTestParameters");

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(localProjectKey)
      .setProjectName("parameters")
      .setProperty("sonar.projectBaseDir", Paths.get(projectDir.toAbsolutePath().toString(), "ProjectUnderTest").toString())
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, localProjectKey, token);
    assertTrue(result.isSuccess());

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(1);
    assertThat(issues.get(0).getMessage()).isEqualTo("Method has 3 parameters, which is greater than the 2 authorized.");
    assertThat(issues.get(0).getRule()).isEqualTo(SONAR_RULES_PREFIX + "S107");
  }

  @Test
  void testVerbose() throws IOException {
    String localProjectKey = PROJECT_KEY + ".10";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, "verbose");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(localProjectKey, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(localProjectKey)
      .setProjectName("verbose")
      .setProjectVersion("1.0")
      .setProperty("sonar.projectBaseDir", Paths.get(projectDir.toAbsolutePath().toString(), "ProjectUnderTest").toString())
      .setProperty("sonar.verbose", "true"));

    assertThat(result.getLogs()).contains("Downloading from http://");
    assertThat(result.getLogs()).contains("sonar.verbose=true was specified - setting the log verbosity to 'Debug'");
  }

  @Test
  void testHelp() throws IOException {
    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    BuildResult result = ORCHESTRATOR.executeBuild(ScannerForMSBuild.create(projectDir.toFile()).addArgument("/?"));

    assertThat(result.getLogs()).contains("Usage");
    assertThat(result.getLogs()).contains("SonarScanner.MSBuild.exe");
  }

  @Test
  void testAllProjectsExcluded() throws Exception {
    String localProjectKey = PROJECT_KEY + ".9";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(localProjectKey, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    ORCHESTRATOR.executeBuild(TestUtils.newScannerBegin(ORCHESTRATOR, localProjectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK));
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild", "/p:ExcludeProjectsFromAnalysis=true");
    BuildResult result = ORCHESTRATOR.executeBuildQuietly(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("end"));

    assertThat(result.isSuccess()).isFalse();
    assertThat(result.getLogs()).contains("The exclude flag has been set so the project will not be analyzed.");
    assertThat(result.getLogs()).contains("No analysable projects were found. SonarQube analysis will not be performed. Check the build summary report for details.");
  }

  @Test
  void testNoActiveRule() throws IOException {
    String localProjectKey = PROJECT_KEY + ".8";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestEmptyQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, "empty");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(localProjectKey, "cs", "EmptyProfileForTest");

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    ORCHESTRATOR.executeBuild(TestUtils.newScannerBegin(ORCHESTRATOR, localProjectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK));
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");
    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, localProjectKey, token);

    assertThat(result.isSuccess()).isTrue();

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).isEmpty();
  }

  @Test
  void excludeAssemblyAttribute() throws Exception {
    String localProjectKey = PROJECT_KEY + ".5";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(localProjectKey, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(basePath, "AssemblyAttribute");
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    ORCHESTRATOR.executeBuild(TestUtils.newScannerBegin(ORCHESTRATOR, localProjectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK));
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");
    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, localProjectKey, token);

    assertThat(result.getLogs()).doesNotContain("File is not under the project directory and cannot currently be analysed by SonarQube");
    assertThat(result.getLogs()).doesNotContain("AssemblyAttributes.cs");
  }

  @Test
  void checkExternalIssuesCS() throws Exception {
    String localProjectKey = PROJECT_KEY + ".ExternalIssuesCS";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ExternalIssues.CS/TestQualityProfileExternalIssues.xml"));
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(localProjectKey, "cs", "ProfileForTestExternalIssues");

    Path projectDir = TestUtils.projectDir(basePath, "ExternalIssues.CS");
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    ORCHESTRATOR.executeBuild(TestUtils.newScannerBegin(ORCHESTRATOR, localProjectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK));
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");
    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, localProjectKey, token);

    assertTrue(result.isSuccess());

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    List<String> ruleKeys = issues.stream().map(Issue::getRule).collect(Collectors.toList());

    // The same set of Sonar issues should be reported, regardless of whether
    // external issues are imported or not
    assertThat(ruleKeys).containsAll(Arrays.asList(
      SONAR_RULES_PREFIX + "S125",
      SONAR_RULES_PREFIX + "S1134"));

    if (ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(7, 4)) {
      // if external issues are imported, then there should also be some
      // Wintellect errors.  However, only file-level issues are imported.
      assertThat(ruleKeys).containsAll(List.of(
        ROSLYN_RULES_PREFIX + "Wintellect004"));

      assertThat(issues).hasSize(3);

    } else {
      // Not expecting any external issues
      assertThat(issues).hasSize(2);
    }
  }

  @Test
  void testXamlCompilation() throws IOException {
    String localProjectKey = PROJECT_KEY + ".11";
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, "Xamarin");

    BuildResult result = runBeginBuildAndEndForStandardProject("XamarinApplication", "", true, true);
    assertTrue(result.isSuccess());

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(filter(issues, SONAR_RULES_PREFIX))
      .hasSize(8)
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(
        tuple(SONAR_RULES_PREFIX + "S927", "XamarinApplication:XamarinApplication.iOS/AppDelegate.cs"),
        tuple(SONAR_RULES_PREFIX + "S927", "XamarinApplication:XamarinApplication.iOS/AppDelegate.cs"),
        tuple(SONAR_RULES_PREFIX + "S1118", "XamarinApplication:XamarinApplication.iOS/Main.cs"),
        tuple(SONAR_RULES_PREFIX + "S1186", "XamarinApplication:XamarinApplication.iOS/Main.cs"),
        tuple(SONAR_RULES_PREFIX + "S1186", "XamarinApplication:XamarinApplication/App.xaml.cs"),
        tuple(SONAR_RULES_PREFIX + "S1186", "XamarinApplication:XamarinApplication/App.xaml.cs"),
        tuple(SONAR_RULES_PREFIX + "S1186", "XamarinApplication:XamarinApplication/App.xaml.cs"),
        tuple(SONAR_RULES_PREFIX + "S1134", "XamarinApplication:XamarinApplication/MainPage.xaml.cs"));

    assertThat(TestUtils.getMeasureAsInteger("XamarinApplication", "lines", ORCHESTRATOR)).isEqualTo(149);
    assertThat(TestUtils.getMeasureAsInteger("XamarinApplication", "ncloc", ORCHESTRATOR)).isEqualTo(93);
    assertThat(TestUtils.getMeasureAsInteger("XamarinApplication", "files", ORCHESTRATOR)).isEqualTo(6);
    assertThat(TestUtils.getMeasureAsInteger("XamarinApplication:XamarinApplication.iOS", "lines", ORCHESTRATOR)).isEqualTo(97);
    assertThat(TestUtils.getMeasureAsInteger("XamarinApplication:XamarinApplication", "lines", ORCHESTRATOR)).isEqualTo(52);
  }

  @Test
  void testRazorCompilationNet2() throws IOException {
    validateRazorProject("RazorWebApplication.net2.1");
  }

  @Test
  void testRazorCompilationNet3() throws IOException {
    validateRazorProject("RazorWebApplication.net3.1");
  }

  @Test
  void testRazorCompilationNet5() throws IOException {
    validateRazorProject("RazorWebApplication.net5");
  }

  @Test
  void testRazorCompilationNet6WithoutSourceGenerators() throws IOException {
    assumeTrue(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022")); // We can't build without MsBuild17
    String projectName = "RazorWebApplication.net6.withoutSourceGenerators";
    assertProjectFileContains(projectName, "<UseRazorSourceGenerator>false</UseRazorSourceGenerator>");
    validateRazorProject(projectName);
  }

  @Test
  void testRazorCompilationNet6WithSourceGenerators() throws IOException {
    assumeTrue(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022")); // We can't build without MsBuild17
    String projectName = "RazorWebApplication.net6.withSourceGenerators";
    assertProjectFileContains(projectName, "<UseRazorSourceGenerator>true</UseRazorSourceGenerator>");
    validateRazorProject(projectName);
  }

  @Test
  void testRazorCompilationNet7WithSourceGenerators() throws IOException {
    // This is not supported for versions older than VS 2022
    assumeTrue(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022"));
    String projectName = "RazorWebApplication.net7.withSourceGenerators";
    assertProjectFileContains(projectName, "<UseRazorSourceGenerator>true</UseRazorSourceGenerator>");
    validateRazorProject(projectName);
  }

  @Test
  void testRazorCompilationNet7WithoutSourceGenerators() throws IOException {
    // This is not supported for versions older than VS 2022
    assumeTrue(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022"));
    String projectName = "RazorWebApplication.net7.withoutSourceGenerators";
    assertProjectFileContains(projectName, "<UseRazorSourceGenerator>false</UseRazorSourceGenerator>");
    validateRazorProject(projectName);
  }

  @Test
  void testEsprojVueWithBackend() throws IOException {
    // For this test also the .vscode folder has been included in the project folder:
    // https://developercommunity.visualstudio.com/t/visual-studio-2022-freezes-when-opening-esproj-fil/1581344
    String localProjectKey = PROJECT_KEY + ".14";
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, "VueWithAspBackend");

    if (!TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022")) {
      return; // This test is not supported on versions older than Visual Studio 22
    }

    Path projectDir = TestUtils.projectDir(basePath, "VueWithAspBackend");
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    ORCHESTRATOR.executeBuild(
      TestUtils.newScannerBegin(ORCHESTRATOR, localProjectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK));

    TestUtils.runNuGet(ORCHESTRATOR, projectDir, true, "restore");
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, Collections.emptyList(), 180 * 1000, "/t:Rebuild", "/nr:false");

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, localProjectKey, token);
    assertTrue(result.isSuccess());

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    List<String> ruleKeys = issues.stream().map(Issue::getRule).collect(Collectors.toList());
    assertThat(ruleKeys).hasSize(5);
    assertThat(ruleKeys).containsExactlyInAnyOrder(
      SONAR_RULES_PREFIX + "S4487",
      SONAR_RULES_PREFIX + "S1134",
      "javascript:S2703",
      "javascript:S2703",
      "typescript:S3626");

    assertThat(TestUtils.getMeasureAsInteger(localProjectKey, "lines", ORCHESTRATOR)).isEqualTo(270);
    assertThat(TestUtils.getMeasureAsInteger(localProjectKey, "ncloc", ORCHESTRATOR)).isEqualTo(170);
    assertThat(TestUtils.getMeasureAsInteger(localProjectKey, "files", ORCHESTRATOR)).isEqualTo(9);
  }

  @Test
  void testCustomRoslynAnalyzer() throws Exception {
    String folderName = "ProjectUnderTest";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/" + folderName + "/TestQualityProfileCustomRoslyn.xml"));
    ORCHESTRATOR.getServer().provisionProject(folderName, folderName);
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(folderName, "cs", "ProfileForTestCustomRoslyn");

    runBeginBuildAndEndForStandardProject(folderName, "", true, false);

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(1 + 37 + 1);
  }

  @Test
  void testCSharpAllFlat() throws IOException {
    runBeginBuildAndEndForStandardProject("CSharpAllFlat", "");

    assertThat(getComponent("CSharpAllFlat:Common.cs")).isNotNull();
  }

  @Test
  void testTargetUninstall() throws IOException {
    Path projectDir = TestUtils.projectDir(basePath, "CSharpAllFlat");
    runBeginBuildAndEndForStandardProject(projectDir, "", true, false);
    // Run the build for a second time - should not fail after uninstalling targets
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild", "CSharpAllFlat.sln");

    assertThat(getComponent("CSharpAllFlat:Common.cs")).isNotNull();
  }

  @Test
  void testCSharpSharedFiles() throws IOException {
    runBeginBuildAndEndForStandardProject("CSharpSharedFiles", "");

    assertThat(getComponent("CSharpSharedFiles:Common.cs"))
      .isNotNull();
    String class1ComponentId = TestUtils.hasModules(ORCHESTRATOR) ? "CSharpSharedFiles:CSharpSharedFiles:D8FEDBA2-D056-42FB-B146-5A409727B65D:Class1.cs" : "CSharpSharedFiles:ClassLib1/Class1.cs";
    assertThat(getComponent(class1ComponentId))
      .isNotNull();
    String class2ComponentId = TestUtils.hasModules(ORCHESTRATOR) ? "CSharpSharedFiles:CSharpSharedFiles:72CD6ED2-481A-4828-BA15-8CD5F0472A77:Class2.cs" : "CSharpSharedFiles:ClassLib2/Class2.cs";
    assertThat(getComponent(class2ComponentId))
      .isNotNull();
  }

  @Test
  void testCSharpSharedProjectType() throws IOException {
    runBeginBuildAndEndForStandardProject("CSharpSharedProjectType", "");

    assertThat(getComponent("CSharpSharedProjectType:SharedProject/TestEventInvoke.cs"))
      .isNotNull();
    String programComponentId1 = TestUtils.hasModules(ORCHESTRATOR) ? "CSharpSharedProjectType:CSharpSharedProjectType:36F96F66-8136-46C0-B83B-EFAE05A8FFC1:Program.cs" : "CSharpSharedProjectType:ConsoleApp1/Program.cs";
    assertThat(getComponent(programComponentId1))
      .isNotNull();
    String programComponentId2 = TestUtils.hasModules(ORCHESTRATOR) ? "CSharpSharedProjectType:CSharpSharedProjectType:F96D8AA1-BCE1-4655-8D65-08F2A5FAC15B:Program.cs" : "CSharpSharedProjectType:ConsoleApp2/Program.cs";
    assertThat(getComponent(programComponentId2))
      .isNotNull();
  }

  @Test
  void testCSharpSharedFileWithOneProjectWithoutProjectBaseDir() throws IOException {
    runBeginBuildAndEndForStandardProject("CSharpSharedFileWithOneProject", "ClassLib1");

    try {
      Components.ShowWsResponse showComponentResponse = newWsClient()
        .components()
        .show(new ShowRequest().setComponent("CSharpSharedFileWithOneProject:Common.cs"));
    } catch (org.sonarqube.ws.client.HttpException ex) {
      assertThat(ex.code()).isEqualTo(404);
    }

    // When not using /d:sonar.projectBaseDir the root dir will be set at the level of the project so that the
    // file Common.cs will be outside of the scope and won't be pushed to SQ

    Components.ShowWsResponse showComponentResponse2 = newWsClient()
      .components()
      .show(new ShowRequest().setComponent("CSharpSharedFileWithOneProject:Class1.cs"));

    assertThat(showComponentResponse2.hasComponent()).isTrue();
  }

  @Test
  void testCSharpSharedFileWithOneProjectUsingProjectBaseDirAbsolute() throws IOException {
    runCSharpSharedFileWithOneProjectUsingProjectBaseDir(
      projectDir -> {
        try {
          return projectDir.toRealPath(LinkOption.NOFOLLOW_LINKS).toString();
        } catch (IOException e) {
          e.printStackTrace();
        }
        return null;
      });
  }

  @Test
  void testCSharpFramework48() throws IOException {
    validateCSharpFramework("CSharp.Framework.4.8");
  }

  @Test
  void testCSharpSdk2() throws IOException {
    validateCSharpSdk("CSharp.SDK.2.1");
  }

  @Test
  void testCSharpSdk3() throws IOException {
    validateCSharpSdk("CSharp.SDK.3.1");
  }

  @Test
  void testScannerNetCore31NoAnalysisWarning() throws IOException {
    assumeFalse(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017")); // We can't run .NET Core SDK under VS 2017 CI context
    Path projectDir = TestUtils.projectDir(basePath, "CSharp.SDK.3.1");
    BuildResult buildResult = runNetCoreBeginBuildAndEnd(projectDir, ScannerClassifier.NET);

    assertThat(buildResult.getLogs()).doesNotContain("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS'");
    assertNoAnalysisWarnings(buildResult);
  }

  @Test
  void testCSharpSdk5() throws IOException {
    validateCSharpSdk("CSharp.SDK.5");
  }

  @Test
  void testScannerNet5NoAnalysisWarnings() throws IOException {
    assumeFalse(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017")); // We can't run .NET Core SDK under VS 2017 CI context
    Path projectDir = TestUtils.projectDir(basePath, "CSharp.SDK.5");
    BuildResult buildResult = runNetCoreBeginBuildAndEnd(projectDir, ScannerClassifier.NET);

    assertThat(buildResult.getLogs()).doesNotContain("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS'");
    assertNoAnalysisWarnings(buildResult);
  }

  @Test
  void testCSharpSdk7() throws IOException {
    if (!TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022")) {
      return; // This test is not supported on versions older than Visual Studio 22
    }
    validateCSharpSdk("CSharp.SDK.7.0");
  }

  @Test
  void testScannerNet7NoAnalysisWarnings() throws IOException {
    if (!TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022")) {
      return; // This test is not supported on versions older than Visual Studio 22
    }

    BuildResult buildResult = runBeginBuildAndEndForStandardProject("CSharp.SDK.7.0", "");

    assertThat(buildResult.getLogs()).doesNotContain("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS'");
    assertNoAnalysisWarnings(buildResult);
  }

  @Test
  void testCSharpSdkLatest() throws IOException {
    // CSharp.SDK.Latest targets .NET6, so this test cannot run for VS older than 2022.
    assumeTrue(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022"));
    validateCSharpSdk("CSharp.SDK.Latest");
  }

  /* TODO: This test doesn't work as expected. Relative path will create sub-folders on SonarQube and so files are not
           located where you expect them.
  @Test
  public void testCSharpSharedFileWithOneProjectUsingProjectBaseDirRelative() throws IOException {
    runCSharpSharedFileWithOneProjectUsingProjectBaseDir(projectDir -> "..\\..");
  } */

  @Test
  void testCSharpSharedFileWithOneProjectUsingProjectBaseDirAbsoluteShort() throws IOException {
    runCSharpSharedFileWithOneProjectUsingProjectBaseDir(Path::toString);
  }

  @Test
  void testProjectTypeDetectionWithWrongCasingReferenceName() throws IOException {
    BuildResult buildResult = runBeginBuildAndEndForStandardProject("DotnetProjectTypeDetection", "TestProjectWrongReferenceCasing");
    assertThat(buildResult.getLogs()).contains("Found 1 MSBuild C# project: 1 TEST project.");
  }

  @Test
  void testDuplicateAnalyzersWithSameNameAreNotRemoved() throws IOException {
    assumeTrue(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022")); // We can't build without MsBuild17
    Path projectDir = TestUtils.projectDir(basePath, "DuplicateAnalyzerReferences");
    BuildResult buildResult = runNetCoreBeginBuildAndEnd(projectDir, ScannerClassifier.NET);

    assertThat(buildResult.getLogs()).doesNotContain("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS'");

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(3)
      .extracting(Issue::getRule)
      .containsExactlyInAnyOrder(
        SONAR_RULES_PREFIX + "S1481", // Program.cs line 7
        SONAR_RULES_PREFIX + "S1186", // Program.cs line 10
        SONAR_RULES_PREFIX + "S1481"); // Generator.cs line 18

    assertThat(TestUtils.getMeasureAsInteger("DuplicateAnalyzerReferences", "lines", ORCHESTRATOR)).isEqualTo(40);
    assertThat(TestUtils.getMeasureAsInteger("DuplicateAnalyzerReferences", "ncloc", ORCHESTRATOR)).isEqualTo(30);
    assertThat(TestUtils.getMeasureAsInteger("DuplicateAnalyzerReferences", "files", ORCHESTRATOR)).isEqualTo(2);
  }

  @Test
  void testIgnoreIssuesDoesNotRemoveSourceGenerator() throws IOException {
    assumeFalse(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017")); // We can't run .NET Core SDK under VS 2017 CI context
    Path projectDir = TestUtils.projectDir(basePath, "IgnoreIssuesDoesNotRemoveSourceGenerator");

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    ScannerForMSBuild scanner = TestUtils.newScannerBegin(ORCHESTRATOR, "IgnoreIssuesDoesNotRemoveSourceGenerator", projectDir, token, ScannerClassifier.NET_FRAMEWORK)
      .setProperty("sonar.cs.roslyn.ignoreIssues", "true");

    ORCHESTRATOR.executeBuild(scanner);

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Restore,Rebuild");

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, "IgnoreIssuesDoesNotRemoveSourceGenerator", token);

    assertTrue(result.isSuccess());
    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(filter(issues, SONAR_RULES_PREFIX)).hasSize(2);
    assertThat(filter(issues, ROSLYN_RULES_PREFIX)).isEmpty();
  }

  @Test
  void whenEachProjectIsOnDifferentDrives_AnalysisFails() throws IOException {
    try {
      Path projectDir = TestUtils.projectDir(basePath, "TwoDrivesTwoProjects");
      TestUtils.createVirtualDrive("Z:", projectDir, "DriveZ");

      BuildResult buildResult = runAnalysisWithoutProjectBasedDir(projectDir);

      assertThat(buildResult.isSuccess()).isFalse();
      assertThat(buildResult.getLogs()).contains("Generation of the sonar-properties file failed. Unable to complete the analysis.");
    } finally {
      TestUtils.deleteVirtualDrive("Z:");
    }
  }

  @Test
  void whenMajorityOfProjectsIsOnSameDrive_AnalysisSucceeds() throws IOException {
    try {
      Path projectDir = TestUtils.projectDir(basePath, "TwoDrivesThreeProjects");
      TestUtils.createVirtualDrive("Y:", projectDir, "DriveY");

      BuildResult buildResult = runAnalysisWithoutProjectBasedDir(projectDir);
      assertThat(buildResult.isSuccess()).isTrue();
      assertThat(buildResult.getLogs()).contains("Using longest common projects path as a base directory: '" + projectDir);
      assertThat(buildResult.getLogs()).contains("WARNING: Directory 'Y:\\Subfolder' is not located under the base directory '" + projectDir + "' and will not be analyzed.");
      assertThat(buildResult.getLogs()).contains("WARNING: File 'Y:\\Subfolder\\Program.cs' is not located under the base directory '" + projectDir + "' and will not be analyzed.");
      assertThat(buildResult.getLogs()).contains("File was referenced by the following projects: 'Y:\\Subfolder\\DriveY.csproj'.");
      assertThat(TestUtils.allIssues(ORCHESTRATOR)).hasSize(2)
        .extracting(Issues.Issue::getRule, Issues.Issue::getComponent)
        .containsExactlyInAnyOrder(
          tuple("vbnet:S6145", "TwoDrivesThreeProjects"),
          tuple(SONAR_RULES_PREFIX + "S1134", "TwoDrivesThreeProjects:DefaultDrive/Program.cs")
        );
    } finally {
      TestUtils.deleteVirtualDrive("Y:");
    }
  }

  @Test
  void testAzureFunctions_WithWrongBaseDirectory_AnalysisSucceeds() throws IOException {
    // If the test is being run under Azure DevOps then the Scanner will
    // expect the project to be under the Azure DevOps sources directory
    if (isRunningUnderAzureDevOps()) {
      String sourcesDirectory = getSourcesDirectory();
      LOG.info("TEST SETUP: Tests are running under Azure DevOps. Build dir:  " + sourcesDirectory);
      basePath = Path.of(sourcesDirectory);
    } else {
      LOG.info("TEST SETUP: Tests are not running under Azure DevOps");
    }

    Path projectDir = TestUtils.projectDir(basePath, "ReproAzureFunctions");
    BuildResult buildResult = runAnalysisWithoutProjectBasedDir(projectDir);

    assertThat(buildResult.isSuccess()).isTrue();
     var temporaryFolderRoot = basePath.getParent().toFile().getCanonicalFile().toString();
     assertThat(buildResult.getLogs()).contains(" '" + temporaryFolderRoot);
  }

  @Test
  void incrementalPrAnalysis_NoCache() throws IOException {
    String projectKey = "incremental-pr-analysis-no-cache";
    Path projectDir = TestUtils.projectDir(basePath, "IncrementalPRAnalysis");
    File unexpectedUnchangedFiles = new File(projectDir.resolve(".sonarqube\\conf\\UnchangedFiles.txt").toString());
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(projectKey)
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setDebugLogs(true) // To assert debug logs too
      .setProperty("sonar.pullrequest.base", "base-branch"));

    assertTrue(result.isSuccess());
    assertThat(unexpectedUnchangedFiles).doesNotExist();
    assertThat(result.getLogs()).contains("Processing analysis cache");

    if (ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(9, 9)) {
      assertThat(result.getLogs()).contains("Cache data is empty. A full analysis will be performed.");
    } else {
      assertThat(result.getLogs()).contains("Incremental PR analysis is available starting with SonarQube 9.9 or later.");
    }
  }

  @Test
  void incrementalPrAnalysis_ProducesUnchangedFiles() throws IOException {
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(9, 9)); // Public cache API was introduced in 9.9

    String projectKey = "IncrementalPRAnalysis";
    String baseBranch = TestUtils.getDefaultBranchName(ORCHESTRATOR);
    Path projectDir = TestUtils.projectDir(basePath, projectKey);

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(projectKey)
      .setProjectName(projectKey)
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Restore,Rebuild");

    BuildResult firstAnalysisResult = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);
    assertTrue(firstAnalysisResult.isSuccess());

    waitForCacheInitialization(projectKey, baseBranch);

    File fileToBeChanged = projectDir.resolve("IncrementalPRAnalysis\\WithChanges.cs").toFile();
    BufferedWriter writer = new BufferedWriter(new FileWriter(fileToBeChanged, true));
    writer.append(' ');
    writer.close();

    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(projectKey)
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setDebugLogs(true) // To assert debug logs too
      .setProperty("sonar.pullrequest.base", baseBranch));

    assertTrue(result.isSuccess());
    assertThat(result.getLogs()).contains("Processing analysis cache");
    assertThat(result.getLogs()).contains("Downloading cache. Project key: IncrementalPRAnalysis, branch: " + baseBranch + ".");

    Path buildDirectory = isRunningUnderAzureDevOps() ? Path.of(getEnvBuildDirectory()) : projectDir;
    Path expectedUnchangedFiles = buildDirectory.resolve(".sonarqube\\conf\\UnchangedFiles.txt");

    LOG.info("UnchangedFiles: " + expectedUnchangedFiles.toAbsolutePath());

    assertThat(expectedUnchangedFiles).exists();
    assertThat(Files.readString(expectedUnchangedFiles))
      .contains("Unchanged1.cs")
      .contains("Unchanged2.cs")
      .doesNotContain("WithChanges.cs"); // Was modified
  }

  @Test
  void checkMultiLanguageSupportWithSdkFormat() throws Exception {
    assumeFalse(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017")); // new SDK-style format was introduced with .NET Core, we can't run .NET Core SDK under VS 2017 CI context
    Path projectDir = TestUtils.projectDir(basePath, "MultiLanguageSupport");
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    String folderName = projectDir.getFileName().toString();
    // Begin step in MultiLanguageSupport folder
    ScannerForMSBuild scanner = TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(folderName)
      .setProjectName(folderName)
      .setProjectVersion("1.0")
      .setProperty("sonar.sourceEncoding", "UTF-8")
      // Overriding environment variables to fallback to projectBaseDir detection
      .setEnvironmentVariable("TF_BUILD_SOURCESDIRECTORY", "")
      .setEnvironmentVariable("TF_BUILD_BUILDDIRECTORY", "")
      .setEnvironmentVariable("BUILD_SOURCESDIRECTORY", "");
    ORCHESTRATOR.executeBuild(scanner);
    // Build solution inside MultiLanguageSupport/src folder
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Restore,Rebuild", "src/MultiLanguageSupport.sln");
    // End step in MultiLanguageSupport folder
    BuildResult result =  TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, folderName, token);
    assertTrue(result.isSuccess());

    // Files within the 'frontend' folder are not included in sonar-project.properties source/test
    // Outside.js, Outside.sql are not detected: projectBaseDir is at .csproj level
    // Excluded.js, Excluded.sql, Excluded.cs are excluded from the .csproj with the Remove attribute
    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(10)
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(
        // "src/MultiLanguageSupport" directory
        tuple("csharpsquid:S1134", "MultiLanguageSupport:src/MultiLanguageSupport/Program.cs"),
        tuple("javascript:S1529", "MultiLanguageSupport:src/MultiLanguageSupport/Excluded.js"),
        tuple("javascript:S1529", "MultiLanguageSupport:src/MultiLanguageSupport/JavaScript.js"),
        tuple("plsql:S1134", "MultiLanguageSupport:src/MultiLanguageSupport/Excluded.sql"),
        tuple("plsql:S1134", "MultiLanguageSupport:src/MultiLanguageSupport/plsql.sql"),
        // "src/" directory
        tuple("plsql:S1134", "MultiLanguageSupport:src/Outside.sql"),
        tuple("javascript:S1529", "MultiLanguageSupport:src/Outside.js"),
        // "frontend/" directory
        tuple("javascript:S1529", "MultiLanguageSupport:frontend/PageOne.js"),
        tuple("typescript:S1128", "MultiLanguageSupport:frontend/PageTwo.tsx"),
        tuple("plsql:S1134", "MultiLanguageSupport:frontend/PageOne.Query.sql"));
  }

  @Test
  void checkMultiLanguageSupportWithNonSdkFormat() throws Exception {
    BuildResult result = runBeginBuildAndEndForStandardProject("MultiLanguageSupportNonSdk", "");
    assertTrue(result.isSuccess());

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(5)
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(
        tuple("csharpsquid:S2094", "MultiLanguageSupportNonSdk:MultiLanguageSupportNonSdk/Foo.cs"),
        tuple("javascript:S1529", "MultiLanguageSupportNonSdk:MultiLanguageSupportNonSdk/Included.js"),
        tuple("javascript:S1529", "MultiLanguageSupportNonSdk:MultiLanguageSupportNonSdk/Excluded.js"),
        tuple("plsql:S1134", "MultiLanguageSupportNonSdk:MultiLanguageSupportNonSdk/Included.sql"),
        tuple("plsql:S1134", "MultiLanguageSupportNonSdk:MultiLanguageSupportNonSdk/Excluded.sql"));
  }

  private void waitForCacheInitialization(String projectKey, String baseBranch) {
    await()
      .pollInterval(Duration.ofSeconds(1))
      .atMost(Duration.ofSeconds(120))
      .until(() -> {
        try {
          ORCHESTRATOR.getServer().newHttpCall("api/analysis_cache/get").setParam("project", projectKey).setParam("branch", baseBranch).setAuthenticationToken(ORCHESTRATOR.getDefaultAdminToken()).execute();
          return true;
        } catch (HttpException ex) {
          return false; // if the `execute()` method is not successful it throws HttpException
        }
      });
  }

  private void validateCSharpSdk(String folderName) throws IOException {
    assumeFalse(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017")); // We can't run .NET Core SDK under VS 2017 CI context
    runBeginBuildAndEndForStandardProject(folderName, "", true, false);

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    if (isTestProjectSupported()) {
      assertThat(issues).hasSize(3)
        .extracting(Issue::getRule, Issue::getComponent)
        .containsExactlyInAnyOrder(
          tuple(SONAR_RULES_PREFIX + "S1134", folderName + ":Main/Common.cs"),
          tuple(SONAR_RULES_PREFIX + "S2699", folderName + ":UTs/CommonTest.cs"),
          tuple(SONAR_RULES_PREFIX + "S2094", folderName + ":Main/Common.cs"));
      // The AspNetCoreMvc/Views/Home/Index.cshtml contains an external CS0219 issue
      // which is currently not imported due to the fact that the generated code Index.cshtml.g.cs is in the object folder.
    } else {
      assertThat(issues).hasSize(3)
        .extracting(Issue::getRule, Issue::getComponent)
        .containsExactlyInAnyOrder(
          tuple(SONAR_RULES_PREFIX + "S1134", folderName + ":AspNetCoreMvc/Program.cs"),
          tuple(SONAR_RULES_PREFIX + "S1134", folderName + ":Main/Common.cs"),
          tuple(SONAR_RULES_PREFIX + "S2094", folderName + ":Main/Common.cs"));
      // The AspNetCoreMvc/Views/Home/Index.cshtml contains an external CS0219 issue
      // which is currently not imported due to the fact that the generated code Index.cshtml.g.cs is in the object folder.
    }
  }

  private void validateCSharpFramework(String folderName) throws IOException {
    assumeFalse(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017")); // We can't run .NET Core SDK under VS 2017 CI context
    BuildResult buildResult = runBeginBuildAndEndForStandardProject(folderName, "", true, true);

    assertNoAnalysisWarnings(buildResult);

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    if (isTestProjectSupported()) {
      assertThat(issues).hasSize(3)
        .extracting(Issue::getRule, Issue::getComponent)
        .containsExactlyInAnyOrder(
          tuple(SONAR_RULES_PREFIX + "S1134", folderName + ":Main/Common.cs"),
          tuple(SONAR_RULES_PREFIX + "S2094", folderName + ":Main/Common.cs"),
          tuple(SONAR_RULES_PREFIX + "S2699", folderName + ":UTs/CommonTest.cs"));
    } else {
      assertThat(issues).hasSize(2)
        .extracting(Issue::getRule, Issue::getComponent)
        .containsExactlyInAnyOrder(
          tuple(SONAR_RULES_PREFIX + "S1134", folderName + ":Main/Common.cs"),
          tuple(SONAR_RULES_PREFIX + "S2094", folderName + ":Main/Common.cs"));
    }
  }

  private void assertNoAnalysisWarnings(BuildResult buildResult) {
    Ce.Task task = TestUtils.getAnalysisWarningsTask(ORCHESTRATOR, buildResult);
    assertThat(task.getStatus()).isEqualTo(Ce.TaskStatus.SUCCESS);
    assertThat(task.getWarningsList()).isEmpty();
  }

  // Verify an AnalysisWarning is raised inside the SQ GUI (on the project dashboard)
  private void assertAnalysisWarning(BuildResult buildResult, String message) {
    Ce.Task task = TestUtils.getAnalysisWarningsTask(ORCHESTRATOR, buildResult);
    assertThat(task.getStatus()).isEqualTo(Ce.TaskStatus.SUCCESS);
    assertThat(task.getWarningsList()).contains(message);
  }

  private void runCSharpSharedFileWithOneProjectUsingProjectBaseDir(Function<Path, String> getProjectBaseDir)
    throws IOException {
    String folderName = "CSharpSharedFileWithOneProject";
    Path projectDir = TestUtils.projectDir(basePath, folderName);

    String token = TestUtils.getNewToken(ORCHESTRATOR);
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(folderName)
      .setProjectName(folderName)
      .setProjectVersion("1.0")
      .setProperty("sonar.projectBaseDir", getProjectBaseDir.apply(projectDir))
      .setDebugLogs(true));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, folderName, token);
    assertTrue(result.isSuccess());
    assertThat(getComponent(folderName + ":Common.cs"))
      .isNotNull();
    String class1ComponentId = TestUtils.hasModules(ORCHESTRATOR) ? folderName + ":" + folderName + ":D8FEDBA2-D056-42FB-B146-5A409727B65D:Class1.cs" : folderName + ":ClassLib1/Class1.cs";
    assertThat(getComponent(class1ComponentId))
      .isNotNull();
  }

  private BuildResult runAnalysisWithoutProjectBasedDir(Path projectDir) {
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    String folderName = projectDir.getFileName().toString();
    ScannerForMSBuild scanner = TestUtils.newScanner(ORCHESTRATOR, projectDir, ScannerClassifier.NET, token)
      .addArgument("begin")
      .setProjectKey(folderName)
      .setProjectName(folderName)
      .setProjectVersion("1.0")
      // do NOT set "sonar.projectBaseDir" for this test
      .setScannerVersion(TestUtils.developmentScannerVersion())
      .setEnvironmentVariable(AzureDevOpsUtils.ENV_SOURCES_DIRECTORY, "")
      .setProperty("sonar.verbose", "true")
      .setProperty("sonar.sourceEncoding", "UTF-8");

    ORCHESTRATOR.executeBuild(scanner);

    BuildResult buildResult = TestUtils.runDotnetCommand(projectDir, "build", folderName + ".sln", "--no-incremental");

    assertThat(buildResult.getLastStatus()).isZero();

    // use executeBuildQuietly to allow for failure
    return ORCHESTRATOR.executeBuildQuietly(TestUtils.newScanner(ORCHESTRATOR, projectDir, ScannerClassifier.NET, token)
      .addArgument("end")
      // simulate it's not on Azure Pipelines (otherwise, it will take the projectBaseDir from there)
      .setEnvironmentVariable(AzureDevOpsUtils.ENV_SOURCES_DIRECTORY, "")
      .setScannerVersion(TestUtils.developmentScannerVersion()));
  }

  private void assertProjectFileContains(String projectName, String textToLookFor) throws IOException {
    Path projectPath = TestUtils.projectDir(basePath, projectName);
    Path csProjPath = projectPath.resolve("RazorWebApplication\\RazorWebApplication.csproj");
    String str = FileUtils.readFileToString(csProjPath.toFile(), "utf-8");
    assertThat(str.indexOf(textToLookFor))
      .isPositive();
  }

  private BuildResult runBeginBuildAndEndForStandardProject(String folderName, String projectName) throws IOException {
    return runBeginBuildAndEndForStandardProject(folderName, projectName, true, false);
  }

  private BuildResult runBeginBuildAndEndForStandardProject(String folderName, String projectName, Boolean setProjectBaseDirExplicitly, Boolean useNuGet) throws IOException {
    Path projectDir = TestUtils.projectDir(basePath, folderName);
    return runBeginBuildAndEndForStandardProject(projectDir, projectName, setProjectBaseDirExplicitly, useNuGet);
  }

  private BuildResult runNetCoreBeginBuildAndEnd(Path projectDir, ScannerClassifier classifier) {
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    String folderName = projectDir.getFileName().toString();
    ScannerForMSBuild scanner = TestUtils.newScannerBegin(ORCHESTRATOR, folderName, projectDir, token, classifier)
      .setUseDotNetCore(Boolean.TRUE)
      .setScannerVersion(TestUtils.developmentScannerVersion())
      // ensure that the Environment Variable parsing happens for .NET Core versions
      .setEnvironmentVariable("SONARQUBE_SCANNER_PARAMS", "{}")
      .setProperty("sonar.sourceEncoding", "UTF-8");

    ORCHESTRATOR.executeBuild(scanner);

    // build project
    String[] arguments = new String[]{"build", folderName + ".sln"};
    int status = CommandExecutor.create().execute(Command.create("dotnet")
      .addArguments(arguments)
      // verbosity level: change 'm' to 'd' for detailed logs
      .addArguments("-v:m")
      .addArgument("/warnaserror:AD0001")
      .setDirectory(projectDir.toFile()), 5 * 60 * 1000);

    assertThat(status).isZero();

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Restore,Rebuild", folderName + ".sln");
    return TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, folderName, token, classifier, Collections.emptyList());
  }

  private BuildResult runBeginBuildAndEndForStandardProject(Path projectDir, String projectName, Boolean setProjectBaseDirExplicitly, Boolean useNuGet) {
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    String folderName = projectDir.getFileName().toString();
    ScannerForMSBuild scanner = TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(folderName)
      .setProjectName(folderName)
      .setProjectVersion("1.0")
      .setProperty("sonar.sourceEncoding", "UTF-8");

    if (setProjectBaseDirExplicitly) {
      // When running under Azure DevOps the scanner calculates the projectBaseDir differently.
      // This can be a problem when using shared files as the keys for the shared files
      // are calculated relative to the projectBaseDir.
      // For tests that need to check a specific shared project key, one way to work round
      // the issue is to explicitly set the projectBaseDir to the project directory, as this
      // will take precedence, so then the key for the shared file is what is expected by
      // the tests.
      if (projectName.isEmpty()) {
        scanner.addArgument("/d:sonar.projectBaseDir=" + projectDir.toAbsolutePath());
      } else {
        scanner.addArgument("/d:sonar.projectBaseDir=" + Paths.get(projectDir.toAbsolutePath().toString(), projectName));
      }

    }

    ORCHESTRATOR.executeBuild(scanner);
    if (useNuGet) {
      TestUtils.runNuGet(ORCHESTRATOR, projectDir, false, "restore");
    }
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Restore,Rebuild", folderName + ".sln");
    return TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, folderName, token);
  }

  private void validateRazorProject(String projectName) throws IOException {
    String localProjectKey = PROJECT_KEY + projectName;
    ORCHESTRATOR.getServer().provisionProject(localProjectKey, projectName);

    if (TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017")) {
      return; // We can't build razor under VS 2017 CI context
    }

    Path projectDir = TestUtils.projectDir(basePath, projectName);
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    ORCHESTRATOR.executeBuild(TestUtils.newScannerBegin(ORCHESTRATOR, localProjectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK));
    TestUtils.runNuGet(ORCHESTRATOR, projectDir, false, "restore");
    TestUtils.runDotnetCommand(projectDir, "build", "--no-incremental");
    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, localProjectKey, token);

    assertTrue(result.isSuccess());

    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    List<String> ruleKeys = issues.stream().map(Issue::getRule).collect(Collectors.toList());

    assertThat(ruleKeys).containsAll(Arrays.asList(SONAR_RULES_PREFIX + "S1118", SONAR_RULES_PREFIX + "S1186"));

    assertThat(TestUtils.getMeasureAsInteger(localProjectKey, "lines", ORCHESTRATOR)).isEqualTo(49);
    assertThat(TestUtils.getMeasureAsInteger(localProjectKey, "ncloc", ORCHESTRATOR)).isEqualTo(39);
    assertThat(TestUtils.getMeasureAsInteger(localProjectKey, "files", ORCHESTRATOR)).isEqualTo(2);
  }

  private void testExcludedAndTest(ScannerForMSBuild build, String projectKeyName, Path projectDir, String token, int expectedTestProjectIssues) {
    testExcludedAndTest(build, projectKeyName, projectDir, token, expectedTestProjectIssues, Collections.EMPTY_LIST);
  }

  private void testExcludedAndTest(ScannerForMSBuild build, String projectKeyName, Path projectDir, String token, int expectedTestProjectIssues, List<EnvironmentVariable> environmentVariables) {
    String normalProjectKey = TestUtils.hasModules(ORCHESTRATOR)
      ? String.format("%1$s:%1$s:B93B287C-47DB-4406-9EAB-653BCF7D20DC", projectKeyName)
      : String.format("%1$s:Normal", projectKeyName);
    String testProjectKey = TestUtils.hasModules(ORCHESTRATOR)
      ? String.format("%1$s:%1$s:2DC588FC-16FB-42F8-9FDA-193852E538AF", projectKeyName)
      : String.format("%1$s:Test", projectKeyName);

    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKeyName, projectKeyName);
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKeyName, "cs", "ProfileForTest");

    ORCHESTRATOR.executeBuild(build);

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, environmentVariables, 60 * 1000, "/t:Restore,Rebuild");

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKeyName, token);

    assertTrue(result.isSuccess());

    // Dump debug info
    LOG.info("normalProjectKey = " + normalProjectKey);
    LOG.info("testProjectKey = " + testProjectKey);

    // Two issues are in the normal project, one is in test project (when analyzed)
    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(1 + expectedTestProjectIssues);

    issues = TestUtils.issuesForComponent(ORCHESTRATOR, normalProjectKey);
    assertThat(issues).hasSize(1);

    issues = TestUtils.issuesForComponent(ORCHESTRATOR, testProjectKey);
    assertThat(issues).hasSize(expectedTestProjectIssues);

    // excluded project doesn't exist in SonarQube

    assertThat(TestUtils.getMeasureAsInteger(projectKeyName, "ncloc", ORCHESTRATOR)).isEqualTo(30);
    assertThat(TestUtils.getMeasureAsInteger(normalProjectKey, "ncloc", ORCHESTRATOR)).isEqualTo(30);
    assertThat(TestUtils.getMeasureAsInteger(testProjectKey, "ncloc", ORCHESTRATOR)).isNull();
  }

  private static boolean isTestProjectSupported() {
    return ORCHESTRATOR.getServer().version().isGreaterThan(8, 8);
  }

  private static Components.Component getComponent(String componentKey) {
    return newWsClient().components().show(new ShowRequest().setComponent(componentKey)).getComponent();
  }

  private static WsClient newWsClient() {
    return TestUtils.newWsClient(ORCHESTRATOR);
  }

  private static void startProxy(boolean needProxyAuth) throws Exception {
    httpProxyPort = NetworkUtils.getNextAvailablePort(NetworkUtils.getLocalhost());

    // Setup Threadpool
    QueuedThreadPool threadPool = new QueuedThreadPool();
    threadPool.setMaxThreads(500);

    server = new Server(threadPool);

    // HTTP Configuration
    HttpConfiguration httpConfig = new HttpConfiguration();
    httpConfig.setSecureScheme("https");
    httpConfig.setSendServerVersion(true);
    httpConfig.setSendDateHeader(false);

    // Handler Structure
    HandlerCollection handlers = new HandlerCollection();
    handlers.setHandlers(new Handler[]{proxyHandler(needProxyAuth), new DefaultHandler()});
    server.setHandler(handlers);

    ServerConnector http = new ServerConnector(server, new HttpConnectionFactory(httpConfig));
    http.setPort(httpProxyPort);
    server.addConnector(http);

    server.start();
  }

  private static ServletContextHandler proxyHandler(boolean needProxyAuth) {
    ServletContextHandler contextHandler = new ServletContextHandler();
    if (needProxyAuth) {
      contextHandler.setSecurityHandler(basicAuth("Private!"));
    }
    contextHandler.setServletHandler(newServletHandler());
    return contextHandler;
  }

  private static SecurityHandler basicAuth(String realm) {
    HashLoginService l = new HashLoginService();

    UserStore userStore = new UserStore();
    userStore.addUser(ScannerMSBuildTest.PROXY_USER, Credential.getCredential(ScannerMSBuildTest.PROXY_PASSWORD), new String[]{"user"});

    l.setUserStore(userStore);
    l.setName(realm);

    Constraint constraint = new Constraint();
    constraint.setName(Constraint.__BASIC_AUTH);
    constraint.setRoles(new String[]{"user"});
    constraint.setAuthenticate(true);

    ConstraintMapping cm = new ConstraintMapping();
    cm.setConstraint(constraint);
    cm.setPathSpec("/*");

    ConstraintSecurityHandler csh = new ConstraintSecurityHandler();
    csh.setAuthenticator(new ProxyAuthenticator());
    csh.setRealmName("myrealm");
    csh.addConstraintMapping(cm);
    csh.setLoginService(l);

    return csh;

  }

  private static ServletHandler newServletHandler() {
    ServletHandler handler = new ServletHandler();
    handler.addServletWithMapping(MyProxyServlet.class, "/*");
    return handler;
  }

  private static String getFileKey(String projectKey) {
    return TestUtils.hasModules(ORCHESTRATOR) ? "my.project:my.project:1049030E-AC7A-49D0-BEDC-F414C5C7DDD8:Foo.cs" : projectKey + ":Foo.cs";
  }

  private List<Issue> filter(List<Issue> issues, String ruleIdPrefix) {
    return issues
      .stream()
      .filter(x -> x.getRule().startsWith(ruleIdPrefix))
      .collect(Collectors.toList());
  }

  public static class MyProxyServlet extends ProxyServlet {
    @Override
    protected void service(HttpServletRequest request, HttpServletResponse response) throws ServletException, IOException {
      seenByProxy.add(request.getRequestURI());
      super.service(request, response);
    }

    @Override
    protected void sendProxyRequest(HttpServletRequest clientRequest, HttpServletResponse proxyResponse, Request proxyRequest) {
      super.sendProxyRequest(clientRequest, proxyResponse, proxyRequest);
    }
  }
}
