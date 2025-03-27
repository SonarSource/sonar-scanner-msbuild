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
import com.sonar.it.scanner.msbuild.utils.QualityProfiles;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.locator.FileLocation;
import java.nio.file.Files;
import java.nio.file.Path;
import org.sonarqube.ws.client.HttpConnector;
import org.sonarqube.ws.client.WsClientFactories;
import org.sonarqube.ws.client.usertokens.GenerateRequest;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.junit.jupiter.api.Assertions.assertTrue;

public class OrchestratorState {

  private final Orchestrator orchestrator;
  private volatile int usageCount;
  private volatile boolean isStarted;
  private String token;

  public OrchestratorState(Orchestrator orchestrator) {
    this.orchestrator = orchestrator;
  }

  public void startOnce() throws Exception {
    synchronized (OrchestratorState.class) {
      usageCount += 1;
      if (usageCount == 1) {
        orchestrator.start();
        for (var profile: QualityProfiles.allProfiles())
        {
          orchestrator.getServer().restoreProfile(FileLocation.of(String.format("qualityProfiles/%s.xml", profile)));
        }

        token = WsClientFactories.getDefault().newClient(HttpConnector.newBuilder().url(orchestrator.getServer().getUrl()).credentials("admin", "admin").build())
          .userTokens()
          .generate(new GenerateRequest().setName("ITs"))
          .getToken();
        // To avoid a race condition in scanner file cache mechanism we analyze single project before any test to populate the cache
        analyzeEmptyProject();
        isStarted = true;
      } else if (!isStarted) {  // The second, third and any other caller should fail fast if something went wrong for the first one
        throw new IllegalStateException("Previous OrchestratorState startup failed");
      }
    }
  }

  public void stopOnce() {
    synchronized (OrchestratorState.class) {
      usageCount -= 1;
      if (usageCount == 0) {
        orchestrator.stop();
        isStarted = false;
      }
    }
  }

  public String token() {
    if (token == null) {
      throw new RuntimeException("OrchestratorState was not started and token is not available yet.");
    }
    return token;
  }

  private void analyzeEmptyProject() throws Exception {
    ContextExtension.init("OrchestratorState.Startup." + Thread.currentThread().getName());
    var result = AnalysisContext.forServer("Empty", ScannerClassifier.NET).runAnalysis();
    assertTrue(result.begin().isSuccess(), "Orchestrator warmup failed - begin step");
    assertTrue(result.build().isSuccess(), "Orchestrator warmup failed - build");
    assertTrue(result.end().isSuccess(), "Orchestrator warmup failed - end step");
    ContextExtension.cleanup();
  }
}
