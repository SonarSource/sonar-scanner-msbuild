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

import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.container.Edition;
import com.sonar.orchestrator.junit5.OrchestratorExtension;
import com.sonar.orchestrator.junit5.OrchestratorExtensionBuilder;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.locator.MavenLocation;
import java.io.IOException;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;
import org.junit.jupiter.api.extension.AfterAllCallback;
import org.junit.jupiter.api.extension.BeforeAllCallback;
import org.junit.jupiter.api.extension.ExtensionContext;

public class ServerTests implements BeforeAllCallback, AfterAllCallback {

  public static final Orchestrator ORCHESTRATOR = createOrchestrator();
  private static final OrchestratorState ORCHESTRATOR_STATE = new OrchestratorState(ORCHESTRATOR);

  @Override
  public void beforeAll(ExtensionContext extensionContext) {
    ORCHESTRATOR_STATE.startOnce();
  }

  @Override
  public void afterAll(ExtensionContext extensionContext) {
    ORCHESTRATOR_STATE.stopOnce();
  }

  public static String token() {
    return ORCHESTRATOR_STATE.token();
  }

  private static Orchestrator createOrchestrator() {
    var version = System.getProperty("sonar.runtimeVersion", "LATEST_RELEASE");
    var orchestrator = OrchestratorExtension.builderEnv()
      .useDefaultAdminCredentialsForBuilds(true)
      .setSonarVersion(version)
      .setEdition(Edition.DEVELOPER)
      .setServerProperty("sonar.telemetry.enable", "false"); // Disabling telemetry to avoid polluting our own data.
    // Plugin versions are defined in https://github.com/SonarSource/sonar-scanner-msbuild/blob/master/azure-pipelines.yml
    // Set the version to NONE to disable the plugin.
    addPlugin(orchestrator, "com.sonarsource.cpp", "sonar-cfamily-plugin", "sonar.cfamilyplugin.version");
    addPlugin(orchestrator, "com.sonarsource.plsql", "sonar-plsql-plugin", "sonar.plsqlplugin.version");
    addPlugin(orchestrator, "org.sonarsource.css", "sonar-css-plugin", "sonar.css.version", "NONE");
    addPlugin(orchestrator, "org.sonarsource.dotnet", "sonar-csharp-plugin", "sonar.csharpplugin.version", "DEV");
    addPlugin(orchestrator, "org.sonarsource.dotnet", "sonar-vbnet-plugin", "sonar.vbnetplugin.version", "DEV");
    addPlugin(orchestrator, "org.sonarsource.iac", "sonar-iac-plugin", "sonar.iacplugin.version");
    addPlugin(orchestrator, "org.sonarsource.java", "sonar-java-plugin", "sonar.javaplugin.version");
    addPlugin(orchestrator, "org.sonarsource.javascript", "sonar-javascript-plugin", "sonar.javascriptplugin.version");
    addPlugin(orchestrator, "org.sonarsource.php", "sonar-php-plugin", "sonar.phpplugin.version");
    addPlugin(orchestrator, "org.sonarsource.python", "sonar-python-plugin", "sonar.pythonplugin.version");
    addPlugin(orchestrator, "org.sonarsource.text", "sonar-text-plugin", "sonar.textplugin.version");
    addPlugin(orchestrator, "org.sonarsource.xml", "sonar-xml-plugin", "sonar.xmlplugin.version");
    addPlugin(orchestrator, System.getProperty("go.groupid", "org.sonarsource.go"), "sonar-go-plugin", "sonar.goplugin.version");

    // DO NOT add any additional plugin loading logic here. Everything must be in the YML
    if (!version.contains("8.9")) {
      // The latest version of the sonarqube-roslyn-sdk generates packages that are compatible only with SQ 9.9 and above.
      orchestrator.addPlugin(FileLocation.of(customRoslynPlugin().toFile()));
    }

    return orchestrator.activateLicense().build();
  }

  private static void addPlugin(OrchestratorExtensionBuilder orchestrator, String groupId, String artifactId, String versionProperty) {
    addPlugin(orchestrator, groupId, artifactId, versionProperty, "LATEST_RELEASE");
  }

  private static void addPlugin(OrchestratorExtensionBuilder orchestrator, String groupId, String artifactId, String versionProperty, String defaultVersion) {
    var version = System.getProperty(versionProperty, defaultVersion);
    if (version == null || version.isEmpty() || version.equals("NONE")) {
      return;
    }
    if(artifactId.equals("sonar-javascript-plugin") && !version.contains("7.4") && !version.contains("9.13")) { // dont look for multi in SQ 8.9 & 9.9
      orchestrator.addPlugin(MavenLocation.create(groupId, artifactId, version, "multi"));
    }
    orchestrator.addPlugin(MavenLocation.of(groupId, artifactId, version));
  }

  private static Path customRoslynPlugin() {
    Path customPluginDir = Paths.get("").resolve("analyzers");
    DirectoryStream.Filter<Path> jarFilter = file -> Files.isRegularFile(file) && file.toString().endsWith(".jar");
    List<Path> jars = new ArrayList<>();
    try {
      Files.newDirectoryStream(customPluginDir, jarFilter).forEach(jars::add);
    } catch (IOException e) {
      throw new IllegalStateException(e);
    }
    if (jars.isEmpty()) {
      throw new IllegalStateException("No jars found in " + customPluginDir);
    } else if (jars.size() > 1) {
      throw new IllegalStateException("Several jars found in " + customPluginDir);
    }
    return jars.get(0);
  }
}
