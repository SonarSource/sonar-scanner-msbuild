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
package com.sonar.it.scanner.msbuild.utils;

import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.build.ScannerForMSBuild;
import com.sonar.orchestrator.http.HttpMethod;
import com.sonar.orchestrator.locator.Location;
import com.sonar.orchestrator.locator.MavenLocation;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import com.sonar.orchestrator.util.StreamConsumer;
import java.io.File;
import java.io.IOException;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Date;
import java.util.List;
import java.util.Set;
import java.util.stream.Collectors;
import javax.annotation.CheckForNull;
import javax.annotation.Nullable;
import org.apache.commons.io.FileUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.sonarqube.ws.Ce;
import org.sonarqube.ws.Components;
import org.sonarqube.ws.Issues.Issue;
import org.sonarqube.ws.Measures;
import org.sonarqube.ws.client.HttpConnector;
import org.sonarqube.ws.client.WsClient;
import org.sonarqube.ws.client.WsClientFactories;
import org.sonarqube.ws.client.ce.TaskRequest;
import org.sonarqube.ws.client.components.TreeRequest;
import org.sonarqube.ws.client.measures.ComponentRequest;
import org.sonarqube.ws.client.settings.SetRequest;
import org.sonarqube.ws.client.usertokens.GenerateRequest;

import static org.assertj.core.api.Assertions.assertThat;

public class TestUtils {
  final static Logger LOG = LoggerFactory.getLogger(TestUtils.class);

  private static final int MSBUILD_RETRY = 3;
  private static final String NUGET_PATH = "NUGET_PATH";
  private static String token = null;

  public static final Long TIMEOUT_LIMIT = 60 * 1000L;
  public static final String MSBUILD_DEFAULT_PATH = "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\MSBuild\\15.0\\Bin\\MSBuild.exe";

  @CheckForNull
  public static String getScannerVersion(Orchestrator orchestrator) {
    return orchestrator.getConfiguration().getString("scannerForMSBuild.version");
  }

  @CheckForNull
  public static String getAnalyzerVersion(Orchestrator orchestrator) {
    return orchestrator.getConfiguration().getString("SONAR_DOTNET_VERSION");
  }

  public static boolean isDevOrLatestRelease(String version) {
    return version.equals("DEV") || version.equals("LATEST_RELEASE");
  }

  private static MavenLocation getScannerMavenLocation(String scannerVersion, ScannerClassifier classifier) {
    String groupId = "org.sonarsource.scanner.msbuild";
    String artifactId = "sonar-scanner";
    return MavenLocation.builder()
      .setGroupId(groupId)
      .setArtifactId(artifactId)
      .setVersion(scannerVersion)
      .setClassifier(classifier.toString())
      .withPackaging("zip")
      .build();
  }

  // https://github.com/SonarSource/sonar-scanner-msbuild/issues/1235
  public static String developmentScannerVersion() {
    // dummy version, needed by Orchestrator to use the dotnet core versions of the scanner
    return "99";
  }

  public static ScannerForMSBuild newScanner(Orchestrator orchestrator, Path projectDir, String token) {
    return newScanner(orchestrator, projectDir, ScannerClassifier.NET_FRAMEWORK, token);
  }

  public static ScannerForMSBuild newScannerBegin(Orchestrator orchestrator, String projectKeyName, Path projectDir, String token, ScannerClassifier classifier) {
    return TestUtils.newScanner(orchestrator, projectDir, classifier, token)
      .addArgument("begin")
      .setProjectKey(projectKeyName)
      .setProjectName(projectKeyName)
      .setProjectVersion("1.0")
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString());
  }

  public static ScannerForMSBuild newScanner(Orchestrator orchestrator, Path projectDir, ScannerClassifier classifier, String token) {
    String scannerVersion = getScannerVersion(orchestrator);

    Location scannerLocation;
    if (scannerVersion != null) {
      LOG.info("Using Scanner for MSBuild " + scannerVersion);
      scannerLocation = getScannerMavenLocation(scannerVersion, classifier);
    } else {
      String scannerLocationEnv = System.getenv("SCANNER_LOCATION");
      if (scannerLocationEnv != null) {
        LOG.info("Using Scanner for MSBuild specified by %SCANNER_LOCATION%: " + scannerLocationEnv);
        scannerLocation = classifier.toLocation(scannerLocationEnv);
      } else {
        // run locally
        LOG.info("Using Scanner for MSBuild from the local build");
        scannerLocation = classifier.toLocation("../build");
      }
    }
    LOG.info("Scanner location: " + scannerLocation);
    var scanner = ScannerForMSBuild.create(projectDir.toFile()).setScannerLocation(scannerLocation).setUseDotNetCore(classifier.isDotNetCore());
    if (orchestrator.getServer().version().isGreaterThanOrEquals(10, 0)) {
      // The `sonar.token` property was introduced in SonarQube 10.0
      scanner.setProperty("sonar.token", token);
    } else {
      scanner.setProperty("sonar.login", token);
    }
    return scanner;
  }

  public static void reset(Orchestrator orchestrator) {
    // The expected format is yyyy-MM-ddTHH:mm:ss+HHmm. e.g. 2017-10-19T13:00:00+0200
    SimpleDateFormat formatter = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ssZ");
    String currentDateTime = formatter.format(new Date());

    LOG.info("TEST SETUP: deleting projects analyzed before: " + currentDateTime);

    orchestrator.getServer()
      .newHttpCall("/api/projects/bulk_delete")
      .setAdminCredentials()
      .setMethod(HttpMethod.POST)
      .setParams("analyzedBefore", currentDateTime)
      .execute();
  }

  public static Path getCustomRoslynPlugin() {
    LOG.info("TEST SETUP: calculating custom Roslyn plugin path...");
    Path customPluginDir = Paths.get("").resolve("analyzers");

    DirectoryStream.Filter<Path> jarFilter = file -> Files.isRegularFile(file) && file.toString().endsWith(".jar");
    List<Path> jars = new ArrayList<>();
    try {
      Files.newDirectoryStream(customPluginDir, jarFilter).forEach(jars::add);
    } catch (IOException e) {
      throw new IllegalStateException(e);
    }
    if (jars.isEmpty()) {
      throw new IllegalStateException("No jars found in " + customPluginDir);
    } else if (jars.size() > 1) {
      throw new IllegalStateException("Several jars found in " + customPluginDir);
    }

    LOG.info("TEST SETUP: custom plugin path = " + jars.get(0));

    return jars.get(0);
  }

  public static Location getMavenLocation(String groupId, String artifactId, String version) {
    TestUtils.LOG.info("TEST SETUP: getting Maven location: " + groupId + " " + artifactId + " " + version);
    Location location = MavenLocation.of(groupId, artifactId, version);

    TestUtils.LOG.info("TEST SETUP: location = " + location.toString());
    return location;
  }

  public static void createVirtualDrive(String drive, Path projectDir, String subDirectory) {
    int setupStatus = CommandExecutor.create().execute(
      Command.create("SUBST")
        .addArguments(drive)
        .addArguments(projectDir.resolve(subDirectory).toAbsolutePath().toString())
        .setDirectory(projectDir.toFile()),
      TIMEOUT_LIMIT);
    assertThat(setupStatus).isZero();
  }

  public static void deleteVirtualDrive(String drive) {
    int cleanupStatus = CommandExecutor.create().execute(
      Command.create("SUBST").addArguments(drive).addArguments("/D"),
      TIMEOUT_LIMIT);
    assertThat(cleanupStatus).isZero();
  }

  public static Path projectDir(Path temp, String projectName) throws IOException {
    File projectToCopy = Paths.get("projects").resolve(projectName).toFile();
    File destination = new File(temp.toFile(), projectName).getCanonicalFile();
    FileUtils.deleteDirectory(destination);
    Path newFolder = Files.createDirectories(destination.toPath());
    FileUtils.copyDirectory(projectToCopy, newFolder.toFile());
    return newFolder;
  }

  public static void runMSBuildWithBuildWrapper(Orchestrator orch, Path projectDir, File buildWrapperPath, File outDir,
    String... arguments) {
    Path msBuildPath = getMsBuildPath(orch);

    int r = CommandExecutor.create().execute(Command.create(buildWrapperPath.toString())
      .addArgument("--out-dir")
      .addArgument(outDir.toString())
      .addArgument(msBuildPath.toString())
      .addArguments(arguments)
      .setDirectory(projectDir.toFile()), TIMEOUT_LIMIT);
    assertThat(r).isZero();
  }

  public static void updateSetting(Orchestrator orchestrator, String projectKey, String propertyKey, List<String> values) {
    newAdminWsClient(orchestrator).settings().set(new SetRequest().setComponent(projectKey).setKey(propertyKey).setValues(values));
  }

  public static void runMSBuild(Orchestrator orch, Path projectDir, String... arguments) {
    runMSBuild(orch, projectDir, Collections.emptyList(), TIMEOUT_LIMIT, arguments);
  }

  public static void buildMSBuild(Orchestrator orchestrator, Path projectDir) {
    runMSBuild(orchestrator, projectDir, Collections.emptyList(), TIMEOUT_LIMIT, "/t:Restore,Rebuild");
  }

  public static BuildResult runMSBuild(Orchestrator orch, Path projectDir, List<EnvironmentVariable> environmentVariables, long timeoutLimit, String... arguments) {
    BuildResult r = runMSBuildQuietly(orch, projectDir, environmentVariables, timeoutLimit, arguments);
    assertThat(r.isSuccess()).isTrue();
    return r;
  }

  public static void runNuGet(Orchestrator orch, Path projectDir, Boolean useDefaultVSCodeMSBuild, String... arguments) {
    Path nugetPath = getNuGetPath(orch);
    var nugetRestore = Command.create(nugetPath.toString())
      .addArguments(arguments)
      .setDirectory(projectDir.toFile());

    if (!useDefaultVSCodeMSBuild) {
      nugetRestore = nugetRestore.addArguments("-MSBuildPath", TestUtils.getMsBuildPath(orch).getParent().toString());
    }

    int r = CommandExecutor.create().execute(nugetRestore, 300 * 1000);
    assertThat(r).isZero();
  }

  public static BuildResult runDotnetCommand(Path workingDir, String dotnetCommand, String... arguments) {
    var argumentList = new ArrayList<>(Arrays.asList(arguments));
    argumentList.add(0, dotnetCommand);
    argumentList.add("-warnaserror:AD0001");
    argumentList.add("-nodereuse:false"); // This is mandatory, otherwise process node locks the dlls in .sonarqube preventing the test to delete temp directory
    argumentList.add("--verbosity:minimal"); // Change to "detailed" if more information is needed

    var buildResult = new BuildResult();
    StreamConsumer.Pipe writer = new StreamConsumer.Pipe(buildResult.getLogsWriter());
    var command = Command.create("dotnet").addArguments(argumentList).setDirectory(workingDir.toFile());
    var status = CommandExecutor.create().execute(command, writer, TIMEOUT_LIMIT);
    buildResult.addStatus(status);
    return buildResult;
  }

  private static Path getNuGetPath(Orchestrator orch) {
    LOG.info("TEST SETUP: calculating path to NuGet.exe...");
    String toolsFolder = Paths.get("tools").resolve("nuget.exe").toAbsolutePath().toString();
    String nugetPathStr = orch.getConfiguration().getString(NUGET_PATH, toolsFolder);
    Path nugetPath = Paths.get(nugetPathStr).toAbsolutePath();
    if (!Files.exists(nugetPath)) {
      throw new IllegalStateException("Unable to find NuGet at '" + nugetPath + "'. Please configure property '" + NUGET_PATH + "'");
    }

    LOG.info("TEST SETUP: nuget.exe path = " + nugetPath);
    return nugetPath;
  }

  private static BuildResult runMSBuildQuietly(Orchestrator orch, Path projectDir, List<EnvironmentVariable> environmentVariables, long timeoutLimit, String... arguments) {
    Path msBuildPath = getMsBuildPath(orch);

    BuildResult result = new BuildResult();
    StreamConsumer.Pipe writer = new StreamConsumer.Pipe(result.getLogsWriter());

    int status = -1;
    int attempts = 0;
    boolean mustRetry = true;
    Command command = Command.create(msBuildPath.toString())
      .addArguments("-nodeReuse:false")
      .addArguments(arguments)
      .setDirectory(projectDir.toFile());
    for (EnvironmentVariable environmentVariable : environmentVariables) {
      command.setEnvironmentVariable(environmentVariable.getName(), environmentVariable.getValue());
    }
    while (mustRetry && attempts < MSBUILD_RETRY) {
      status = CommandExecutor.create().execute(command, writer, timeoutLimit);
      attempts++;
      mustRetry = status != 0;
      if (mustRetry) {
        LOG.warn("Failed to build, will retry " + (MSBUILD_RETRY - attempts) + " times.");
      }
    }

    result.addStatus(status);
    return result;
  }

  public static Path getMsBuildPath(Orchestrator orch) {
    String msBuildPathStr = orch.getConfiguration().getString("msbuild.path",
      orch.getConfiguration().getString("MSBUILD_PATH", MSBUILD_DEFAULT_PATH));
    Path msBuildPath = Paths.get(msBuildPathStr).toAbsolutePath();
    if (!Files.exists(msBuildPath)) {
      throw new IllegalStateException("Unable to find MSBuild at " + msBuildPath
        + ". Please configure property 'msbuild.path' or 'MSBUILD_PATH' environment variable to the full path to MSBuild.exe.");
    }
    LOG.info("MSBUILD_PATH is set to {}", msBuildPath);
    return msBuildPath;
  }

  public static void dumpComponentList(Orchestrator orchestrator, String projectKey) {
    Set<String> componentKeys = newWsClient(orchestrator)
      .components()
      .tree(new TreeRequest().setQualifiers(Collections.singletonList("FIL")).setComponent(projectKey))
      .getComponentsList()
      .stream()
      .map(Components.Component::getKey)
      .collect(Collectors.toSet());

    LOG.info("Dumping C# component keys:");
    for (String key : componentKeys) {
      LOG.info("  Key: " + key);
    }
  }

  public static void dumpAllIssues(Orchestrator orchestrator) {
    LOG.info("Dumping all issues:");
    for (Issue issue : allIssues(orchestrator)) {
      LOG.info("  Key: " + issue.getKey() + "   Rule: " + issue.getRule() + "  Component:" + issue.getComponent());
    }
  }

  public static BuildResult executeEndStepAndDumpResults(Orchestrator orchestrator, Path projectDir, String projectKey, String token) {
    return executeEndStepAndDumpResults(orchestrator, projectDir, projectKey, token, ScannerClassifier.NET_FRAMEWORK, Collections.emptyList());
  }

  public static BuildResult executeEndStepAndDumpResults(Orchestrator orchestrator, Path projectDir, String projectKey, String token, List<EnvironmentVariable> environmentVariables) {
    return executeEndStepAndDumpResults(orchestrator, projectDir, projectKey, token, ScannerClassifier.NET_FRAMEWORK, environmentVariables);
  }

  public static BuildResult executeEndStepAndDumpResults(Orchestrator orchestrator,
    Path projectDir,
    String projectKey,
    String token,
    ScannerClassifier classifier,
    List<EnvironmentVariable> environmentVariables) {
    var endCommand = TestUtils.newScanner(orchestrator, projectDir, classifier, token)
      .setUseDotNetCore(classifier.isDotNetCore())
      .setScannerVersion(developmentScannerVersion())
      .addArgument("end");

    for (var pair : environmentVariables) {
      endCommand.setEnvironmentVariable(pair.getName(), pair.getValue());
    }

    BuildResult result = orchestrator.executeBuild(endCommand);

    if (result.isSuccess()) {
      TestUtils.dumpComponentList(orchestrator, projectKey);
      TestUtils.dumpAllIssues(orchestrator);
    } else {
      LOG.warn("End step was not successful - skipping dumping issues data");
    }

    return result;
  }

  public static List<Issue> issuesForComponent(Orchestrator orchestrator, String componentKey) {
    return newWsClient(orchestrator)
      .issues()
      .search(new org.sonarqube.ws.client.issues.SearchRequest().setComponentKeys(Collections.singletonList(componentKey)))
      .getIssuesList();
  }

  public static List<Issue> allIssues(Orchestrator orchestrator) {
    return newWsClient(orchestrator)
      .issues()
      .search(new org.sonarqube.ws.client.issues.SearchRequest())
      .getIssuesList();
  }

  public static String getDefaultBranchName(Orchestrator orchestrator) {
    return orchestrator.getServer().version().isGreaterThanOrEquals(9, 8) ? "main" : "master";
  }

  public static WsClient newWsClient(Orchestrator orchestrator) {
    return WsClientFactories.getDefault().newClient(HttpConnector.newBuilder()
      .url(orchestrator.getServer().getUrl())
      .token(getNewToken(orchestrator))
      .build());
  }

  public static WsClient newAdminWsClient(Orchestrator orchestrator) {
    return WsClientFactories.getDefault().newClient(HttpConnector.newBuilder()
      .url(orchestrator.getServer().getUrl())
      .credentials("admin", "admin")
      .build());
  }

  public static String getNewToken(Orchestrator orchestrator) {
    if (token == null) {
      token = newAdminWsClient(orchestrator).userTokens().generate(new GenerateRequest().setName("its")).getToken();
    }
    return token;
  }

  public static boolean hasModules(Orchestrator orch) {
    return !orch.getServer().version().isGreaterThanOrEquals(7, 6);
  }

  @CheckForNull
  public static Integer getMeasureAsInteger(String componentKey, String metricKey, Orchestrator orchestrator) {
    Measures.Measure measure = getMeasure(componentKey, metricKey, orchestrator);

    Integer result = (measure == null) ? null : Integer.parseInt(measure.getValue());
    LOG.info("Component: " + componentKey +
      "  metric key: " + metricKey +
      "  value: " + result);

    return result;
  }

  // The (?s) flag indicates that the dot special character ( . ) should additionally match the following
  // line terminator ("newline") characters in a string, which it would not match otherwise.
  public static void matchesSingleLine(String input, String pattern) {
    assertThat(input).matches("(?s).*"+pattern+".*");
  }

  @CheckForNull
  private static Measures.Measure getMeasure(@Nullable String componentKey, String metricKey, Orchestrator orchestrator) {
    Measures.ComponentWsResponse response = newWsClient(orchestrator).measures().component(new ComponentRequest()
      .setComponent(componentKey)
      .setMetricKeys(Collections.singletonList(metricKey)));
    List<Measures.Measure> measures = response.getComponent().getMeasuresList();
    return measures.size() == 1 ? measures.get(0) : null;
  }

  public static Ce.Task getAnalysisWarningsTask(Orchestrator orchestrator, BuildResult buildResult) {
    String taskId = extractCeTaskId(buildResult);
    return newWsClient(orchestrator)
      .ce()
      .task(new TaskRequest().setId(taskId).setAdditionalFields(Collections.singletonList("warnings")))
      .getTask();
  }

  private static String extractCeTaskId(BuildResult buildResult) {
    List<String> taskIds = extractCeTaskIds(buildResult);
    if (taskIds.size() != 1) {
      throw new IllegalStateException("More than one task id retrieved from logs.");
    }
    return taskIds.iterator().next();
  }

  private static List<String> extractCeTaskIds(BuildResult buildResult) {
    // The log looks like this:
    // INFO: More about the report processing at http://127.0.0.1:53395/api/ce/task?id=0f639b4c-6421-4620-81d0-eac0f5759f06
    return buildResult.getLogsLines(s -> s.contains("More about the report processing at")).stream()
      .map(s -> s.substring(s.lastIndexOf("=") + 1))
      .collect(Collectors.toList());
  }
}
