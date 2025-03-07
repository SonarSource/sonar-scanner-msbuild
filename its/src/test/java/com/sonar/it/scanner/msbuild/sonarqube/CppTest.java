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
package com.sonar.it.scanner.msbuild.sonarqube;

import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.util.ZipUtils;
import java.io.File;
import java.net.URL;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.List;
import java.util.stream.Collectors;
import org.apache.commons.io.FileUtils;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.sonarqube.ws.Issues.Issue;

import static com.sonar.it.scanner.msbuild.sonarqube.Tests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;

/**
 * Only cpp, without C# plugin
 */
// See task https://github.com/SonarSource/sonar-scanner-msbuild/issues/789
@ExtendWith(Tests.class)
class CppTest {

  @TempDir
  public Path basePath;

  @Test
  void testCppOnly() throws Exception {
    String projectKey = "cpp";
    String fileKey = TestUtils.hasModules(ORCHESTRATOR) ? "cpp:cpp:A8B8B694-4489-4D82-B9A0-7B63BF0B8FCE:ConsoleApp.cpp" : "cpp:ConsoleApp.cpp";

    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("src/test/resources/TestQualityProfileCpp.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKey, "Cpp");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cpp", "ProfileForTestCpp");

    Path projectDir = TestUtils.projectDir(basePath, "CppSolution");
    File wrapperOutDir = new File(projectDir.toFile(), "out");

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(projectKey)
      .setProjectName("Cpp")
      .setProjectVersion("1.0")
      .setProperty("sonar.cfamily.build-wrapper-output", wrapperOutDir.toString())
      .setProperty("sonar.projectBaseDir", Paths.get(projectDir.toAbsolutePath().toString(), "ConsoleApp").toString())
      .execute(ORCHESTRATOR);

    File buildWrapperZip = new File(basePath.toString(), "build-wrapper-win-x86.zip");
    File buildWrapperDir = basePath.toFile();
    FileUtils.copyURLToFile(new URL(ORCHESTRATOR.getServer().getUrl() + "/static/cpp/build-wrapper-win-x86.zip"), buildWrapperZip);
    ZipUtils.unzip(buildWrapperZip, buildWrapperDir);

    String platformToolset = System.getProperty("msbuild.platformtoolset", "v140");
    String windowsSdk = System.getProperty("msbuild.windowssdk", "10.0.18362.0");

    TestUtils.runMSBuildWithBuildWrapper(ORCHESTRATOR, projectDir, new File(buildWrapperDir, "build-wrapper-win-x86/build-wrapper-win-x86-64.exe"),
      wrapperOutDir, "/t:Rebuild",
      String.format("/p:WindowsTargetPlatformVersion=%s", windowsSdk),
      String.format("/p:PlatformToolset=%s", platformToolset));

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);
    assertThat(result.isSuccess()).isTrue();
    assertThat(result.getLogs()).doesNotContain("Invalid character encountered in file");

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, projectKey);

    List<String> keys = issues.stream().map(Issue::getRule).collect(Collectors.toList());
    assertThat(keys).containsAll(List.of("cpp:S106"));

    assertThat(TestUtils.getMeasureAsInteger(projectKey, "ncloc", ORCHESTRATOR)).isEqualTo(15);
    assertThat(TestUtils.getMeasureAsInteger(fileKey, "ncloc", ORCHESTRATOR)).isEqualTo(8);
  }

  @Test
  void testCppWithSharedFiles() throws Exception {
    String projectKey = "cpp-shared";
    String fileKey = TestUtils.hasModules(ORCHESTRATOR) ? "cpp-shared:cpp-shared:90BD7FAF-0B72-4D37-9610-D7C92B217BB0:Project1.cpp" : "cpp-shared:Project1/Project1.cpp";

    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("src/test/resources/TestQualityProfileCpp.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKey, "Cpp");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cpp", "ProfileForTestCpp");

    Path projectDir = TestUtils.projectDir(basePath, "CppSharedFiles");
    File wrapperOutDir = new File(projectDir.toFile(), "out");

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(projectKey)
      .setProjectName("Cpp")
      .setProjectVersion("1.0")
      .setProperty("sonar.cfamily.build-wrapper-output", wrapperOutDir.toString())
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .execute(ORCHESTRATOR);

    File buildWrapperZip = new File(basePath.toString(), "build-wrapper-win-x86.zip");
    File buildWrapperDir = Files.createDirectories(basePath).toFile();
    FileUtils.copyURLToFile(new URL(ORCHESTRATOR.getServer().getUrl() + "/static/cpp/build-wrapper-win-x86.zip"), buildWrapperZip);
    ZipUtils.unzip(buildWrapperZip, buildWrapperDir);

    String platformToolset = System.getProperty("msbuild.platformtoolset", "v140");
    String windowsSdk = System.getProperty("msbuild.windowssdk", "10.0.18362.0");

    TestUtils.runMSBuildWithBuildWrapper(ORCHESTRATOR, projectDir, new File(buildWrapperDir, "build-wrapper-win-x86/build-wrapper-win-x86-64.exe"),
      wrapperOutDir, "/t:Rebuild",
      String.format("/p:WindowsTargetPlatformVersion=%s", windowsSdk),
      String.format("/p:PlatformToolset=%s", platformToolset));

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);
    assertThat(result.isSuccess()).isTrue();
    assertThat(result.getLogs()).doesNotContain("Invalid character encountered in file");

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, projectKey);

    List<String> keys = issues.stream().map(Issue::getRule).collect(Collectors.toList());
    assertThat(keys).containsAll(List.of("cpp:S106"));

    assertThat(TestUtils.getMeasureAsInteger(projectKey, "ncloc", ORCHESTRATOR)).isEqualTo(22);
    assertThat(TestUtils.getMeasureAsInteger(fileKey, "ncloc", ORCHESTRATOR)).isEqualTo(8);
  }
}
