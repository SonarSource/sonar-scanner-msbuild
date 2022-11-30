/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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
import org.apache.commons.io.FileUtils;
import org.apache.http.HttpEntity;
import org.apache.http.client.methods.CloseableHttpResponse;
import org.apache.http.client.methods.HttpPost;
import org.apache.http.entity.ContentType;
import org.apache.http.entity.mime.MultipartEntityBuilder;
import org.apache.http.impl.client.CloseableHttpClient;
import org.apache.http.impl.client.HttpClients;
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
import org.junit.After;
import org.junit.Assume;
import org.junit.Before;
import org.junit.ClassRule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.sonarqube.ws.Ce;
import org.sonarqube.ws.Components;
import org.sonarqube.ws.Issues;
import org.sonarqube.ws.Issues.Issue;
import org.sonarqube.ws.client.WsClient;
import org.sonarqube.ws.client.ce.ActivityRequest;
import org.sonarqube.ws.client.components.ShowRequest;

import java.io.BufferedWriter;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileWriter;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
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

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;
import static org.awaitility.Awaitility.await;
import static org.junit.Assert.assertTrue;
import static org.junit.Assume.assumeFalse;

public class ScannerMSBuildTest {
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

  @ClassRule
  public static TemporaryFolder temp = TestUtils.createTempFolder();

  @ClassRule
  public static Orchestrator ORCHESTRATOR = SonarScannerTestSuite.ORCHESTRATOR;

  @Before
  public void setUp() {
    TestUtils.reset(ORCHESTRATOR);
    seenByProxy.clear();
  }

  @After
  public void stopProxy() throws Exception {
    if (server != null && server.isStarted()) {
      server.stop();
    }
  }

  @Test
  public void incrementalPrAnalysis_ProducesUnchangedFiles() throws IOException {
    Assume.assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(9, 4)); // Cache API was introduced in 9.4

    String projectKey = "incremental-pr-analysis";
    String baseBranch = "master";
    Path projectDir = TestUtils.projectDir(temp, "IncrementalPRAnalysis");
    // This is a temporary solution for uploading a valid cache.
    // The file was generated from https://github.com/SonarSource/sonar-dotnet/commit/28c1224aca62968fb52b0332d6f6b7ef3da11f2b commit.
    // In order to regenerate this file:
    // - Update `FileStatusCacheSensor` to trim the paths correctly and save only the relative paths.
    // - build the plugin
    // - add it to your SonarQube
    // - start SonarQube and analyze `IncrementalPRAnalysis` project with `/d:sonar.scanner.keepReport=true`
    // - archive the content of `.sonarqube\out\.sonar\scanner-report\` (only the content, without parent folder) as `scanner-report.zip`
    File reportZip = projectDir.resolve("scanner-report.zip").toFile();

    uploadAnalysisWithCache(projectKey, baseBranch, reportZip);

    File fileToBeChanged = projectDir.resolve("IncrementalPRAnalysis\\WithChanges.cs").toFile();
    BufferedWriter writer = new BufferedWriter(new FileWriter(fileToBeChanged, true));
    writer.append(' ');
    writer.close();

    BuildResult result = ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(projectKey)
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setDebugLogs(true) // To assert debug logs too
      .setProperty("sonar.pullrequest.base", baseBranch));

    assertTrue(result.isSuccess());
    assertThat(result.getLogs()).contains("Processing analysis cache");
    assertThat(result.getLogs()).contains("Processing pull request with base branch 'master'.");
    assertThat(result.getLogs()).contains("Downloading cache. Project key: incremental-pr-analysis, branch: master.");

    File expectedUnchangedFiles = new File(projectDir.resolve(".sonarqube\\conf\\UnchangedFiles.txt").toString());
    assertThat(expectedUnchangedFiles).exists();
    assertThat(Files.readString(expectedUnchangedFiles.toPath()))
      .contains("Unchanged1.cs")
      .contains("Unchanged2.cs")
      .doesNotContain("WithChanges.cs"); // Was modified
  }

  private void uploadAnalysisWithCache(String projectKey, String baseBranch, File reportZip) throws IOException {
    CloseableHttpClient httpClient = HttpClients.createDefault();
    HttpPost uploadFile = new HttpPost(ORCHESTRATOR.getServer().getUrl() + "/api/ce/submit");

    MultipartEntityBuilder builder = MultipartEntityBuilder.create();
    builder.addTextBody("projectKey", projectKey);
    builder.addBinaryBody("report", new FileInputStream(reportZip), ContentType.APPLICATION_OCTET_STREAM, reportZip.getName());

    HttpEntity multipart = builder.build();
    uploadFile.setEntity(multipart);
    CloseableHttpResponse x = httpClient.execute(uploadFile);
    LOG.warn("ZZZ: " + x.toString());
    LOG.warn("ZZZ: task id: " + new String(x.getEntity().getContent().readAllBytes(), StandardCharsets.UTF_8));

    await()
      .pollInterval(Duration.ofSeconds(1))
      .atMost(Duration.ofSeconds(120))
      .until(() -> {
        try
        {
          com.sonar.orchestrator.http.HttpResponse y = ORCHESTRATOR.getServer().newHttpCall("api/ce/activity_status").setParam("component", projectKey).setAuthenticationToken(ORCHESTRATOR.getDefaultAdminToken()).execute();
          LOG.warn("ZZZ: before, activity status" + y.getBodyAsString());
          com.sonar.orchestrator.http.HttpResponse response = ORCHESTRATOR.getServer().newHttpCall("api/analysis_cache/get").setParam("project", projectKey).setParam("branch", baseBranch).execute();
          LOG.warn("ZZZ: after, " + response.getBodyAsString());
          return true;
        }
        catch (HttpException ex) {
          LOG.warn("ZZZ: fetch attempt", ex);
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
          tuple(SONAR_RULES_PREFIX + "S1134", folderName + ":AspNetCoreMvc/Program.cs"),
          tuple(SONAR_RULES_PREFIX + "S1134", folderName + ":Main/Common.cs"),
          tuple(SONAR_RULES_PREFIX + "S2699", folderName + ":UTs/CommonTest.cs"));
      // The AspNetCoreMvc/Views/Home/Index.cshtml contains an external CS0219 issue
      // which is currently not imported due to the fact that the generated code Index.cshtml.g.cs is in the object folder.
    } else {
      assertThat(issues).hasSize(2)
        .extracting(Issue::getRule, Issue::getComponent)
        .containsExactlyInAnyOrder(
          tuple(SONAR_RULES_PREFIX + "S1134", folderName + ":AspNetCoreMvc/Program.cs"),
          tuple(SONAR_RULES_PREFIX + "S1134", folderName + ":Main/Common.cs"));
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
      assertThat(issues).hasSize(2)
        .extracting(Issue::getRule, Issue::getComponent)
        .containsExactlyInAnyOrder(
          tuple(SONAR_RULES_PREFIX + "S1134", folderName + ":Main/Common.cs"),
          tuple(SONAR_RULES_PREFIX + "S2699", folderName + ":UTs/CommonTest.cs"));
    } else {
      assertThat(issues).hasSize(1)
        .extracting(Issue::getRule, Issue::getComponent)
        .containsExactlyInAnyOrder(
          tuple(SONAR_RULES_PREFIX + "S1134", folderName + ":Main/Common.cs"));
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
    assertThat(task.getWarningsList()).containsExactly(message);
  }

  private void runCSharpSharedFileWithOneProjectUsingProjectBaseDir(Function<Path, String> getProjectBaseDir)
    throws IOException {
    String folderName = "CSharpSharedFileWithOneProject";
    Path projectDir = TestUtils.projectDir(temp, folderName);

    String token = TestUtils.getNewToken(ORCHESTRATOR);
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(folderName)
      .setProjectName(folderName)
      .setProjectVersion("1.0")
      .setProperty("sonar.projectBaseDir", getProjectBaseDir.apply(projectDir))
      .setProperty("sonar.login", token)
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
    ScannerForMSBuild scanner = TestUtils.newScanner(ORCHESTRATOR, projectDir, ScannerClassifier.NET_5)
      .addArgument("begin")
      .setProjectKey(folderName)
      .setProjectName(folderName)
      .setProjectVersion("1.0")
      // do NOT set "sonar.projectBaseDir" for this test
      .setProperty("sonar.login", token)
      .setScannerVersion(TestUtils.developmentScannerVersion())
      .setEnvironmentVariable(VstsUtils.ENV_SOURCES_DIRECTORY, "")
      .setProperty("sonar.verbose", "true")
      .setProperty("sonar.sourceEncoding", "UTF-8");

    ORCHESTRATOR.executeBuild(scanner);

    // build project
    String[] arguments = new String[]{"build", folderName + ".sln"};
    int status = CommandExecutor.create().execute(Command.create("dotnet")
      .addArguments(arguments)
      // verbosity level: change 'm' to 'd' for detailed logs
      .addArguments("-v:m")
      .addArgument("/warnaserror:AD0001")
      .setEnvironmentVariable(VstsUtils.ENV_SOURCES_DIRECTORY, "")
      .setDirectory(projectDir.toFile()), 5 * 60 * 1000);

    assertThat(status).isZero();

    // use executeBuildQuietly to allow for failure
    return ORCHESTRATOR.executeBuildQuietly(TestUtils.newScanner(ORCHESTRATOR, projectDir, ScannerClassifier.NET_5)
      .addArgument("end")
      .setProperty("sonar.login", token)
      // simulate it's not on Azure Pipelines (otherwise, it will take the projectBaseDir from there)
      .setEnvironmentVariable(VstsUtils.ENV_SOURCES_DIRECTORY, "")
      .setScannerVersion(TestUtils.developmentScannerVersion()));
  }

  private void assertProjectFileContains(String projectName, String textToLookFor) throws IOException {
    Path projectPath = TestUtils.projectDir(temp, projectName);
    Path csProjPath = projectPath.resolve("RazorWebApplication\\RazorWebApplication.csproj");
    String str = FileUtils.readFileToString(csProjPath.toFile(), "utf-8");
    assertThat(str.indexOf(textToLookFor))
      .isPositive();
  }

  private BuildResult runBeginBuildAndEndForStandardProject(String folderName, String projectName) throws IOException {
    return runBeginBuildAndEndForStandardProject(folderName, projectName, true, false);
  }

  private BuildResult runBeginBuildAndEndForStandardProject(String folderName, String projectName, Boolean setProjectBaseDirExplicitly, Boolean useNuGet) throws IOException {
    Path projectDir = TestUtils.projectDir(temp, folderName);
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
    return TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, folderName, token, classifier);
  }

  private BuildResult runBeginBuildAndEndForStandardProject(Path projectDir, String projectName, Boolean setProjectBaseDirExplicitly, Boolean useNuGet) {
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    String folderName = projectDir.getFileName().toString();
    ScannerForMSBuild scanner = TestUtils.newScanner(ORCHESTRATOR, projectDir)
      .addArgument("begin")
      .setProjectKey(folderName)
      .setProjectName(folderName)
      .setProjectVersion("1.0")
      .setProperty("sonar.sourceEncoding", "UTF-8")
      .setProperty("sonar.login", token);

    if (setProjectBaseDirExplicitly) {
      // When running under VSTS the scanner calculates the projectBaseDir differently.
      // This can be a problem when using shared files as the keys for the shared files
      // are calculated relative to the projectBaseDir.
      // For tests that need to check a specific shared project key, one way to work round
      // the issue is to explicitly set the projectBaseDir to the project directory, as this
      // will take precedence, so then then key for the shared file is what is expected by
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

    Path projectDir = TestUtils.projectDir(temp, projectName);
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    ORCHESTRATOR.executeBuild(TestUtils.newScannerBegin(ORCHESTRATOR, localProjectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK_46));
    TestUtils.runNuGet(ORCHESTRATOR, projectDir, false, "restore");
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild", "/nr:false");
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

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, environmentVariables, "/t:Rebuild");

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKeyName, token);

    assertTrue(result.isSuccess());

    // Dump debug info
    LOG.info("normalProjectKey = " + normalProjectKey);
    LOG.info("testProjectKey = " + testProjectKey);

    // Two issues are in the normal project, one is in test project (when analyzed)
    List<Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(2 + expectedTestProjectIssues);

    issues = TestUtils.issuesForComponent(ORCHESTRATOR, normalProjectKey);
    assertThat(issues).hasSize(2);

    issues = TestUtils.issuesForComponent(ORCHESTRATOR, testProjectKey);
    assertThat(issues).hasSize(expectedTestProjectIssues);

    // excluded project doesn't exist in SonarQube

    assertThat(TestUtils.getMeasureAsInteger(projectKeyName, "ncloc", ORCHESTRATOR)).isEqualTo(45);
    assertThat(TestUtils.getMeasureAsInteger(normalProjectKey, "ncloc", ORCHESTRATOR)).isEqualTo(45);
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
