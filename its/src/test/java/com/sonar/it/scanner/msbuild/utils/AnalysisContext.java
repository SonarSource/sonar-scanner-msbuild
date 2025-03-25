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

public class AnalysisContext {
  final static Logger LOG = LoggerFactory.getLogger(AnalysisContext.class);

  public final Orchestrator orchestrator;
  public final String projectKey;
  public final Path projectDir;
  public final String token;
  public final ScannerCommand begin;
  public final ScannerCommand end;

  public AnalysisContext(Orchestrator orchestrator, ScannerClassifier classifier, String projectKey, Path projectDir, String token) {
    this.orchestrator = orchestrator;
    this.projectKey = projectKey;
    this.projectDir = projectDir;
    this.token = token;
    begin = ScannerCommand.createBeginStep(classifier, token, projectDir, projectKey);
    end = ScannerCommand.createEndStep(classifier, token, projectDir);
  }

  public static AnalysisContext forServer(String projectKey, Path temp, String directoryName) {
    return forServer(projectKey, temp, directoryName, ScannerClassifier.NET_FRAMEWORK);
  }

  public static AnalysisContext forServer(String projectKey, Path temp, String directoryName, ScannerClassifier classifier) {
    return new AnalysisContext(ServerTests.ORCHESTRATOR, classifier, projectKey, TestUtils.projectDir(temp, directoryName), ServerTests.token());
  }

  public static AnalysisContext forCloud(String projectKey, Path temp, String directoryName) {
    return new AnalysisContext(null, ScannerClassifier.NET_FRAMEWORK, projectKey, TestUtils.projectDir(temp, directoryName), CloudConstants.SONARCLOUD_TOKEN);
  }

  public AnalysisResult runAnalysis() {
    var beginResult = begin.execute(orchestrator);
    // ToDo: NuGet vs Restore
    // ToDo: SCAN4NET-10 Use BuildCommand
    var buildResult = TestUtils.buildMSBuild(orchestrator, this.projectDir);
    var endResult = end.execute(orchestrator);
    if (endResult.isSuccess()) {
      TestUtils.dumpComponentList(orchestrator, projectKey);
      TestUtils.dumpProjectIssues(orchestrator, projectKey);
    } else {
      LOG.warn("End step was not successful - skipping dumping issues data");
    }
    return new AnalysisResult(beginResult, buildResult, endResult);
  }
}
