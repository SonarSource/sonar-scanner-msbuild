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
import com.sonar.orchestrator.build.ScannerForMSBuild;
import com.sonar.orchestrator.config.Configuration;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import javax.annotation.CheckForNull;
import org.apache.commons.io.FileUtils;
import org.junit.rules.TemporaryFolder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static org.assertj.core.api.Assertions.assertThat;

public class TestUtils {
  public static final String MSBUILD_PATH = "msbuild.path";
  private final static Logger LOG = LoggerFactory.getLogger(ScannerMSBuildTest.class);

  @CheckForNull
  public static String getScannerVersion() {
    Configuration configuration = Orchestrator.builderEnv().build().getConfiguration();
    return configuration.getString("scannerForMSBuild.version");
  }

  public static ScannerForMSBuild newScanner(Path projectDir) {
    String scannerVersion = getScannerVersion();

    if (scannerVersion != null) {
      LOG.info("Using Scanner for MSBuild " + scannerVersion);
      return ScannerForMSBuild.create(projectDir.toFile())
        .setScannerVersion(scannerVersion);
    } else {
      // run locally
      LOG.info("Using Scanner for MSBuild from the local build");
      Path scannerZip = Paths.get("../DeploymentArtifacts/BuildAgentPayload/Release/SonarQube.Scanner.MSBuild.zip");
      return ScannerForMSBuild.create(projectDir.toFile())
        .setScannerLocation(FileLocation.of(scannerZip.toFile()));
    }
  }

  public static Path getCustomRoslynPlugin() {
    Path customPluginDir = Paths.get("").resolve("analyzers");

    DirectoryStream.Filter<Path> jarFilter = new DirectoryStream.Filter<Path>() {
      public boolean accept(Path file) throws IOException {
        return Files.isRegularFile(file) && file.toString().endsWith(".jar");
      }
    };
    List<Path> jars = new ArrayList<>();
    try {
      Files.newDirectoryStream(customPluginDir, jarFilter).forEach(p -> jars.add(p));
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

  public static Path projectDir(TemporaryFolder temp, String projectName) throws IOException {
    Path projectDir = Paths.get("projects").resolve(projectName);
    FileUtils.deleteDirectory(new File(temp.getRoot(), projectName));
    Path tmpProjectDir = temp.newFolder(projectName).toPath();
    FileUtils.copyDirectory(projectDir.toFile(), tmpProjectDir.toFile());
    return tmpProjectDir;
  }

  public static void runMSBuildWithBuildWrapper(Orchestrator orch, Path projectDir, File buildWrapperPath, File outDir, String... arguments) {
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
    Path msBuildPath = getMsBuildPath(orch);

    int r = CommandExecutor.create().execute(Command.create(msBuildPath.toString())
      .addArguments(arguments)
      .setDirectory(projectDir.toFile()), 60 * 1000);
    assertThat(r).isEqualTo(0);
  }

  private static Path getMsBuildPath(Orchestrator orch) {
    String msBuildPathStr = orch.getConfiguration().getString(MSBUILD_PATH, "C:\\Program Files (x86)\\MSBuild\\14.0\\bin\\MSBuild.exe");
    Path msBuildPath = Paths.get(msBuildPathStr).toAbsolutePath();
    if (!Files.exists(msBuildPath)) {
      throw new IllegalStateException("Unable to find MSBuild at " + msBuildPath.toString() + ". Please configure property '" + MSBUILD_PATH + "'");
    }
    return msBuildPath;
  }

  protected static String parseVersion() {
    try {
      String content = FileUtils.readFileToString(new File("../AssemblyInfo.Shared.cs"), StandardCharsets.UTF_8);
      return parseVersion(content);
    } catch (IOException e) {
      throw new IllegalStateException(e);
    }
  }

  private static String parseVersion(String content) {
    Pattern p = Pattern.compile("(?s).*\\[assembly: AssemblyVersion\\(\"(.*?)\"\\)].*");
    Matcher matcher = p.matcher(content);
    if (matcher.matches()) {
      return matcher.group(1);
    }
    throw new IllegalStateException("Unable to parse version from " + content);
  }
}
