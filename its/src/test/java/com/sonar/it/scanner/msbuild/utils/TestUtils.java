/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

import com.sonar.it.scanner.msbuild.sonarqube.ServerTests;
import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.build.BuildResult;
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
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.Set;
import java.util.stream.Collectors;
import javax.annotation.CheckForNull;
import javax.annotation.Nullable;
import org.apache.commons.io.FileUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.sonar.api.internal.apachecommons.lang3.StringUtils;
import org.sonarqube.ws.Ce;
import org.sonarqube.ws.Components;
import org.sonarqube.ws.Issues;
import org.sonarqube.ws.Issues.Issue;
import org.sonarqube.ws.Measures;
import org.sonarqube.ws.client.HttpConnector;
import org.sonarqube.ws.client.WsClient;
import org.sonarqube.ws.client.WsClientFactories;
import org.sonarqube.ws.client.ce.TaskRequest;
import org.sonarqube.ws.client.components.ShowRequest;
import org.sonarqube.ws.client.components.TreeRequest;
import org.sonarqube.ws.client.measures.ComponentRequest;
import org.sonarqube.ws.client.settings.SetRequest;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;

public class TestUtils {
  final static Logger LOG = LoggerFactory.getLogger(TestUtils.class);

  private static final int MSBUILD_RETRY = 3;
  private static final String NUGET_PATH = "NUGET_PATH";

  public static final long TIMEOUT_LIMIT = 60 * 1000L;
  public static final String MSBUILD_DEFAULT_PATH = "C:\\Program Files\\Microsoft Visual Studio\\2022\\Professional\\MSBuild\\Current\\Bin\\MSBuild.exe";

  @CheckForNull
  public static String getAnalyzerVersion(Orchestrator orchestrator) {
    return orchestrator.getConfiguration().getString("DOTNET_VERSION");
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

  // ToDo: SCAN4NET-201: Remove this, after SCAN4NET-320 or SCAN4NET199 will stop using it
  public static ScannerCommand newScannerBegin(Orchestrator orchestrator, String projectKey, Path projectDir, String token) {
    // ToDo: Cleanup inconsistent "end" logic. For now, this defaults to "end" step and caller must override it
    return ScannerCommand.createBeginStep(ScannerClassifier.NET_FRAMEWORK, token, projectDir, projectKey);
  }

  // ToDo: SCAN4NET-201: Remove this, after SCAN4NET-320 or SCAN4NET199 will stop using it
  public static ScannerCommand newScannerBegin(Orchestrator orchestrator, String projectKey, Path projectDir, String token, ScannerClassifier classifier) {
    return ScannerCommand.createBeginStep(classifier, token, projectDir, projectKey);
  }

  // ToDo: SCAN4NET-201: Remove this, after SCAN4NET-320 or SCAN4NET199 will stop using it
  public static ScannerCommand newScannerEnd(Orchestrator orchestrator, Path projectDir, String token) {
    return ScannerCommand.createEndStep(ScannerClassifier.NET_FRAMEWORK, token, projectDir);
  }

  // ToDo: SCAN4NET-201: Remove this, after SCAN4NET-320 or SCAN4NET199 will stop using it
  public static ScannerCommand newScannerEnd(Orchestrator orchestrator, Path projectDir, ScannerClassifier classifier, String token) {
    return ScannerCommand.createEndStep(classifier, token, projectDir);
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
    var target = projectDir.resolve(subDirectory).toAbsolutePath().toString();
    // If SUBST fails, it's most likely flakiness from a previous canceled run that did not clean up the drive.
    GeneralCommand.create("SUBST", projectDir)
      .addArgument(drive)
      .addArgument(target)
      .execute();
  }

  public static void deleteVirtualDrive(String drive, Path projectDir) {
    GeneralCommand.create("SUBST", projectDir)
      .addArgument(drive).addArgument("/D")
      .execute();
  }

  public static Path projectDir(Path temp, String projectName) {
    try {
      File projectToCopy = Paths.get("projects").resolve(projectName).toFile();
      File destination = new File(temp.toFile(), projectName).getCanonicalFile();
      FileUtils.deleteDirectory(destination);
      Path newFolder = Files.createDirectories(destination.toPath());
      FileUtils.copyDirectory(projectToCopy, newFolder.toFile());
      Files.copy(Paths.get("..", "NuGet.Config"), newFolder.resolve("NuGet.Config"));
      return newFolder;
    } catch (IOException ex) {
      throw new RuntimeException(ex.getMessage(), ex);
    }
  }

  public static void updateSetting(Orchestrator orchestrator, String projectKey, String propertyKey, List<String> values) {
    newWsClient(orchestrator).settings().set(new SetRequest().setComponent(projectKey).setKey(propertyKey).setValues(values));
  }

  // ToDo: SCAN4NET-10 will move/deprecate/remove this in favor of BuildCommand
  public static void runMSBuild(Orchestrator orch, Path projectDir, String... arguments) {
    runMSBuild(orch, projectDir, Collections.emptyList(), TIMEOUT_LIMIT, arguments);
  }

  // ToDo: SCAN4NET-10 will move/deprecate/remove this in favor of BuildCommand
  public static BuildResult buildMSBuild(Orchestrator orchestrator, Path projectDir) {
    return runMSBuild(orchestrator, projectDir, Collections.emptyList(), TIMEOUT_LIMIT, "/t:Restore,Rebuild");
  }

  // ToDo: SCAN4NET-10 will move/deprecate/remove this in favor of BuildCommand
  public static BuildResult runMSBuild(Orchestrator orch, Path projectDir, List<EnvironmentVariable> environmentVariables, long timeoutLimit, String... arguments) {
    BuildResult r = runMSBuildQuietly(orch, projectDir, environmentVariables, timeoutLimit, arguments);
    assertThat(r.isSuccess()).isTrue();
    return r;
  }

  // ToDo: Move to AnalysisContext
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

  // ToDo: SCAN4NET-10 will move/deprecate/remove this in favor of BuildCommand
  public static BuildResult runDotnetCommand(Path workingDir, String dotnetCommand, String... arguments) {
    return runDotnetCommand(workingDir, Collections.emptyList(), dotnetCommand, arguments);
  }

  // ToDo: SCAN4NET-10 will move/deprecate/remove this in favor of BuildCommand
  public static BuildResult runDotnetCommand(Path workingDir, List<EnvironmentVariable> environmentVariables, String dotnetCommand, String... arguments) {
    var argumentList = new ArrayList<>(Arrays.asList(arguments));
    argumentList.add(0, dotnetCommand);
    argumentList.add("-warnaserror:AD0001");
    argumentList.add("-nodereuse:false"); // This is mandatory, otherwise process node locks the dlls in .sonarqube preventing the test to delete temp directory
    argumentList.add("--verbosity:minimal"); // Change to "detailed" if more information is needed

    var buildResult = new BuildResult();
    StreamConsumer.Pipe writer = new StreamConsumer.Pipe(buildResult.getLogsWriter());
    var command = Command.create("dotnet").addArguments(argumentList).setDirectory(workingDir.toFile());
    initCommandEnvironment(command, environmentVariables);
    LOG.info("Command line: {}", command.toCommandLine());
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

  // ToDo: SCAN4NET-10 will move/deprecate/remove this in favor of BuildCommand
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
    initCommandEnvironment(command, environmentVariables);
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

  public static Components.Component getComponent(String componentKey) {
    return newWsClient(ORCHESTRATOR).components().show(new ShowRequest().setComponent(componentKey)).getComponent();
  }

  public static List<Components.Component> listComponents(Orchestrator orchestrator, String projectKey) {
    return newWsClient(orchestrator)
      .components()
      .tree(new TreeRequest().setQualifiers(Collections.singletonList("FIL")).setComponent(projectKey))
      .getComponentsList();
  }

  public static void dumpComponentList(Orchestrator orchestrator, String projectKey) {
    Set<String> componentKeys = listComponents(orchestrator, projectKey).stream().map(Components.Component::getKey).collect(Collectors.toSet());
    LOG.info("Dumping C# component keys:");
    for (String key : componentKeys) {
      LOG.info("  Key: " + key);
    }
  }

  public static void dumpProjectIssues(Orchestrator orchestrator, String projectKey) {
    LOG.info("Dumping all issues:");
    for (Issue issue : projectIssues(orchestrator, projectKey)) {
      LOG.info("  Key: " + issue.getKey() + "   Rule: " + issue.getRule() + "  Component:" + issue.getComponent());
    }
  }

  @Deprecated // Use AnalysisContext instead
  public static BuildResult runAnalysis(Path projectDir, String projectKey, Boolean useNuGet) {
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    String folderName = projectDir.getFileName().toString();
    TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK).setProperty("sonar.sourceEncoding", "UTF-8").execute(ORCHESTRATOR);
    if (useNuGet) {
      TestUtils.runNuGet(ORCHESTRATOR, projectDir, false, "restore");
    }
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Restore,Rebuild", folderName + ".sln");
    return TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);
  }

  @Deprecated // Use AnalysisContext instead
  public static BuildResult executeEndStepAndDumpResults(Orchestrator orchestrator, Path projectDir, String projectKey, String token) {
    return executeEndStepAndDumpResults(orchestrator, projectDir, projectKey, token, ScannerClassifier.NET_FRAMEWORK, Collections.emptyList(), Collections.emptyList());
  }

  @Deprecated // Use AnalysisContext instead
  public static BuildResult executeEndStepAndDumpResults(Orchestrator orchestrator, Path projectDir, String projectKey, String token,
    List<EnvironmentVariable> environmentVariables) {
    return executeEndStepAndDumpResults(orchestrator, projectDir, projectKey, token, ScannerClassifier.NET_FRAMEWORK, environmentVariables, Collections.emptyList());
  }

  @Deprecated // Use AnalysisContext instead
  public static BuildResult executeEndStepAndDumpResults(
    Orchestrator orchestrator,
    Path projectDir,
    String projectKey,
    String token,
    List<EnvironmentVariable> environmentVariables,
    List<String> additionalProperties) {
    return executeEndStepAndDumpResults(orchestrator, projectDir, projectKey, token, ScannerClassifier.NET_FRAMEWORK, environmentVariables, additionalProperties);
  }

  @Deprecated // Use AnalysisContext instead
  public static BuildResult executeEndStepAndDumpResults(Orchestrator orchestrator,
    Path projectDir,
    String projectKey,
    String token,
    ScannerClassifier classifier,
    List<EnvironmentVariable> environmentVariables,
    List<String> additionalProperties) {
    var endCommand = TestUtils.newScannerEnd(orchestrator, projectDir, classifier, token);
    for (var environmentVariable : environmentVariables) {
      endCommand.setEnvironmentVariable(environmentVariable.name(), environmentVariable.value());
    }

    for (var property : additionalProperties) {
      var keyValue = property.split("=");
      var value = keyValue.length > 1 ? keyValue[1] : null;
      LOG.info("Setting property: {}={}", keyValue[0], value);
      endCommand.setProperty(keyValue[0], value);
    }

    BuildResult result = endCommand.execute(orchestrator);

    if (result.isSuccess()) {
      TestUtils.dumpComponentList(orchestrator, projectKey);
      TestUtils.dumpProjectIssues(orchestrator, projectKey);
    } else {
      LOG.warn("End step was not successful - skipping dumping issues data");
    }

    return result;
  }

  // This will return results for any component key when passed to the projectKey parameter
  public static List<Issue> projectIssues(Orchestrator orchestrator, String projectKey) {
    List<Issue> results = new ArrayList<>();
    Issues.SearchWsResponse issues;
    var client = newWsClient(orchestrator);
    var request = new org.sonarqube.ws.client.issues.SearchRequest().setComponentKeys(Collections.singletonList(projectKey));
    var page = 1;
    do {
      issues = client.issues().search(request.setP(String.valueOf(page)));
      results.addAll(issues.getIssuesList());
      page++;
    } while (results.size() < issues.getPaging().getTotal());
    if (!orchestrator.getServer().version().isGreaterThan(9, 9)) {
      // The filtering per component key does not work with SQ 9.9 and below
      // We get all issues and filter by hand instead
      results.removeIf(x -> !StringUtils.equalsAny(projectKey, x.getProject(), x.getComponent()));
    }
    return results;
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

  // ToDo: Remove this in SCAN4NET-320
  public static String getNewToken(Orchestrator orchestrator) {
    return ServerTests.token();
  }

  public static void deleteDirectory(Path directory) {
    // Some have Directory.Delete(directory, true), others have different mentality
    try {
      Files.walk(directory).sorted(Comparator.reverseOrder()).map(Path::toFile).forEach(File::delete);
    } catch (IOException ex) {
      throw new RuntimeException(ex.getMessage(), ex);
    }
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
    assertThat(input).matches("(?s).*" + pattern + ".*");
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

  // ToDo: SCAN4NET-10 will deprecate/remove this. By using AnalysisContext and its environment variable handling, this will not be needed anymore
  private static Command initCommandEnvironment(Command command, List<EnvironmentVariable> environmentVariables) {
    var buildDirectory = environmentVariables.stream().filter(x -> x.name() == AzureDevOps.AGENT_BUILDDIRECTORY).findFirst();
    if (buildDirectory.isPresent()) {
      LOG.info("TEST SETUP: AGENT_BUILDDIRECTORY was explicitly set to " + buildDirectory.get().value());
    } else {
      // If not set explicitly to simulate AZD environment, reset to "" so SonarQube.Integration.ImportBefore.targets can correctly compute SonarQubeTempPath
      command.setEnvironmentVariable(AzureDevOps.AGENT_BUILDDIRECTORY, "");
      LOG.info("TEST SETUP: Resetting AGENT_BUILDDIRECTORY for MsBuild");
    }
    for (EnvironmentVariable environmentVariable : environmentVariables) {
      command.setEnvironmentVariable(environmentVariable.name(), environmentVariable.value());
    }
    return command;
  }
}
