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

import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import java.io.IOException;
import java.nio.file.LinkOption;
import java.nio.file.Path;
import java.util.function.Function;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.sonarqube.ws.Components;
import org.sonarqube.ws.Issues;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;
import static org.junit.jupiter.api.Assertions.assertTrue;

@ExtendWith({ServerTests.class, ContextExtension.class})
class BaseDirTest {
  private static final String SONAR_RULES_PREFIX = "csharpsquid:";

  @TempDir
  public Path basePath;

  @Test
  void testCSharpSharedFileWithOneProjectWithoutProjectBaseDir() throws IOException {
    var projectKey = "CSharpSharedFileWithOneProject";
    var projectDir = TestUtils.projectDir(basePath, projectKey);
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK)
      .setProperty("sonar.sourceEncoding", "UTF-8")
      .setProperty("sonar.projectBaseDir", projectDir.resolve("ClassLib1").toAbsolutePath().toString()) // Common.cs file is outside of this base path and will not be uploaded to SQ
      .execute(ORCHESTRATOR);
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Restore,Rebuild");
    TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);

    assertThat(TestUtils.listComponents(ORCHESTRATOR, projectKey))
      .extracting(Components.Component::getKey)
      .containsExactlyInAnyOrder("CSharpSharedFileWithOneProject:Class1.cs"); // Common.cs is not present
  }

  @Test
  void whenEachProjectIsOnDifferentDrives_AnalysisFails() throws IOException {
    try {
      Path projectDir = TestUtils.projectDir(basePath, "TwoDrivesTwoProjects");
      TestUtils.createVirtualDrive("Z:", projectDir, "DriveZ");

      BuildResult buildResult = runAnalysisWithoutProjectBasedDir(projectDir);

      assertThat(buildResult.isSuccess()).isFalse();
      assertThat(buildResult.getLogs()).contains("Generation of the sonar-properties file failed. Unable to complete the analysis.");
    } finally {
      TestUtils.deleteVirtualDrive("Z:");
    }
  }

  @Test
  void whenMajorityOfProjectsIsOnSameDrive_AnalysisSucceeds() throws IOException {
    try {
      var projectKey = "TwoDrivesThreeProjects";
      Path projectDir = TestUtils.projectDir(basePath, projectKey);
      TestUtils.createVirtualDrive("Y:", projectDir, "DriveY");

      BuildResult buildResult = runAnalysisWithoutProjectBasedDir(projectDir);
      assertThat(buildResult.isSuccess()).isTrue();
      assertThat(buildResult.getLogs()).contains("Using longest common projects path as a base directory: '" + projectDir);
      assertThat(buildResult.getLogs()).contains("WARNING: Directory 'Y:\\Subfolder' is not located under the base directory '" + projectDir + "' and will not be analyzed.");
      assertThat(buildResult.getLogs()).contains("WARNING: File 'Y:\\Subfolder\\Program.cs' is not located under the base directory '" + projectDir +
        "' and will not be analyzed.");
      assertThat(buildResult.getLogs()).contains("File was referenced by the following projects: 'Y:\\Subfolder\\DriveY.csproj'.");
      assertThat(TestUtils.projectIssues(ORCHESTRATOR, projectKey)).hasSize(2)
        .extracting(Issues.Issue::getRule, Issues.Issue::getComponent)
        .containsExactlyInAnyOrder(
          tuple("vbnet:S6145", projectKey),
          tuple(SONAR_RULES_PREFIX + "S1134", projectKey + ":DefaultDrive/Program.cs")
        );
    } finally {
      TestUtils.deleteVirtualDrive("Y:");
    }
  }

  @Test
  void testAzureFunctions_WithWrongBaseDirectory_AnalysisSucceeds() throws IOException {
    Path projectDir = TestUtils.projectDir(basePath, "ReproAzureFunctions");
    BuildResult buildResult = runAnalysisWithoutProjectBasedDir(projectDir);

    assertThat(buildResult.isSuccess()).isTrue();
    var temporaryFolderRoot = basePath.getParent().toFile().getCanonicalFile().toString();
    assertThat(buildResult.getLogs()).contains(" '" + temporaryFolderRoot);
  }

  @Test
  void testCSharpSharedFileWithOneProjectUsingProjectBaseDirAbsolute() throws IOException {
    runCSharpSharedFileWithOneProjectUsingProjectBaseDir(
      projectDir -> {
        try {
          return projectDir.toRealPath(LinkOption.NOFOLLOW_LINKS).toString();
        } catch (IOException e) {
          e.printStackTrace();
        }
        return null;
      });
  }

  /* TODO: This test doesn't work as expected. Relative path will create sub-folders on SonarQube and so files are not
           located where you expect them.
  @Test
  public void testCSharpSharedFileWithOneProjectUsingProjectBaseDirRelative() throws IOException {
    runCSharpSharedFileWithOneProjectUsingProjectBaseDir(projectDir -> "..\\..");
  } */

  @Test
  void testCSharpSharedFileWithOneProjectUsingProjectBaseDirAbsoluteShort() throws IOException {
    runCSharpSharedFileWithOneProjectUsingProjectBaseDir(Path::toString);
  }

  private void runCSharpSharedFileWithOneProjectUsingProjectBaseDir(Function<Path, String> getProjectBaseDir)
    throws IOException {
    String folderName = "CSharpSharedFileWithOneProject";
    Path projectDir = TestUtils.projectDir(basePath, folderName);

    String token = TestUtils.getNewToken(ORCHESTRATOR);
    TestUtils.newScannerBegin(ORCHESTRATOR, folderName, projectDir, token)
      .setProperty("sonar.projectBaseDir", getProjectBaseDir.apply(projectDir))
      .setDebugLogs()
      .execute(ORCHESTRATOR);

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Restore,Rebuild");

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, folderName, token);
    assertTrue(result.isSuccess());
    assertThat(TestUtils.getComponent(folderName + ":Common.cs")).isNotNull();
    assertThat(TestUtils.getComponent(folderName + ":ClassLib1/Class1.cs")).isNotNull();
  }

  private BuildResult runAnalysisWithoutProjectBasedDir(Path projectDir) {
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    String folderName = projectDir.getFileName().toString();
    TestUtils.newScannerBegin(ORCHESTRATOR, folderName, projectDir, token, ScannerClassifier.NET)
      .setProperty("sonar.projectBaseDir", null)  // Do NOT set "sonar.projectBaseDir" for this test. We need to remove the default value
      .setDebugLogs()
      .setProperty("sonar.sourceEncoding", "UTF-8")
      .execute(ORCHESTRATOR);

    BuildResult buildResult = TestUtils.runDotnetCommand(projectDir, "build", folderName + ".sln", "--no-incremental");
    assertThat(buildResult.getLastStatus()).isZero();

    return TestUtils.newScannerEnd(ORCHESTRATOR, projectDir, ScannerClassifier.NET, token).execute(ORCHESTRATOR);
  }
}
