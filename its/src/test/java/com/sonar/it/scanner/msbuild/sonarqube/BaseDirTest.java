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
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.it.scanner.msbuild.utils.Timeout;
import java.io.IOException;
import java.nio.file.Path;
import java.nio.file.Paths;
import org.assertj.core.util.Files;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledOnOs;
import org.junit.jupiter.api.condition.OS;
import org.junit.jupiter.api.extension.ExtendWith;
import org.sonarqube.ws.Components;
import org.sonarqube.ws.Issues;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;

@ExtendWith({ServerTests.class, ContextExtension.class})
class BaseDirTest {

  @Test
  void whenProjectIsOutsideExplicitBaseDir_ProjectNotPresent() {
    var context = AnalysisContext.forServer("CSharpSharedFileWithOneProject");
    context.begin
      // Common.cs file is outside of this base path and will not be uploaded to SQ
      .setProperty("sonar.projectBaseDir", context.projectDir.resolve("ClassLib1").toAbsolutePath().toString());
    context.runAnalysis();

    assertThat(TestUtils.listComponents(ORCHESTRATOR, context.projectKey))
      .extracting(Components.Component::getKey)
      .containsExactlyInAnyOrder(context.projectKey + ":Class1.cs"); // Common.cs is not present
  }

  @Test
  @EnabledOnOs(OS.WINDOWS)
  void whenEachProjectIsOnDifferentDrives_AnalysisFails() {
    var context = createContextWithoutProjectBasedDir("TwoDrivesTwoProjects");
    try {
      TestUtils.createVirtualDrive("Z:", context.projectDir, "DriveZ");
      var logs = context.runFailedAnalysis().end().getLogs();

      assertThat(logs).contains("Generation of the sonar-properties file failed. Unable to complete the analysis.");
    } finally {
      TestUtils.deleteVirtualDrive("Z:", context.projectDir);
    }
  }

  @Test
  @EnabledOnOs(OS.WINDOWS)
  void whenMajorityOfProjectsIsOnSameDrive_AnalysisSucceeds() {
    var context = createContextWithoutProjectBasedDir("TwoDrivesThreeProjects");
    try {
      TestUtils.createVirtualDrive("Y:", context.projectDir, "DriveY");
      var logs = context.runAnalysis().end().getLogs();

      assertThat(logs).contains("Using longest common projects path as a base directory: '" + context.projectDir);
      assertThat(logs).contains("WARNING: Directory 'Y:\\Subfolder' is not located under the base directory '" + context.projectDir + "' and will not be analyzed.");
      assertThat(logs).contains("WARNING: File 'Y:\\Subfolder\\Program.cs' is not located under the base directory '" + context.projectDir +
        "' and will not be analyzed.");
      assertThat(logs).contains("File was referenced by the following projects: 'Y:\\Subfolder\\DriveY.csproj'.");
      assertThat(TestUtils.projectIssues(ORCHESTRATOR, context.projectKey)).hasSize(2)
        .extracting(Issues.Issue::getRule, Issues.Issue::getComponent)
        .containsExactlyInAnyOrder(
          tuple("vbnet:S6145", context.projectKey),
          tuple("csharpsquid:S1134", context.projectKey + ":DefaultDrive/Program.cs")
        );
    } finally {
      TestUtils.deleteVirtualDrive("Y:", context.projectDir);
    }
  }

  @Test
  void azureFunctions_WithWrongBaseDirectory_AnalysisSucceeds() throws IOException {
    var context = createContextWithoutProjectBasedDir("ReproAzureFunctions"); // Azure Functions creates auto-generated project in temp as part of the compilation
    var temporaryFolderRoot = context.projectDir.getParent().toFile().getCanonicalFile().toString();
    context.build.useDotNet().setTimeout(Timeout.TWO_MINUTES);
    var logs = context.runAnalysis().end().getLogs();

    assertThat(logs).contains(" '" + temporaryFolderRoot);
  }

  @Test
  void projectBaseDir_Absolute() {
    var context = AnalysisContext.forServer("CSharpSharedFileWithOneProject");
    assertThat(context.projectDir.toString()).doesNotContain("~"); // Scaffolding does .toRealPath to convert it from DOS 8.3 format
    context.begin.setProperty("sonar.projectBaseDir", context.projectDir.toString());
    context.runAnalysis();

    assertThat(TestUtils.listComponents(ORCHESTRATOR, context.projectKey))
      .extracting(Components.Component::getKey)
      .containsExactlyInAnyOrder(
        context.projectKey + ":Common.cs",
        context.projectKey + ":ClassLib1/Class1.cs"
      );
  }

  @Test
  // We test this on Windows only because the 8.3 format is a Windows convention
  // https://en.wikipedia.org/wiki/8.3_filename
  @EnabledOnOs(OS.WINDOWS)
  void projectBaseDir_AbsoluteShort() {
    var directoryName = "CSharpSharedFileWithOneProject";
    var context = AnalysisContext.forServer(directoryName);
    var tempDirectoryName = context.projectDir.getParent().getFileName().toString(); // Something like "junit5-ContextExtension-projectBaseDir_AbsoluteShort-11477225628510485675"
    // Files.temporaryFolderPath returns the same root in 8.3, but only when the name is long (locally). AzureDev Ops CI has a short C:\Windows\TEMP\
    var projectDir83Format = Path.of(Files.temporaryFolderPath(), tempDirectoryName, directoryName);
    context.begin.setProperty("sonar.projectBaseDir", projectDir83Format.toString());
    context.runAnalysis();

    assertThat(TestUtils.listComponents(ORCHESTRATOR, context.projectKey))
      .extracting(Components.Component::getKey)
      .containsExactlyInAnyOrder(
        context.projectKey + ":Common.cs",
        context.projectKey + ":ClassLib1/Class1.cs"
      );
  }

  @Test
  void projectBaseDir_Relative() {
    var context = AnalysisContext.forServer("CSharpSharedFileWithOneProject");
    // projectDir = "C:\Windows\Temp\junit5-ContextExtension-projectBaseDir_Relative-11477225628510485675\CSharpSharedFileWithOneProject"
    // tempDirectoryName = "junit5-ContextExtension-projectBaseDir_Relative-11477225628510485675"
    // projectBaseDir = "..\.." is relative to the projectDir. That is "C:\Windows\Temp\", so component keys should start with tempDirectoryName.
    var tempDirectoryName = context.projectDir.getParent().getFileName().toString();
    context.begin.setProperty("sonar.projectBaseDir", Paths.get("..", "..").toString());  // Relative from scanner working directory
    context.runAnalysis();

    assertThat(TestUtils.listComponents(ORCHESTRATOR, context.projectKey))
      .extracting(Components.Component::getKey)
      .containsExactlyInAnyOrder(
        context.projectKey + ":" + tempDirectoryName + "/CSharpSharedFileWithOneProject/Common.cs",
        context.projectKey + ":" + tempDirectoryName + "/CSharpSharedFileWithOneProject/ClassLib1/Class1.cs"
      );
  }

  private AnalysisContext createContextWithoutProjectBasedDir(String directoryName) {
    var context = AnalysisContext.forServer(directoryName);
    context.begin
      .setProperty("sonar.projectBaseDir", null)  // Do NOT set "sonar.projectBaseDir" for this test. We need to remove the default value
      .setDebugLogs();
    return context;
  }
}
