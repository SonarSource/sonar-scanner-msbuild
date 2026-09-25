/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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
package com.sonar.it.scanner.msbuild.server;

import com.sonar.it.scanner.msbuild.utils.AnalysisContext;
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.QualityProfile;
import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.locator.FileLocation;
import java.util.stream.Collectors;
import org.sonarqube.ws.Qualityprofiles;
import org.sonarqube.ws.client.HttpConnector;
import org.sonarqube.ws.client.WsClient;
import org.sonarqube.ws.client.WsClientFactories;
import org.sonarqube.ws.client.qualityprofiles.SearchRequest;
import org.sonarqube.ws.client.qualityprofiles.SetDefaultRequest;
import org.sonarqube.ws.client.usertokens.GenerateRequest;

import static org.junit.jupiter.api.Assertions.assertTrue;

public class OrchestratorState {

  private static final String CORE_PROFILE = "Sonar way core";
  private static final String COMPREHENSIVE_PROFILE = "Sonar way comprehensive";

  private final Orchestrator orchestrator;
  private volatile int usageCount;
  private volatile boolean isStarted;
  private String token;

  public OrchestratorState(Orchestrator orchestrator) {
    this.orchestrator = orchestrator;
  }

  public void startOnce() {
    synchronized (OrchestratorState.class) {
      usageCount += 1;
      if (usageCount == 1) {
        orchestrator.start();
        var adminClient = WsClientFactories.getDefault().newClient(HttpConnector.newBuilder().url(orchestrator.getServer().getUrl()).credentials("admin", "admin").build());
        restoreComprehensiveDefaultProfiles(adminClient);
        for (var profile : QualityProfile.allProfiles()) {
          orchestrator.getServer().restoreProfile(FileLocation.of(String.format("qualityProfiles/%s.xml", profile)));
        }

        token = adminClient
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

  // Since SonarQube 2026.6 (SONAR-32511), every language ships "Sonar way core", "Sonar way extended" and "Sonar way comprehensive",
  // and "Sonar way core" is the default. Tests were written against the full rule set, so we restore "Sonar way comprehensive" as default.
  // Older versions ship only "Sonar way" and are left untouched.
  private static void restoreComprehensiveDefaultProfiles(WsClient client) {
    var builtInProfiles = client.qualityprofiles().search(new SearchRequest()).getProfilesList().stream()
      .filter(Qualityprofiles.SearchWsResponse.QualityProfile::getIsBuiltIn)
      .toList();
    var tieredLanguages = builtInProfiles.stream()
      .filter(x -> x.getName().equals(CORE_PROFILE))
      .map(Qualityprofiles.SearchWsResponse.QualityProfile::getLanguage)
      .collect(Collectors.toSet());
    for (var language : tieredLanguages) {
      if (builtInProfiles.stream().noneMatch(x -> x.getLanguage().equals(language) && x.getName().equals(COMPREHENSIVE_PROFILE))) {
        throw new IllegalStateException(String.format("Language '%s' has built-in profile '%s' but no '%s'", language, CORE_PROFILE, COMPREHENSIVE_PROFILE));
      }
      client.qualityprofiles().setDefault(new SetDefaultRequest().setLanguage(language).setQualityProfile(COMPREHENSIVE_PROFILE));
    }
  }

  private void analyzeEmptyProject() {
    ContextExtension.init("OrchestratorState.Startup." + Thread.currentThread().getName());
    var result = AnalysisContext.forServer("Empty").runAnalysis();
    assertTrue(result.begin().isSuccess(), "Orchestrator warmup failed - begin step");
    assertTrue(result.build().isSuccess(), "Orchestrator warmup failed - build");
    assertTrue(result.end().isSuccess(), "Orchestrator warmup failed - end step");
    ContextExtension.cleanup();
  }
}
