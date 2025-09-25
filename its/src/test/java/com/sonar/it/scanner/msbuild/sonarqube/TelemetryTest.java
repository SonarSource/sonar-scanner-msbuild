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
import com.sonar.orchestrator.build.BuildResult;
import org.assertj.core.api.AbstractListAssert;
import org.assertj.core.api.ObjectAssert;
import org.jetbrains.annotations.NotNull;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;

import java.io.IOException;
import java.nio.file.Paths;
import java.util.List;

import static com.sonar.it.scanner.msbuild.utils.SonarAssertions.assertThat;

@ExtendWith({ServerTests.class, ContextExtension.class})
class TelemetryTest {

  @Test
  @ServerMinVersion("2025.3")
  void telemetry_telemetryFiles_areCorrect_CS() {
    var result = runAnalysis("Telemetry");
    assertThatEndLogMetrics(result.end()).satisfiesExactlyInAnyOrder(
      x -> assertThat(x).matches("csharp\\.cs\\.language_version\\.csharp7(_3)?=3"),
      x -> assertThat(x).isEqualTo("csharp.cs.target_framework.netstandard1_6=3"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_scanner_skipjreprovisioning.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_branch_autoconfig_disabled.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_projectbasedir.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_projectbasedir.value=rooted"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_scm_disabled.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_verbose.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.serverInfo.product=SQ_Server"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.serverInfo.serverUrl=custom_url"),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.serverInfo.version="),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.jre.bootstrapping=Disabled"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.scannerEngine.bootstrapping=Enabled"),
      x -> assertThat(x).matches("dotnetenterprise\\.s4net\\.scannerEngine\\.download=CacheHit"),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.visual_studio_version="),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.msbuild_version="),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.target_framework_moniker._netstandard_version_v1_6=3"));
  }

  @Test
  @ServerMinVersion("2025.3")
  void telemetry_telemetryFiles_areCorrect_VB() {
    var result = runAnalysis("TelemetryVB");
    assertThatEndLogMetrics(result.end()).satisfiesExactlyInAnyOrder(
      x -> assertThat(x).matches("vbnet\\.vbnet\\.language_version\\.visualbasic(15|16|17_13)=3"),
      x -> assertThat(x).isEqualTo("vbnet.vbnet.target_framework.netstandard1_6=3"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_scanner_skipjreprovisioning.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_branch_autoconfig_disabled.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_projectbasedir.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_projectbasedir.value=rooted"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_scm_disabled.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_verbose.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.serverInfo.product=SQ_Server"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.serverInfo.serverUrl=custom_url"),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.serverInfo.version="),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.jre.bootstrapping=Disabled"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.scannerEngine.bootstrapping=Enabled"),
      x -> assertThat(x).matches("dotnetenterprise\\.s4net\\.scannerEngine\\.download=CacheHit"),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.visual_studio_version="),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.msbuild_version="),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.target_framework_moniker._netstandard_version_v1_6=3"));
  }

  @Test
  @ServerMinVersion("2025.3")
  void telemetry_telemetryFiles_areCorrect_CSVB_Mixed() {
    var result = runAnalysis("TelemetryCSVBMixed");
    assertThatEndLogMetrics(result.end()).satisfiesExactlyInAnyOrder(
      x -> assertThat(x).matches("vbnet\\.vbnet\\.language_version\\.visualbasic(15|16|17_13)=1"),
      x -> assertThat(x).isEqualTo("vbnet.vbnet.target_framework.netstandard1_6=1"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_scanner_skipjreprovisioning.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_branch_autoconfig_disabled.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_projectbasedir.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_projectbasedir.value=rooted"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_scm_disabled.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_verbose.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.serverInfo.product=SQ_Server"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.serverInfo.serverUrl=custom_url"),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.serverInfo.version="),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.jre.bootstrapping=Disabled"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.scannerEngine.bootstrapping=Enabled"),
      x -> assertThat(x).matches("dotnetenterprise\\.s4net\\.scannerEngine\\.download=CacheHit"),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.visual_studio_version="),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.msbuild_version="),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.target_framework_moniker._netstandard_version_v1_6=1"),
      x -> assertThat(x).matches("csharp\\.cs\\.language_version\\.csharp7(_3)?=2"),
      x -> assertThat(x).isEqualTo("csharp.cs.target_framework.netstandard1_6=2"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.target_framework_moniker._netstandard_version_v1_6=2"));
  }

  @Test
  @MSBuildMinVersion(17)
  @ServerMinVersion("2025.3")
  void telemetry_multiTargetFramework_tfmsAreCorrectlyRecorded() throws IOException {
    var context = AnalysisContext.forServer(Paths.get("Telemetry", "TelemetryMultiTarget").toString());
    context.begin.setDebugLogs();
    var result = context.runAnalysis();

    assertThatEndLogMetrics(result.end()).satisfiesExactlyInAnyOrder(
      x -> assertThat(x).isEqualTo("csharp.cs.language_version.csharp12=2"),
      x -> assertThat(x).isEqualTo("csharp.cs.language_version.csharp13=2"),
      x -> assertThat(x).isEqualTo("csharp.cs.target_framework.net8_0=2"),
      x -> assertThat(x).isEqualTo("csharp.cs.target_framework.net9_0=2"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_scanner_skipjreprovisioning.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_branch_autoconfig_disabled.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_projectbasedir.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_projectbasedir.value=rooted"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_scm_disabled.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_verbose.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.serverInfo.product=SQ_Server"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.serverInfo.serverUrl=custom_url"),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.serverInfo.version="),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.jre.bootstrapping=Disabled"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.scannerEngine.bootstrapping=Enabled"),
      x -> assertThat(x).matches("dotnetenterprise\\.s4net\\.scannerEngine\\.download=CacheHit"),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.visual_studio_version="),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.msbuild_version="),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.target_framework_moniker._netcoreapp_version_v8_0=2"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.target_framework_moniker._netcoreapp_version_v9_0=2"));
  }

  @NotNull
  private static AnalysisResult runAnalysis(String telemetryProject) {
    var context = AnalysisContext.forServer(Paths.get("Telemetry", telemetryProject).toString());
    context.begin.setDebugLogs();
    var result = context.runAnalysis();
    assertThat(result.isSuccess()).isTrue();
    return result;
  }

  private static AbstractListAssert<?, List<? extends String>, String, ObjectAssert<String>> assertThatEndLogMetrics(BuildResult result) {
    final String metricLog = "^[0-9.:]*  DEBUG: Adding metric: ";
    var endLogs = result.getLogsLines(x -> x.matches(metricLog + ".*"));
    return assertThat(endLogs).map(x -> x.replaceFirst(metricLog, ""));
  }
}

