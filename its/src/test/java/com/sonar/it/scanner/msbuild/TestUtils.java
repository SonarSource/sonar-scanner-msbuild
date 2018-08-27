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
import com.sonar.orchestrator.config.Configuration;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.locator.Location;
import com.sonar.orchestrator.locator.MavenLocation;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import com.sonar.orchestrator.util.StreamConsumer;
import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Set;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.stream.Collectors;
import javax.annotation.CheckForNull;
import org.apache.commons.io.FileUtils;
import org.junit.rules.TemporaryFolder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.sonarqube.ws.WsComponents;
import org.sonarqube.ws.client.HttpConnector;
import org.sonarqube.ws.client.WsClient;
import org.sonarqube.ws.client.WsClientFactories;
import org.sonarqube.ws.client.component.SearchWsRequest;
import sun.rmi.runtime.Log;

import static org.assertj.core.api.Assertions.assertThat;

public class TestUtils {
  private final static Logger LOG = LoggerFactory.getLogger(ScannerMSBuildTest.class);

  @CheckForNull
  public static String getScannerVersion(Orchestrator orchestrator) {
    return orchestrator.getConfiguration().getString("scannerForMSBuild.version");
  }

  private static MavenLocation mavenLocation(String scannerVersion) {
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
      scannerLocation = mavenLocation(scannerVersion);
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

    return jars.get(0);
  }

  public static TemporaryFolder createTempFolder() {
    // If the test is being run under VSTS then the Scanner will
    // expect the project to be under the VSTS sources directory
    File baseDirectory = null;
    if (VstsUtils.isRunningUnderVsts()){
      String vstsSourcePath = VstsUtils.getSourcesDirectory();
      LOG.info("Tests are running under VSTS. Build dir:  " + vstsSourcePath);
      baseDirectory = new File(vstsSourcePath);
    }
    else {
      LOG.info("Tests are not running under VSTS");
    }
    return new TemporaryFolder(baseDirectory);
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

  private static Path getMsBuildPath(Orchestrator orch) {
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

  static WsClient newWsClient(Orchestrator orchestrator) {
    return WsClientFactories.getDefault().newClient(HttpConnector.newBuilder()
      .url(orchestrator.getServer().getUrl())
      .build());
  }
}
