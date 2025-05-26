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
import org.sonarqube.ws.Components;
import org.sonarqube.ws.Issues.Issue;

import java.io.FileInputStream;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.stream.Collectors;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;

@ExtendWith({ServerTests.class, ContextExtension.class})
class TelemetryTest {

  @Test
  void telemetry_telemetryFiles_areCorrect() throws IOException {
    var context = AnalysisContext.forServer("Telemetry");
    context.begin.setProperty("sonar.scanner.scanAll", "true");
    context.runAnalysis();

    var sonarQubeOutDirectory = context.projectDir.resolve(".sonarqube").resolve("out");

    assertThat(readContents(sonarQubeOutDirectory.resolve(("Telemetry.S4NET.json"))))
      .satisfiesExactly(
        x -> assertThat(x).startsWith("{\"dotnetenterprise.s4net.params.sonar_scanner_scanall.value"),
        x -> assertThat(x).startsWith("{\"dotnetenterprise.s4net.params.sonar_scanner_scanall.source")
      );

    assertThat(readContents(sonarQubeOutDirectory.resolve("Telemetry.Targets.S4NET.json")))
      .satisfiesExactly(
        x -> assertThat(x).startsWith("{\"dotnetenterprise.s4net.build.visual_studio_version\":"),
        x -> assertThat(x).startsWith("{\"dotnetenterprise.s4net.build.msbuild_tools_version\":")
      );

    for (int i = 0; i < 4; i++) {
      assertThat(readContents(sonarQubeOutDirectory.resolve(String.valueOf(i)).resolve("Telemetry.json"))).satisfiesExactly(
        x -> assertThat(x).startsWith("{\"dotnetenterprise.s4net.build.target_framework_moniker\":"));
    }
  }

  private List<String> readContents(Path path) throws IOException  {
    List<String> content = Files.readAllLines(path, StandardCharsets.UTF_8);
    // see: https://stackoverflow.com/a/73590918
    content.replaceAll(x -> x.replace("\uFEFF", ""));
    return content;
  }
}

