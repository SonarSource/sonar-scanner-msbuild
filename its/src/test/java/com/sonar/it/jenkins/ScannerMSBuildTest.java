/*
 * Jenkins :: Integration Tests
 * Copyright (C) 2013 ${owner}
 * sonarqube@googlegroups.com
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
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */
package com.sonar.it.jenkins;

import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.build.ScannerForMSBuild;
import com.sonar.orchestrator.config.Configuration;
import com.sonar.orchestrator.junit.SingleStartExternalResource;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.locator.Locators;
import com.sonar.orchestrator.locator.MavenLocation;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import java.io.File;
import java.io.IOException;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.nio.file.FileSystem;
import java.nio.file.FileSystems;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.nio.file.StandardCopyOption;
import java.util.HashMap;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import org.apache.commons.io.FileUtils;
import org.junit.ClassRule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.sonar.wsclient.issue.IssueQuery;

import static org.assertj.core.api.Assertions.assertThat;

public class ScannerMSBuildTest {

  private final static Logger LOG = LoggerFactory.getLogger(ScannerMSBuildTest.class);

  private static final String MSBUILD_HOME = "msbuild.home";

  @ClassRule
  public static TemporaryFolder temp = new TemporaryFolder();

  public static Orchestrator ORCHESTRATOR;

  @ClassRule
  public static SingleStartExternalResource resource = new SingleStartExternalResource() {

    @Override
    protected void beforeAll() {
      File modifiedCs = prepareCSharpPlugin();
      ORCHESTRATOR = Orchestrator.builderEnv().addPlugin(FileLocation.of(modifiedCs)).build();
      ORCHESTRATOR.start();
    }

    private File prepareCSharpPlugin() {
      Configuration configuration = Orchestrator.builderEnv().build().getConfiguration();
      Locators locators = new Locators(configuration);
      MavenLocation csharp = MavenLocation.create("org.sonarsource.dotnet", "sonar-csharp-plugin", configuration.getString("csharpPlugin.version", "5.1"));
      File modifiedCs;
      try {
        modifiedCs = temp.newFile("modified-chsarp.jar");
      } catch (IOException e) {
        throw new IllegalStateException(e);
      }
      locators.copyToFile(csharp, modifiedCs);

      String scannerVersion;
      String buildOnQa = System.getenv("CI_BUILD_NUMBER");
      if (buildOnQa != null) {
        scannerVersion = parseVersion() + "-build" + buildOnQa;
      } else {
        scannerVersion = configuration.getString("scannerForMSBuild.version");
      }
      Path scannerImpl;
      if (scannerVersion != null) {
        LOG.info("Update C# plugin with Scanner For MSBuild implementation " + scannerVersion);
        MavenLocation scannerImplLocation = MavenLocation.builder().setGroupId("org.sonarsource.scanner.msbuild").setArtifactId("sonar-scanner-msbuild").setVersion(scannerVersion)
          .setClassifier("impl").withPackaging("zip").build();
        try {
          scannerImpl = temp.newFile("sonar-scanner-msbuild-impl.zip").toPath();
        } catch (IOException e) {
          throw new IllegalStateException(e);
        }
        locators.copyToFile(scannerImplLocation, scannerImpl.toFile());
      } else {
        // Run locally
        LOG.info("Update C# plugin with local build of Scanner For MSBuild implementation");
        scannerImpl = Paths.get("../DeploymentArtifacts/CSharpPluginPayload/Release/SonarQube.MSBuild.Runner.Implementation.zip");
      }

      replaceInZip(modifiedCs.toURI(), scannerImpl, "/static/SonarQube.MSBuild.Runner.Implementation.zip");
      return modifiedCs;
    }

    @Override
    protected void afterAll() {
      ORCHESTRATOR.stop();
    }
  };

  @Test
  public void testSample() throws Exception {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject("foo", "Foo");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile("foo", "cs", "ProfileForTest");

    File projectDir = projectDir("projects/ProjectUnderTest");
    ORCHESTRATOR.executeBuild(ScannerForMSBuild.create(projectDir)
      .addArgument("begin")
      .setProjectKey("foo")
      .setProjectName("Foo")
      .setProjectVersion("1.0"));

    runMSBuild(projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(ScannerForMSBuild.create(projectDir)
      .addArgument("end"));

    assertThat(ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list()).hasSize(4);

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

  public static void main(String[] args) {
    System.out.println(parseVersion("using System.Runtime.InteropServices;\n\n[assembly: AssemblyVersion(\"2.1.0.0\")]\n[assembly: AssemblyFileVersion(\"2.1.0.0\")]"));
  }

  private File projectDir(String dir) throws IOException {
    File projectDir = new File(dir);
    File tmpProjectDir = temp.newFolder();
    FileUtils.copyDirectory(projectDir, tmpProjectDir);
    return tmpProjectDir;
  }

  private void runMSBuild(File projectDir, String... arguments) {
    String msBuildPath = ORCHESTRATOR.getConfiguration().getString(MSBUILD_HOME, "C:\\Program Files (x86)\\MSBuild\\14.0");
    Path msBuildHome = Paths.get(msBuildPath).toAbsolutePath();
    if (!Files.exists(msBuildHome)) {
      throw new IllegalStateException("Unable to find MSBuild at " + msBuildHome.toString() + ". Please configure property '" + MSBUILD_HOME + "'");
    }

    int r = CommandExecutor.create().execute(Command.create(msBuildHome.resolve("bin/MSBuild.exe").toString())
      .addArguments(arguments)
      .setDirectory(projectDir), 60 * 1000);
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
      Files.copy(src, pathInZipfile, StandardCopyOption.REPLACE_EXISTING);
    } catch (IOException e) {
      throw new IllegalStateException(e);
    }
  }

}
