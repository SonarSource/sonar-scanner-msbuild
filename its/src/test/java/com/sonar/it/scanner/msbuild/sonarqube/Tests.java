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
import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.container.Edition;
import com.sonar.orchestrator.junit5.OrchestratorExtension;
import com.sonar.orchestrator.locator.FileLocation;
import org.junit.jupiter.api.extension.AfterAllCallback;
import org.junit.jupiter.api.extension.BeforeAllCallback;
import org.junit.jupiter.api.extension.ExtensionContext;

public class Tests implements BeforeAllCallback, AfterAllCallback {

  public static final Orchestrator ORCHESTRATOR = createOrchestrator();

  private volatile int usageCount;

  @Override
  public void beforeAll(ExtensionContext extensionContext) {
    usageCount += 1;
    if (usageCount == 1) {
      ORCHESTRATOR.start();
    }
  }

  @Override
  public void afterAll(ExtensionContext extensionContext) throws Exception {
    usageCount -= 1;
    if (usageCount == 0) {
      ORCHESTRATOR.stop();
    }
  }

  private static Orchestrator createOrchestrator() {
    var version = System.getProperty("sonar.runtimeVersion", "DEV");
    var orchestrator = OrchestratorExtension.builderEnv()
      .useDefaultAdminCredentialsForBuilds(true)
      .setSonarVersion(version)
      .setEdition(Edition.DEVELOPER)
      .setServerProperty("sonar.telemetry.enable", "false") // Disabling telemetry to avoid polluting our own data.
      // Plugin versions are defined in https://github.com/SonarSource/sonar-scanner-msbuild/blob/master/azure-pipelines.yml
      // The versions specified here are used when testing locally.
      .addPlugin(TestUtils.getMavenLocation("com.sonarsource.cpp", "sonar-cfamily-plugin", System.getProperty("sonar.cfamilyplugin.version", "LATEST_RELEASE")))
      .addPlugin(TestUtils.getMavenLocation("org.sonarsource.dotnet", "sonar-csharp-plugin", System.getProperty("sonar.csharpplugin.version", "DEV")))
      .addPlugin(TestUtils.getMavenLocation("org.sonarsource.dotnet", "sonar-vbnet-plugin", System.getProperty("sonar.vbnetplugin.version", "DEV")))
      .addPlugin(TestUtils.getMavenLocation("org.sonarsource.xml", "sonar-xml-plugin", System.getProperty("sonar.xmlplugin.version", "LATEST_RELEASE")))
      .addPlugin(TestUtils.getMavenLocation("com.sonarsource.plsql", "sonar-plsql-plugin", System.getProperty("sonar.plsqlplugin.version", "LATEST_RELEASE")))
      // The following plugin versions are hardcoded because `DEV` is not compatible with SQ < 8.9, to be fixed with this issue: https://github.com/SonarSource/sonar-scanner-msbuild/issues/1486
      .addPlugin(TestUtils.getMavenLocation("org.sonarsource.javascript", "sonar-javascript-plugin", System.getProperty("sonar.javascriptplugin.version", "LATEST_RELEASE")))
      .activateLicense();

    if (version.contains("8.9")) {
      // org.sonarsource.css was discontinued after 8.9 and merged into javascript
      orchestrator.addPlugin(TestUtils.getMavenLocation("org.sonarsource.css", "sonar-css-plugin", System.getProperty("sonar.css.version", "LATEST_RELEASE")));
    } else {
      // The latest version of the sonarqube-roslyn-sdk generates packages that are compatible only with SQ 9.9 and above.
      orchestrator.addPlugin(FileLocation.of(TestUtils.getCustomRoslynPlugin().toFile()));
    }
    return orchestrator.build();
  }
}
