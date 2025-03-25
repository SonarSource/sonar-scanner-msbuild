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

import com.sonar.it.scanner.msbuild.sonarcloud.CloudConstants;
import com.sonar.it.scanner.msbuild.sonarqube.ServerTests;
import com.sonar.orchestrator.Orchestrator;
import java.nio.file.Path;

public class AnalysisContext {

  public final Orchestrator orchestrator;
  public final String projectKey;
  public final Path projectDir;
  public final String token;

  public AnalysisContext(Orchestrator orchestrator, String projectKey, Path projectDir, String token) {
    this.orchestrator = orchestrator;
    this.projectKey = projectKey;
    this.projectDir = projectDir;
    this.token = token;
  }

  public static AnalysisContext forServer(String projectKey, Path temp, String directoryName) {
    return new AnalysisContext(ServerTests.ORCHESTRATOR, projectKey, TestUtils.projectDir(temp, directoryName), ServerTests.token());
  }

  public static AnalysisContext forCloud(String projectKey, Path temp, String directoryName) {
    return new AnalysisContext(null, projectKey, TestUtils.projectDir(temp, directoryName), CloudConstants.SONARCLOUD_TOKEN);
  }

  public AnalysisResult runAnalysis() {
    // FIXME: Make this SC compatible
    var begin = ScannerCommand.createBeginStep(this).execute(orchestrator);
    var build = TestUtils.buildMSBuild(orchestrator, this.projectDir);
    var end = TestUtils.executeEndStepAndDumpResults(orchestrator, projectDir, projectKey, token);
    return new AnalysisResult(begin, build, end);
  }

}
