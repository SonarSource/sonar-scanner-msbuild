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

import com.sonar.it.scanner.msbuild.utils.AnalysisContext;
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.TempDirectory;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.util.ZipUtils;
import java.io.File;
import java.io.IOException;
import java.net.URL;
import java.util.List;
import org.apache.commons.io.FileUtils;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.sonarqube.ws.Issues.Issue;
import com.sonar.it.scanner.msbuild.utils.QualityProfiles;
import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;

/**
 * Only cpp, without C# plugin
 */
// See task https://github.com/SonarSource/sonar-scanner-msbuild/issues/789
@ExtendWith({ServerTests.class, ContextExtension.class})
class CppTest {

  @Test
  void testCppOnly() throws Exception {
    var context = AnalysisContext.forServer("CppSolution");

    ORCHESTRATOR.getServer().provisionProject(context.projectKey, context.projectKey);
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(context.projectKey, "cpp", QualityProfiles.CPP_S106);

    File wrapperOutDir = new File(context.projectDir.toFile(), "out");

    var beginResult = context.begin
      .setProperty("sonar.cfamily.build-wrapper-output", wrapperOutDir.toString())
      .execute(ORCHESTRATOR);
    assertThat(beginResult.isSuccess()).describedAs("C++ begin step failed with logs %n%s", beginResult.getLogs()). isTrue();

    String platformToolset = System.getProperty("msbuild.platformtoolset", "v140");
    String windowsSdk = System.getProperty("msbuild.windowssdk", "10.0.18362.0");
    try (var buildWrapperDir = getBuildWrapperDir(context)) {
      TestUtils.runMSBuildWithBuildWrapper(ORCHESTRATOR, context.projectDir, buildWrapperDir.path.resolve("build-wrapper-win-x86/build-wrapper-win-x86-64.exe").toFile(),
        wrapperOutDir, "/t:Rebuild",
        String.format("/p:WindowsTargetPlatformVersion=%s", windowsSdk),
        String.format("/p:PlatformToolset=%s", platformToolset));
      BuildResult result = context.end.execute(ORCHESTRATOR);
      assertThat(result.isSuccess()).as(result.getLogs()).isTrue();
      assertThat(result.getLogs()).doesNotContain("Invalid character encountered in file");

      List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
      assertThat(issues).extracting(Issue::getRule).containsAll(List.of("cpp:S106"));
      assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "ncloc", ORCHESTRATOR)).isEqualTo(15);
      assertThat(TestUtils.getMeasureAsInteger(context.projectKey + ":ConsoleApp/ConsoleApp.cpp", "ncloc", ORCHESTRATOR)).isEqualTo(8);
    }
  }

  @Test
  void testCppWithSharedFiles() throws Exception {
    var context = AnalysisContext.forServer("CppSharedFiles");

    ORCHESTRATOR.getServer().provisionProject(context.projectKey, context.projectKey);
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(context.projectKey, "cpp", QualityProfiles.CPP_S106);

    File wrapperOutDir = new File(context.projectDir.toFile(), "out");

    var beginResult =context.begin
      .setProperty("sonar.cfamily.build-wrapper-output", wrapperOutDir.toString())
      .execute(ORCHESTRATOR);
    assertThat(beginResult.isSuccess()).describedAs("C++ begin step failed with logs %n%s", beginResult.getLogs()). isTrue();

    String platformToolset = System.getProperty("msbuild.platformtoolset", "v140");
    String windowsSdk = System.getProperty("msbuild.windowssdk", "10.0.18362.0");
    try(var buildWrapperDir = getBuildWrapperDir(context);) {
      TestUtils.runMSBuildWithBuildWrapper(ORCHESTRATOR, context.projectDir, buildWrapperDir.path.resolve("build-wrapper-win-x86/build-wrapper-win-x86-64.exe").toFile(),
        wrapperOutDir, "/t:Rebuild",
        String.format("/p:WindowsTargetPlatformVersion=%s", windowsSdk),
        String.format("/p:PlatformToolset=%s", platformToolset));

      BuildResult result = context.end.execute(ORCHESTRATOR);
      assertThat(result.isSuccess()).as(result.getLogs()).isTrue();
      assertThat(result.getLogs()).doesNotContain("Invalid character encountered in file");

      List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
      assertThat(issues).extracting(Issue::getRule).containsAll(List.of("cpp:S106"));
      assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "ncloc", ORCHESTRATOR)).isEqualTo(22);
      assertThat(TestUtils.getMeasureAsInteger(context.projectKey + ":Project1/Project1.cpp", "ncloc", ORCHESTRATOR)).isEqualTo(8);
    }
  }

  private static TempDirectory getBuildWrapperDir(AnalysisContext context) throws IOException {
    File buildWrapperZip = new File(context.projectDir.toString(), "build-wrapper-win-x86.zip");
    var buildWrapperDir = new TempDirectory("cpp-build-wrapper");
    FileUtils.copyURLToFile(new URL(ORCHESTRATOR.getServer().getUrl() + "/static/cpp/build-wrapper-win-x86.zip"), buildWrapperZip);
    ZipUtils.unzip(buildWrapperZip, buildWrapperDir.path.toFile());
    return buildWrapperDir;
  }
}
