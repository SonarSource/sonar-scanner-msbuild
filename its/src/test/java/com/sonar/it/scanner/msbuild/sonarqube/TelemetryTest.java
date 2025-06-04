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

import com.sonar.it.scanner.msbuild.utils.*;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;

@ExtendWith({ServerTests.class, ContextExtension.class})
class TelemetryTest {

  @Test
  void telemetry_telemetryFiles_areCorrect_CS() throws IOException {
    AssertTelemetry("Telemetry");
  }

  @Test
  void telemetry_telemetryFiles_areCorrect_VB() throws IOException {
    AssertTelemetry("TelemetryVB");
  }

  @Test
  void telemetry_telemetryFiles_areCorrect_CSVB_Mixed() throws IOException {
    AssertTelemetry("TelemetryCSVBMixed");
  }

  @Test
  @MSBuildMinVersion(17)
  void telemetry_telemetryFiles_areCorrect_multiTargetFramework() throws IOException {

    var context = AnalysisContext.forServer(Paths.get("Telemetry", "TelemetryMultiTarget").toString());

    context.begin.setProperty(
      new Property("sonar.verbose", "true"));
    context.runAnalysis();

    var sonarQubeOutDirectory = context.projectDir.resolve(".sonarqube").resolve("out");

    ArrayList<String> projectMonikers = new ArrayList<>();
    for (int i=0; i<4; i++) {
      projectMonikers.addAll(readContents(sonarQubeOutDirectory.resolve(String.valueOf(i)).resolve("Telemetry.json")));
    }

    assertThat(projectMonikers).containsExactlyInAnyOrder(
      "{\"dotnetenterprise.s4net.build.target_framework_moniker\":\".NETCoreApp,Version=v8.0\"}",
      "{\"dotnetenterprise.s4net.build.target_framework_moniker\":\".NETCoreApp,Version=v8.0\"}",
      "{\"dotnetenterprise.s4net.build.target_framework_moniker\":\".NETCoreApp,Version=v9.0\"}",
      "{\"dotnetenterprise.s4net.build.target_framework_moniker\":\".NETCoreApp,Version=v9.0\"}");

    var result = context.end.execute(ORCHESTRATOR);
    var logLines = Arrays.asList(result.getLogs().split("\n"));
    // guid.sonar.cs.scanner.telemetry should exist once per project
    assertThat(logLines.stream().filter(x -> x.matches(".*\\.sonar\\.cs\\.scanner\\.telemetry=\\\\"))).hasSize(2);
    // "TelemetryMultiTarget\\.sonarqube\\out\\[uniqueNumber]\\Telemetry.json" should exist once per project and per target framework
    assertThat(logLines.stream().filter(x -> x.matches(".*\\\\\\\\[0-9]\\\\\\\\Telemetry\\.json\".?\\\\?"))).hasSize(4);
  }

  private void AssertTelemetry(String projectName) throws IOException {
    var context = AnalysisContext.forServer(Paths.get("Telemetry", projectName).toString());
    context.begin.setProperty(
      new Property("sonar.scanner.scanAll", "false"),
      new Property("s", context.projectDir.resolve(Paths.get("SonarQube.Analysis.xml")).toAbsolutePath().toString()));
    context.runAnalysis();

    var sonarQubeOutDirectory = context.projectDir.resolve(".sonarqube").resolve("out");

    assertThat(readContents(sonarQubeOutDirectory.resolve(("Telemetry.S4NET.json"))))
      .satisfiesExactly(
        x -> assertThat(x).isEqualTo("{\"dotnetenterprise.s4net.params.sonar_scanner_skipjreprovisioning.source\":\"CLI\"}"),
        x -> assertThat(x).isEqualTo("{\"dotnetenterprise.s4net.params.sonar_branch_autoconfig_disabled.source\":\"CLI\"}"),
        x -> assertThat(x).isEqualTo("{\"dotnetenterprise.s4net.params.sonar_projectbasedir.source\":\"CLI\"}"),
        x -> assertThat(x).isEqualTo("{\"dotnetenterprise.s4net.params.sonar_projectbasedir.value\":\"rooted\"}"),
        x -> assertThat(x).isEqualTo("{\"dotnetenterprise.s4net.params.sonar_scanner_scanall.source\":\"CLI\"}"),
        x -> assertThat(x).isEqualTo("{\"dotnetenterprise.s4net.params.sonar_scm_disabled.source\":\"CLI\"}"),
        x -> assertThat(x).isEqualTo("{\"dotnetenterprise.s4net.serverInfo.product\":\"SQ_Server\"}"),
        x -> assertThat(x).isEqualTo("{\"dotnetenterprise.s4net.serverInfo.serverUrl\":\"custom_url\"}")
      );

    assertThat(readContents(sonarQubeOutDirectory.resolve("Telemetry.Targets.S4NET.json")))
      .satisfiesExactly(
        x -> assertThat(x).startsWith("{\"dotnetenterprise.s4net.build.visual_studio_version\":"),
        x -> assertThat(x).startsWith("{\"dotnetenterprise.s4net.build.msbuild_version\":")
      );

    for (int i = 0; i < 3; i++) {
      assertThat(readContents(sonarQubeOutDirectory.resolve(String.valueOf(i)).resolve("Telemetry.json"))).satisfiesExactly(
        x -> assertThat(x).isEqualTo("{\"dotnetenterprise.s4net.build.target_framework_moniker\":\".NETStandard,Version=v1.6\"}"));
    }
  }

  private List<String> readContents(Path path) throws IOException {
    List<String> content = Files.readAllLines(path, StandardCharsets.UTF_8);
    // see: https://stackoverflow.com/a/73590918
    content.replaceAll(x -> x.replace("\uFEFF", ""));
    return content;
  }
}

