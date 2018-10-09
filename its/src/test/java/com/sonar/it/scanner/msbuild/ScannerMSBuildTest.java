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
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.build.ScannerForMSBuild;
import com.sonar.orchestrator.container.Edition;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.locator.MavenLocation;
import com.sonar.orchestrator.util.NetworkUtils;
import java.io.IOException;
import java.nio.file.LinkOption;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.Set;
import java.util.concurrent.ConcurrentLinkedDeque;
import java.util.function.Function;
import java.util.stream.Collectors;
import javax.annotation.CheckForNull;
import javax.annotation.Nullable;
import javax.servlet.ServletException;
import javax.servlet.http.HttpServletRequest;
import javax.servlet.http.HttpServletResponse;

import com.sonar.orchestrator.version.Version;
import org.eclipse.jetty.client.api.Request;
import org.eclipse.jetty.proxy.ProxyServlet;
import org.eclipse.jetty.security.ConstraintMapping;
import org.eclipse.jetty.security.ConstraintSecurityHandler;
import org.eclipse.jetty.security.HashLoginService;
import org.eclipse.jetty.security.SecurityHandler;
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
import org.junit.After;
import org.junit.Assume;
import org.junit.Before;
import org.junit.ClassRule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;
import org.sonar.wsclient.issue.Issue;
import org.sonar.wsclient.issue.IssueQuery;
import org.sonarqube.ws.WsComponents;
import org.sonarqube.ws.WsMeasures;
import org.sonarqube.ws.client.HttpConnector;
import org.sonarqube.ws.client.WsClient;
import org.sonarqube.ws.client.WsClientFactories;
import org.sonarqube.ws.client.component.SearchWsRequest;
import org.sonarqube.ws.client.component.ShowWsRequest;
import org.sonarqube.ws.client.measure.ComponentWsRequest;

import static java.util.Objects.requireNonNull;
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
  private static final String PROJECT_KEY = "my.project";
  private static final String MODULE_KEY = "my.project:my.project:1049030E-AC7A-49D0-BEDC-F414C5C7DDD8";
  private static final String FILE_KEY = MODULE_KEY + ":Foo.cs";
  private static final String PROXY_USER = "scott";
  private static final String PROXY_PASSWORD = "tiger";
  private static Server server;
  private static int httpProxyPort;

  private static ConcurrentLinkedDeque<String> seenByProxy = new ConcurrentLinkedDeque<>();

  @ClassRule
  public static Orchestrator ORCHESTRATOR = Orchestrator.builderEnv()
    .setSonarVersion(requireNonNull(System.getProperty("sonar.runtimeVersion"), "Please set system property sonar.runtimeVersion"))
    .setEdition(Edition.DEVELOPER)
    .addPlugin(FileLocation.of(TestUtils.getCustomRoslynPlugin().toFile()))
    .addPlugin(MavenLocation.of("org.sonarsource.dotnet", "sonar-csharp-plugin", "LATEST_RELEASE"))
    // TODO: switch this to LATEST_RELEASE once the OS VB plugin has been released
    .addPlugin(MavenLocation.of("org.sonarsource.dotnet", "sonar-vbnet-plugin", "DEV"))
    .activateLicense()
    .build();

  @ClassRule
  public static TemporaryFolder temp = TestUtils.createTempFolder();

  @Before
  public void setUp() {
    ORCHESTRATOR.resetData();
    seenByProxy.clear();
  }

  @After
  public void stopProxy() throws Exception {
    if (server != null && server.isStarted()) {
      server.stop();
    }
  }

  @Test
  public void testSample() throws Exception {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("sample")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("end"));

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    assertThat(issues).hasSize(2);
    assertLineCountForProjectUnderTest();
  }

  @Test
  public void testSampleWithProxyAuth() throws Exception {
    startProxy(true);
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("sample")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    BuildResult result = ORCHESTRATOR.executeBuildQuietly(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("end")
      .setEnvironmentVariable("SONAR_SCANNER_OPTS", "-Dhttp.nonProxyHosts= -Dhttp.proxyHost=localhost -Dhttp.proxyPort=" + httpProxyPort));

    assertThat(result.getLastStatus()).isNotEqualTo(0);
    assertThat(result.getLogs()).contains("407");
    assertThat(seenByProxy).isEmpty();

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("end")
      .setEnvironmentVariable("SONAR_SCANNER_OPTS",
        "-Dhttp.nonProxyHosts= -Dhttp.proxyHost=localhost -Dhttp.proxyPort=" + httpProxyPort + " -Dhttp.proxyUser=" + PROXY_USER + " -Dhttp.proxyPassword=" + PROXY_PASSWORD));

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    assertThat(issues).hasSize(2);
    assertLineCountForProjectUnderTest();

    assertThat(seenByProxy).isNotEmpty();
  }

  @Test
  public void testHelpMessage() throws IOException {
    Assume.assumeTrue(TestUtils.getScannerVersion(ORCHESTRATOR) == null);

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("/?"));

    assertThat(result.getLogs()).contains("Usage:");
    assertTrue(result.isSuccess());
  }

  @Test
  public void testNoProjectNameAndVersion() throws Exception {
    Assume.assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(6, 1));

    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("end"));

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    assertThat(issues).hasSize(2);
    assertLineCountForProjectUnderTest();
  }

  private void assertLineCountForProjectUnderTest() {
    assertThat(getMeasureAsInteger(FILE_KEY, "ncloc")).isEqualTo(23);
    assertThat(getMeasureAsInteger(PROJECT_KEY, "ncloc")).isEqualTo(37);
    assertThat(getMeasureAsInteger(FILE_KEY, "lines")).isEqualTo(71);
  }

  @Test
  public void testExcludedAndTest() throws Exception {
    String normalProjectKey = "my.project:my.project:B93B287C-47DB-4406-9EAB-653BCF7D20DC";
    String testProjectKey = "my.project:my.project:2DC588FC-16FB-42F8-9FDA-193852E538AF";

    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "excludedAndTest");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTest");

    Path projectDir = TestUtils.projectDir(temp, "ExcludedTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("excludedAndTest")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
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
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("multilang")
      .setProjectVersion("1.0")
      .setDebugLogs(true));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("end"));

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    // 2 CS, 2 vbnet
    assertThat(issues).hasSize(4);

    List<String> keys = issues.stream().map(Issue::ruleKey).collect(Collectors.toList());
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
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("parameters")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
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
    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
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
    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
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
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("sample")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild", "/p:ExcludeProjectsFromAnalysis=true");

    BuildResult result = ORCHESTRATOR.executeBuildQuietly(TestUtils.newScanner(ORCHESTRATOR, projectDir)
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
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("empty")
      .setProjectVersion("1.0")
      .addArgument("/d:sonar.verbose=true"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    BuildResult result = ORCHESTRATOR.executeBuildQuietly(TestUtils.newScanner(ORCHESTRATOR, projectDir)
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
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("sample")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("end"));

    assertThat(result.getLogs()).doesNotContain("File is not under the project directory and cannot currently be analysed by SonarQube");
    assertThat(result.getLogs()).doesNotContain("AssemblyAttributes.cs");
  }

  @Test
  public void checkExternalIssues() throws Exception {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ExternalIssues/TestQualityProfileExternalIssues.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTestExternalIssues");

    Path projectDir = TestUtils.projectDir(temp, "ExternalIssues");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("sample")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("end"));
    assertTrue(result.isSuccess());

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    List<String> keys = issues.stream().map(Issue::ruleKey).collect(Collectors.toList());

    // The same set of Sonar issues should be reported, regardless of whether
    // external issues are imported or not
    assertThat(keys).containsAll(Arrays.asList(
      "csharpsquid:S125",
      "csharpsquid:S1134"));

    if (ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(7,4))
    {
      // if external issues are imported, then there should also be some
      // Wintellect errors.  However, only file-level issues are imported.
      assertThat(keys).containsAll(Arrays.asList(
        "external_roslyn:Wintellect004"));

      assertThat(issues).hasSize(3);

    } else {
      // Not expecting any external issues
      assertThat(issues).hasSize(2);
    }
  }

  @Test
  public void testCustomRoslynAnalyzer() throws Exception {
    String folderName = "ProjectUnderTest";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/" + folderName +
      "/TestQualityProfileCustomRoslyn.xml"));
    ORCHESTRATOR.getServer().provisionProject(folderName, folderName);
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(folderName, "cs",
      "ProfileForTestCustomRoslyn");

    runBeginBuildAndEndForStandardProject(folderName, false);

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    assertThat(issues).hasSize(2 + 37 + 1);
  }

  @Test
  public void testCSharpAllFlat() throws IOException {
    runBeginBuildAndEndForStandardProject("CSharpAllFlat", true);

    assertThat(getComponent("CSharpAllFlat:Common.cs")).isNotNull();
  }

  @Test
  public void testCSharpSharedFiles() throws IOException {
    runBeginBuildAndEndForStandardProject("CSharpSharedFiles", true);

    assertThat(getComponent("CSharpSharedFiles:Common.cs"))
      .isNotNull();
    assertThat(getComponent("CSharpSharedFiles:CSharpSharedFiles:D8FEDBA2-D056-42FB-B146-5A409727B65D:Class1.cs"))
      .isNotNull();
    assertThat(getComponent("CSharpSharedFiles:CSharpSharedFiles:72CD6ED2-481A-4828-BA15-8CD5F0472A77:Class2.cs"))
      .isNotNull();
  }

  @Test
  public void testCSharpSharedProjectType() throws IOException {
    runBeginBuildAndEndForStandardProject("CSharpSharedProjectType", true);

    assertThat(getComponent("CSharpSharedProjectType:SharedProject/TestEventInvoke.cs"))
      .isNotNull();
    assertThat(getComponent("CSharpSharedProjectType:CSharpSharedProjectType:36F96F66-8136-46C0-B83B-EFAE05A8FFC1:Program.cs"))
      .isNotNull();
    assertThat(getComponent("CSharpSharedProjectType:CSharpSharedProjectType:F96D8AA1-BCE1-4655-8D65-08F2A5FAC15B:Program.cs"))
      .isNotNull();
  }

  @Test
  public void testCSharpSharedFileWithOneProjectWithoutProjectBaseDir() throws IOException {
    runBeginBuildAndEndForStandardProject("CSharpSharedFileWithOneProject", false);

    Set<String> componentKeys = newWsClient()
      .components()
      .search(new SearchWsRequest().setLanguage("cs").setQualifiers(Collections.singletonList("FIL")))
      .getComponentsList()
      .stream()
      .map(WsComponents.Component::getKey)
      .collect(Collectors.toSet());

    // When not using /d:sonar.projectBaseDir the root dir will be set at the level of the project so that the
    // file Common.cs will be outside of the scope and won't be pushed to SQ
    assertThat(componentKeys).doesNotContain("CSharpSharedFileWithOneProject:Common.cs");
    assertThat(componentKeys).contains("CSharpSharedFileWithOneProject:CSharpSharedFileWithOneProject:"
      + "D8FEDBA2-D056-42FB-B146-5A409727B65D:Class1.cs");
  }

  @Test
  public void testCSharpSharedFileWithOneProjectUsingProjectBaseDirAbsolute() throws IOException {
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

  /* TODO: This test doesn't work as expected. Relative path will create sub-folders on SonarQube and so files are not
           located where you expect them.
  @Test
  public void testCSharpSharedFileWithOneProjectUsingProjectBaseDirRelative() throws IOException {
    runCSharpSharedFileWithOneProjectUsingProjectBaseDir(projectDir -> "..\\..");
  } */

  @Test
  public void testCSharpSharedFileWithOneProjectUsingProjectBaseDirAbsoluteShort() throws IOException {
    runCSharpSharedFileWithOneProjectUsingProjectBaseDir(Path::toString);
  }

  private void runCSharpSharedFileWithOneProjectUsingProjectBaseDir(Function<Path, String> getProjectBaseDir)
    throws IOException {
    String folderName = "CSharpSharedFileWithOneProject";
    Path projectDir = TestUtils.projectDir(temp, folderName);

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(folderName)
      .setProjectName(folderName)
      .setProjectVersion("1.0")
      .setProperty("sonar.projectBaseDir", getProjectBaseDir.apply(projectDir))
      .setDebugLogs(true));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("end"));

    assertThat(getComponent(folderName + ":Common.cs"))
      .isNotNull();
    assertThat(getComponent(folderName + ":" + folderName + ":D8FEDBA2-D056-42FB-B146-5A409727B65D:"
      + "Class1.cs"))
      .isNotNull();
  }

  private void runBeginBuildAndEndForStandardProject(String folderName, Boolean setProjectBaseDirExplicitly) throws IOException {
    Path projectDir = TestUtils.projectDir(temp, folderName);

    ScannerForMSBuild scanner = TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(folderName)
      .setProjectName(folderName)
      .setProjectVersion("1.0");

    if (setProjectBaseDirExplicitly) {
      // When running under VSTS the scanner calculates the projectBaseDir differently.
      // This can be a problem when using shared files as the keys for the shared files
      // are calculated relative to the projectBaseFir.
      // For tests that need to check a specific shared project key, one way to work round
      // the issue is to explicitly set the projectBaseDir to the project directory, as this
      // will take precedence, so then then key for the shared file is what is expected by
      // the tests.
      scanner.addArgument("/d:sonar.projectBaseDir=" + projectDir.toAbsolutePath());
    }

    ORCHESTRATOR.executeBuild(scanner);
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild", folderName + ".sln");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("end"));

    TestUtils.dumpComponentList(ORCHESTRATOR);
  }

  private static WsComponents.Component getComponent(String componentKey) {
    return newWsClient().components().show(new ShowWsRequest().setKey(componentKey)).getComponent();
  }

  @CheckForNull
  private static Integer getMeasureAsInteger(String componentKey, String metricKey) {
    WsMeasures.Measure measure = getMeasure(componentKey, metricKey);
    return (measure == null) ? null : Integer.parseInt(measure.getValue());
  }

  @CheckForNull
  private static WsMeasures.Measure getMeasure(@Nullable String componentKey, String metricKey) {
    WsMeasures.ComponentWsResponse response = newWsClient().measures().component(new ComponentWsRequest()
      .setComponentKey(componentKey)
      .setMetricKeys(Collections.singletonList(metricKey)));
    List<WsMeasures.Measure> measures = response.getComponent().getMeasuresList();
    return measures.size() == 1 ? measures.get(0) : null;
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
      contextHandler.setSecurityHandler(basicAuth(PROXY_USER, PROXY_PASSWORD, "Private!"));
    }
    contextHandler.setServletHandler(newServletHandler());
    return contextHandler;
  }

  private static SecurityHandler basicAuth(String username, String password, String realm) {

    HashLoginService l = new HashLoginService();
    l.putUser(username, Credential.getCredential(password), new String[]{"user"});
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
