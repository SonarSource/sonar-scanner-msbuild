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
import com.sonar.orchestrator.locator.Locators;
import com.sonar.orchestrator.locator.MavenLocation;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import java.io.File;
import java.io.IOException;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.nio.file.DirectoryStream;
import java.nio.file.FileSystem;
import java.nio.file.FileSystems;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.nio.file.StandardCopyOption;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import javax.annotation.CheckForNull;

import org.apache.commons.io.FileUtils;
import org.junit.rules.TemporaryFolder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static org.assertj.core.api.Assertions.assertThat;

public class TestUtils {
  public static final String MSBUILD_HOME = "msbuild.home";
  private final static Logger LOG = LoggerFactory.getLogger(ScannerMSBuildTest.class);

  public static Path prepareCSharpPlugin(TemporaryFolder temp) {
    Path t;
    try {
      t = temp.newFolder("CSharpPlugin").toPath();
    } catch (IOException e) {
      throw new IllegalStateException(e);
    }
    Configuration configuration = Orchestrator.builderEnv().build().getConfiguration();
    Locators locators = new Locators(configuration);
    String pluginVersion = TestSuite.getCSharpVersion();
    MavenLocation csharp = MavenLocation.create("org.sonarsource.dotnet", "sonar-csharp-plugin", pluginVersion);
    Path modifiedCs = t.resolve("modified-chsarp.jar");
    locators.copyToFile(csharp, modifiedCs.toFile());

    String scannerPayloadVersion = getScannerPayloadVersion();

    Path scannerImpl;
    if (scannerPayloadVersion != null) {
      LOG.info("Updating C# plugin ({}) with Scanner For MSBuild implementation ({})", pluginVersion, scannerPayloadVersion);
      MavenLocation scannerImplLocation = MavenLocation.builder()
        .setGroupId("org.sonarsource.scanner.msbuild")
        .setArtifactId("sonar-scanner-msbuild")
        .setVersion(scannerPayloadVersion)
        .setClassifier("impl")
        .withPackaging("zip")
        .build();
      scannerImpl = t.resolve("sonar-scanner-msbuild-impl.zip");
      if (locators.copyToFile(scannerImplLocation, scannerImpl.toFile()) == null) {
        throw new IllegalStateException("Unable to find sonar-scanner-msbuild " + scannerPayloadVersion + " in local Maven repository");
      }
    } else {
      // Run locally
      LOG.info("Updating C# plugin ({}) with local build of Scanner For MSBuild implementation", pluginVersion);
      scannerImpl = Paths.get("../DeploymentArtifacts/CSharpPluginPayload/Release/SonarQube.MSBuild.Runner.Implementation.zip");
    }

    replaceInZip(modifiedCs.toUri(), scannerImpl, "/static/SonarQube.MSBuild.Runner.Implementation.zip");
    return modifiedCs;
  }

  @CheckForNull
  private static String getScannerVersion() {
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

  /**
   * Try to get version of the Scanner for MSBuild to embed in the C# plugin (use as payload).
   * If no version is found, null is returned.
   */
  @CheckForNull
  public static String getScannerPayloadVersion() {
    Configuration configuration = Orchestrator.builderEnv().build().getConfiguration();
    return configuration.getString("scannerForMSBuildPayload.version");
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

  public static void runMSBuild(Orchestrator orch, Path projectDir, String... arguments) {
    String msBuildHome = orch.getConfiguration().getString(MSBUILD_HOME, "C:\\Program Files (x86)\\MSBuild\\14.0");
    Path msBuildPath = Paths.get(msBuildHome).toAbsolutePath();
    if (!Files.exists(msBuildPath)) {
      throw new IllegalStateException("Unable to find MSBuild at " + msBuildPath.toString() + ". Please configure property '" + MSBUILD_HOME + "'");
    }

    int r = CommandExecutor.create().execute(Command.create(msBuildPath.resolve("bin/MSBuild.exe").toString())
      .addArguments(arguments)
      .setDirectory(projectDir.toFile()), 60 * 1000);
    assertThat(r).isEqualTo(0);
  }

  private static void replaceInZip(URI zipUri, Path src, String dest) {
    Map<String, String> env = new HashMap<>();
    env.put("create", "true");
    // locate file system by using the syntax
    // defined in java.net.JarURLConnection
    URI uri = URI.create("jar:" + zipUri);
    try (FileSystem zipfs = FileSystems.newFileSystem(uri, env)) {
      Path pathInZipfile = zipfs.getPath(dest);
      LOG.info("Replacing " + pathInZipfile + " in " + src);
      Files.copy(src, pathInZipfile, StandardCopyOption.REPLACE_EXISTING);
    } catch (IOException e) {
      throw new IllegalStateException(e);
    }
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
