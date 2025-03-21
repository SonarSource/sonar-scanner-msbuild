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

import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.Orchestrator;
import java.nio.file.Files;
import java.nio.file.Path;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.junit.jupiter.api.Assertions.assertTrue;

public class OrchestratorState {

  private final Orchestrator orchestrator;
  private volatile int usageCount;

  public OrchestratorState(Orchestrator orchestrator) {
    this.orchestrator = orchestrator;
  }

  public void startOnce() throws Exception {
    synchronized (OrchestratorState.class) {
      usageCount += 1;
      if (usageCount == 1) {
        orchestrator.start();
        TestUtils.getNewToken(orchestrator);  // ToDo: SCAN4NET-293 This is an ugly tangle that should be fixed later
        // To avoid a race condition in scanner file cache mechanism we analyze single project before any test to populate the cache
        analyzeEmptyProject();
      }
    }
  }

  public void stopOnce() {
    synchronized (OrchestratorState.class) {
      usageCount -= 1;
      if (usageCount == 0) {
        orchestrator.stop();
      }
    }
  }

  private void analyzeEmptyProject() throws Exception {
    Path temp = Files.createTempDirectory("OrchestratorState.Startup." + Thread.currentThread().getName());
    Path projectDir = TestUtils.projectDir(temp, "Empty");
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    assertTrue(TestUtils.newScannerBegin(ORCHESTRATOR, "Empty", projectDir, token, ScannerClassifier.NET_FRAMEWORK).execute(ORCHESTRATOR).isSuccess(),
      "Orchestrator warmup failed - begin step");
    TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);
    assertTrue(TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, "Empty", token).isSuccess(),
      "Orchestrator warmup failed - end step");
    TestUtils.deleteDirectory(temp);
  }
}
