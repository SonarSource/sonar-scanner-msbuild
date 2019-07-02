/*
 * Scanner for MSBuild :: Integration Tests
 * Copyright (C) 2016-2019 SonarSource SA
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
import com.sonar.orchestrator.locator.FileLocation;
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
import java.util.Collections;
import java.util.List;
import java.util.Set;
import java.util.stream.Collectors;
import javax.annotation.CheckForNull;
import javax.annotation.Nullable;
import org.apache.commons.io.FileUtils;
import org.junit.rules.TemporaryFolder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.sonarqube.ws.Issues.Issue;
import org.sonarqube.ws.WsComponents;
import org.sonarqube.ws.WsMeasures;
import org.sonarqube.ws.client.HttpConnector;
import org.sonarqube.ws.client.WsClient;
import org.sonarqube.ws.client.WsClientFactories;
import org.sonarqube.ws.client.component.SearchWsRequest;
import org.sonarqube.ws.client.measure.ComponentWsRequest;

import static org.assertj.core.api.Assertions.assertThat;

public class TestUtils {
  final static Logger LOG = LoggerFactory.getLogger(ScannerMSBuildTest.class);

  private static final String NUGET_PATH = "NUGET_PATH";

  @CheckForNull
  public static String getScannerVersion(Orchestrator orchestrator) {
    return orchestrator.getConfiguration().getString("scannerForMSBuild.version");
  }

  private static MavenLocation getScannerMavenLocation(String scannerVersion) {
    String groupId = "org.sonarsource.scanner.msbuild";
    String artifactId = "sonar-scanner-msbuild";
    return MavenLocation.builder()
      .setGroupId(groupId)
      .setArtifactId(artifactId)
      .setVersion(scannerVersion)
      .setClassifier("net46")
      .withPackaging("zip")
      .build();
  }

  public static ScannerForMSBuild newScanner(Orchestrator orchestrator, Path projectDir) {
    String scannerVersion = getScannerVersion(orchestrator);

    Location scannerLocation;
    if (scannerVersion != null) {
      LOG.info("Using Scanner for MSBuild " + scannerVersion);
      scannerLocation = getScannerMavenLocation(scannerVersion);
    }
    else {
      String scannerLocationEnv = System.getenv("SCANNER_LOCATION");
      if(scannerLocationEnv != null) {
        LOG.info("Using Scanner for MSBuild specified by %SCANNER_LOCATION%: " + scannerLocationEnv);
        Path scannerPath = Paths.get(scannerLocationEnv, "sonarscanner-msbuild-net46.zip");
        scannerLocation = FileLocation.of(scannerPath.toFile());
      }
      else {
        // run locally
        LOG.info("Using Scanner for MSBuild from the local build");
        scannerLocation = FindScannerZip("../DeploymentArtifacts/BuildAgentPayload/Release");
      }
    }

    LOG.info("Scanner location: " + scannerLocation);
    return ScannerForMSBuild.create(projectDir.toFile())
      .setScannerLocation(scannerLocation);
  }

  private static Location FindScannerZip(String folderPath){
    Path root = Paths.get(folderPath);
    Path scannerZip = Paths.get(folderPath + "/sonarscanner-msbuild-net46.zip");
    Location scannerLocation = FileLocation.of(scannerZip.toFile());
    return scannerLocation;
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
      throw new IllegalStateException("No jars found in " + customPluginDir.toString());
    } else if (jars.size() > 1) {
      throw new IllegalStateException("Several jars found in " + customPluginDir.toString());
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

  public static TemporaryFolder createTempFolder() {
    LOG.info("TEST SETUP: creating temporary folder...");

    // If the test is being run under VSTS then the Scanner will
    // expect the project to be under the VSTS sources directory
    File baseDirectory = null;
    if (VstsUtils.isRunningUnderVsts()){
      String vstsSourcePath = VstsUtils.getSourcesDirectory();
      LOG.info("TEST SETUP: Tests are running under VSTS. Build dir:  " + vstsSourcePath);
      baseDirectory = new File(vstsSourcePath);
    }
    else {
      LOG.info("TEST SETUP: Tests are not running under VSTS");
    }
    TemporaryFolder folder = new TemporaryFolder(baseDirectory);
    LOG.info("TEST SETUP: Temporary folder created. Base directory: " + baseDirectory);
    return folder;
  }

  public static Path projectDir(TemporaryFolder temp, String projectName) throws IOException {
    Path projectDir = Paths.get("projects").resolve(projectName);
    FileUtils.deleteDirectory(new File(temp.getRoot(), projectName));
    Path tmpProjectDir = Paths.get(temp.newFolder(projectName).getCanonicalPath());
    FileUtils.copyDirectory(projectDir.toFile(), tmpProjectDir.toFile());
    return tmpProjectDir;
  }

  public static void runMSBuildWithBuildWrapper(Orchestrator orch, Path projectDir, File buildWrapperPath, File outDir,
    String... arguments) {
    Path msBuildPath = getMsBuildPath(orch);

    int r = CommandExecutor.create().execute(Command.create(buildWrapperPath.toString())
      .addArgument("--out-dir")
      .addArgument(outDir.toString())
      .addArgument(msBuildPath.toString())
      .addArguments(arguments)
      .setDirectory(projectDir.toFile()), 60 * 1000);
    assertThat(r).isEqualTo(0);
  }

  public static void runMSBuild(Orchestrator orch, Path projectDir, String... arguments) {
    BuildResult r = runMSBuildQuietly(orch, projectDir, arguments);
    assertThat(r.isSuccess()).isTrue();
  }

  // Versions of SonarQube and plugins support aliases:
  // - "DEV" for the latest build of master that passed QA
  // - "DEV[1.0]" for the latest build that passed QA of series 1.0.x
  // - "LATEST_RELEASE" for the latest release
  // - "LATEST_RELEASE[1.0]" for latest release of series 1.0.x
  // The SonarQube alias "LTS" has been dropped. An alternative is "LATEST_RELEASE[6.7]".
  // The term "latest" refers to the highest version number, not the most recently published version.
  public static String replaceLtsVersion(String version) {
    if (version != null && version.equals("LTS"))
    {
      return "LATEST_RELEASE[7.9]";
    }
    return version;
  }

  public static void runNuGet(Orchestrator orch, Path projectDir, String... arguments) {
    Path nugetPath = getNuGetPath(orch);

    int r = CommandExecutor.create().execute(Command.create(nugetPath.toString())
      .addArguments(arguments)
      .addArguments("-MSBuildPath", TestUtils.getMsBuildPath(orch).getParent().toString())
      .setDirectory(projectDir.toFile()), 60 * 1000);
    assertThat(r).isEqualTo(0);
  }

  private static Path getNuGetPath(Orchestrator orch) {
    LOG.info("TEST SETUP: calculating path to NuGet.exe...");
    String toolsFolder = Paths.get("tools").resolve("nuget.exe").toAbsolutePath().toString();
    String nugetPathStr = orch.getConfiguration().getString(NUGET_PATH, toolsFolder);
    Path nugetPath = Paths.get(nugetPathStr).toAbsolutePath();
    if (!Files.exists(nugetPath)) {
      throw new IllegalStateException("Unable to find NuGet at '" + nugetPath.toString() +
        "'. Please configure property '" + NUGET_PATH + "'");
    }

    LOG.info("TEST SETUP: nuget.exe path = " + nugetPath);
    return nugetPath;
  }

  private static BuildResult runMSBuildQuietly(Orchestrator orch, Path projectDir, String... arguments) {
    Path msBuildPath = getMsBuildPath(orch);

    BuildResult result = new BuildResult();
    StreamConsumer.Pipe writer = new StreamConsumer.Pipe(result.getLogsWriter());
    int status = CommandExecutor.create().execute(Command.create(msBuildPath.toString())
      .addArguments(arguments)
      .setDirectory(projectDir.toFile()), writer, 60 * 1000);

    result.addStatus(status);
    return result;
  }

  static Path getMsBuildPath(Orchestrator orch) {
    String msBuildPathStr = orch.getConfiguration().getString("msbuild.path",
      orch.getConfiguration().getString("MSBUILD_PATH", "C:\\Program Files (x86)\\Microsoft Visual "
        + "Studio\\2017\\Enterprise\\MSBuild\\15.0\\Bin\\MSBuild.exe"));
    Path msBuildPath = Paths.get(msBuildPathStr).toAbsolutePath();
    if (!Files.exists(msBuildPath)) {
      throw new IllegalStateException("Unable to find MSBuild at " + msBuildPath.toString()
        + ". Please configure property 'msbuild.path' or 'MSBUILD_PATH'.");
    }
    return msBuildPath;
  }

  static void dumpComponentList(Orchestrator orchestrator)
  {
    Set<String> componentKeys = newWsClient(orchestrator)
      .components()
      .search(new SearchWsRequest().setLanguage("cs").setQualifiers(Collections.singletonList("FIL")))
      .getComponentsList()
      .stream()
      .map(WsComponents.Component::getKey)
      .collect(Collectors.toSet());

    LOG.info("Dumping C# component keys:");
    for(String key: componentKeys) {
      LOG.info("  Key: " + key);
    }
  }

  public static List<Issue> issuesForComponent(Orchestrator orchestrator, String componentKey) {
    return newWsClient(orchestrator)
      .issues()
      .search(new org.sonarqube.ws.client.issue.SearchWsRequest().setComponentKeys(Collections.singletonList(componentKey)))
      .getIssuesList();
  }

  public static List<Issue> allIssues(Orchestrator orchestrator) {
    return newWsClient(orchestrator)
      .issues()
      .search(new org.sonarqube.ws.client.issue.SearchWsRequest())
      .getIssuesList();
  }

  static WsClient newWsClient(Orchestrator orchestrator) {
    return WsClientFactories.getDefault().newClient(HttpConnector.newBuilder()
      .url(orchestrator.getServer().getUrl())
      .build());
  }

  public static boolean hasModules(Orchestrator orch) {
    return !orch.getServer().version().isGreaterThanOrEquals(7, 6);
  }

  @CheckForNull
  public static Integer getMeasureAsInteger(String componentKey, String metricKey, Orchestrator orchestrator) {
    WsMeasures.Measure measure = getMeasure(componentKey, metricKey, orchestrator);

    Integer result = (measure == null) ? null : Integer.parseInt(measure.getValue());
    LOG.info("Component: " + componentKey + 
              "  metric key: " + metricKey + 
              "  value: " + result);

    return result;
  }

  @CheckForNull
  private static WsMeasures.Measure getMeasure(@Nullable String componentKey, String metricKey, Orchestrator orchestrator) {
    WsMeasures.ComponentWsResponse response = newWsClient(orchestrator).measures().component(new ComponentWsRequest()
      .setComponentKey(componentKey)
      .setMetricKeys(Collections.singletonList(metricKey)));
    List<WsMeasures.Measure> measures = response.getComponent().getMeasuresList();
    return measures.size() == 1 ? measures.get(0) : null;
  }  
}
