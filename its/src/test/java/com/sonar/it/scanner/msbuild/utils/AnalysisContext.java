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
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static org.junit.jupiter.api.Assertions.assertTrue;

public class AnalysisContext {
  final static Logger LOG = LoggerFactory.getLogger(AnalysisContext.class);

  public final Orchestrator orchestrator; // Can be null for Cloud
  public final String projectKey;
  public final Path projectDir;
  public final String token;
  public final ScannerCommand begin;
  public final BuildCommand build;
  public final ScannerCommand end;

  public AnalysisContext(Orchestrator orchestrator, ScannerClassifier classifier, String projectKey, Path projectDir, String token) {
    this.orchestrator = orchestrator;
    this.projectKey = projectKey;
    this.projectDir = projectDir;
    this.token = token;
    begin = ScannerCommand.createBeginStep(classifier, token, projectDir, projectKey);
    build = new BuildCommand(projectDir);
    end = ScannerCommand.createEndStep(classifier, token, projectDir);
  }

  public static AnalysisContext forServer(String projectKey, Path temp, String directoryName) {
    return forServer(projectKey, temp, directoryName, ScannerClassifier.NET_FRAMEWORK);
  }

  public static AnalysisContext forServer(String projectKey, Path temp, String directoryName, ScannerClassifier classifier) {
    return new AnalysisContext(ServerTests.ORCHESTRATOR, classifier, projectKey, TestUtils.projectDir(temp, directoryName), ServerTests.token());
  }

  public static AnalysisContext forCloud(String projectKey, Path temp, String directoryName) {
    var context = new AnalysisContext(null, ScannerClassifier.NET_FRAMEWORK, projectKey, TestUtils.projectDir(temp, directoryName), CloudConstants.SONARCLOUD_TOKEN);
    context.begin
      .setOrganization(CloudConstants.SONARCLOUD_ORGANIZATION)
      .setProperty("sonar.scanner.sonarcloudUrl", CloudConstants.SONARCLOUD_URL)
      .setProperty("sonar.scanner.apiBaseUrl", CloudConstants.SONARCLOUD_API_URL)
      .setDebugLogs(true);
    context.build.useDotNet();  // We don't have Orchestrator to locate MsBuild.exe
    return context;
  }

  public AnalysisResult runAnalysis() {
    var beginResult = begin.execute(orchestrator);
    assertTrue(beginResult.isSuccess());
    var buildResult = build.execute(orchestrator);
    var endResult = end.execute(orchestrator);
    if (endResult.isSuccess()) {
      if (orchestrator != null) {
        TestUtils.dumpComponentList(orchestrator, projectKey);
        TestUtils.dumpProjectIssues(orchestrator, projectKey);
      }
    } else {
      LOG.warn("End step was not successful - skipping dumping issues data");
    }
    return new AnalysisResult(beginResult, buildResult, endResult);
  }
}
