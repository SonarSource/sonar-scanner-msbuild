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
import java.util.Objects;
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
      .addPlugin(TestUtils.getMavenLocation("com.sonarsource.cpp", "sonar-cfamily-plugin", getPluginVersion(version, "sonar.cfamilyplugin.version")))
      .addPlugin(TestUtils.getMavenLocation("org.sonarsource.css", "sonar-css-plugin", getPluginVersion(version, "sonar.css.version")))
      .addPlugin(TestUtils.getMavenLocation("org.sonarsource.dotnet", "sonar-csharp-plugin", getPluginVersion(version, "sonar.csharpplugin.version")))
      .addPlugin(TestUtils.getMavenLocation("org.sonarsource.dotnet", "sonar-vbnet-plugin", getPluginVersion(version, "sonar.vbnetplugin.version")))
      .addPlugin(TestUtils.getMavenLocation("org.sonarsource.xml", "sonar-xml-plugin", getPluginVersion(version, "sonar.xmlplugin.version")))
      // The following plugin versions are hardcoded because `DEV` is not compatible with SQ < 8.9, to be fixed with this issue: https://github.com/SonarSource/sonar-scanner-msbuild/issues/1486
      .addPlugin(TestUtils.getMavenLocation("org.sonarsource.javascript", "sonar-javascript-plugin", getPluginVersion(version, "sonar.javascriptplugin.version")))
      .addPlugin(TestUtils.getMavenLocation("com.sonarsource.plsql", "sonar-plsql-plugin", getPluginVersion(version, "sonar.plsqlplugin.version")))
      .activateLicense();

    if (!version.contains("8.9")) {
      // The latest version of the sonarqube-roslyn-sdk generates packages that are compatible only with SQ 9.9 and above.
      orchestrator.addPlugin(FileLocation.of(TestUtils.getCustomRoslynPlugin().toFile()));
    }
    return orchestrator.build();
  }

  private static String getPluginVersion(String runtimeVersion, String pluginKey) {
    return System.getProperty(pluginKey, getDefaultPluginVersion(runtimeVersion, pluginKey));
  }

  private static String getDefaultPluginVersion(String runtimeVersion, String pluginKey) {
    return switch (pluginKey) {
      case "sonar.cfamilyplugin.version" -> switch (runtimeVersion) {
        case "LATEST_RELEASE[8.9]" -> "6.20.0.31240";
        case "LATEST_RELEASE[9.9]" -> "6.41.0.60884";
        case "LATEST_RELEASE[2025.1]" -> "6.62.0.78645";
        default -> defaultFromRuntimeVersion(runtimeVersion);
      };
      case "sonar.css.version" -> switch (runtimeVersion) {
        case "LATEST_RELEASE[8.9]" -> "1.4.2.2002";
        case "LATEST_RELEASE[9.9]" -> "9.13.0.20537";
        case "LATEST_RELEASE[2025.1]" -> "10.20.0.29356";
        default -> defaultFromRuntimeVersion(runtimeVersion);
      };
      case "sonar.csharpplugin.version", "sonar-vbnet-plugin" -> switch (runtimeVersion) {
        case "LATEST_RELEASE[8.9]" -> "8.22.0.31243";
        case "LATEST_RELEASE[9.9]" -> "8.51.0.59060";
        case "LATEST_RELEASE[2025.1]" -> "10.4.0.108396";
        default -> defaultFromRuntimeVersion(runtimeVersion);
      };
      case "sonar.javascriptplugin.version" -> switch (runtimeVersion) {
        case "LATEST_RELEASE[8.9]" -> "7.4.4.15624";
        case "LATEST_RELEASE[9.9]" -> "9.13.0.20537";
        case "LATEST_RELEASE[2025.1]" -> "10.20.0.29356";
        default -> defaultFromRuntimeVersion(runtimeVersion);
      };
      case "sonar.plsqlplugin.version" -> switch (runtimeVersion) {
        case "LATEST_RELEASE[8.9]" -> "3.6.1.3873";
        case "LATEST_RELEASE[9.9]" -> "3.8.0.4948";
        case "LATEST_RELEASE[2025.1]" -> "3.15.0.7123";
        default -> defaultFromRuntimeVersion(runtimeVersion);
      };
      case "sonar.xmlplugin.version" -> switch (runtimeVersion) {
        case "LATEST_RELEASE[8.9]" -> "2.0.1.2020";
        case "LATEST_RELEASE[9.9]" -> "2.7.0.3820";
        case "LATEST_RELEASE[2025.1]" -> "2.12.0.5749";
        default -> defaultFromRuntimeVersion(runtimeVersion);
      };
      default -> defaultFromRuntimeVersion(runtimeVersion);
    };
  }

  private static String defaultFromRuntimeVersion(String runtimeVersion) {
    return Objects.equals(runtimeVersion, "DEV")
      ? "DEV"
      : "LATEST_RELEASE";
  }
}
