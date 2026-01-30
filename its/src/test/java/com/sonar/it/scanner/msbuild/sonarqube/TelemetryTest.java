/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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
import java.util.Arrays;
import java.util.Collections;
import org.assertj.core.api.AbstractListAssert;
import org.assertj.core.api.ObjectAssert;
import org.jetbrains.annotations.NotNull;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;

import java.nio.file.Paths;
import java.util.List;
import org.sonarqube.ws.client.settings.SetRequest;

import static com.sonar.it.scanner.msbuild.utils.SonarAssertions.assertThat;

@ExtendWith({ServerTests.class, ContextExtension.class})
class TelemetryTest {

  @Test
  @MSBuildMinVersion(16)
  @ServerMinVersion("2025.3")
  void telemetry_telemetryFiles_areCorrect_CS() {
    // Setup context with custom server settings to test server settings telemetry
    var context = AnalysisContext.forServer(Paths.get("Telemetry", "Telemetry").toString());
    context.orchestrator.getServer().provisionProject(context.projectKey, context.projectKey);
    var settings = TestUtils.newWsClient(context.orchestrator).settings();
    java.util.function.Supplier<SetRequest> request = () -> new SetRequest().setComponent(context.projectKey);

    // Configure custom server settings
    settings.set(request.get()
      .setKey("sonar.cs.analyzeGeneratedCode")
      .setValue("false") // same as default, gets overridden by cli parameter
    );
    settings.set(request.get()
      .setKey("sonar.cs.analyzeRazorCode")
      .setValue("false") // overrides default
    );
    settings.set(request.get()
      .setKey("sonar.cs.dotcover.reportsPaths") // gets overridden by cli parameter
      .setValues(Collections.singletonList("**/*.dotcover.*.html"))
    );
    settings.set(request.get()
      .setKey("sonar.cs.opencover.reportsPaths")
      .setValues(Arrays.asList("opencover1.xml", "opencover2.xml"))
    );

    // Configure CLI properties (some override server settings)
    context.begin
      .setDebugLogs()
      .setProperty("sonar.cs.dotcover.reportsPaths", "dotCover.Output.html") // overrides server setting
      .setProperty("sonar.cs.analyzeGeneratedCode", "true"); // overrides server setting

    var result = context.runAnalysis();
    assertThat(result.isSuccess()).isTrue();

    assertThatEndLogMetrics(result.end()).satisfiesExactlyInAnyOrder(
      x -> assertThat(x).matches("csharp\\.cs\\.language_version\\.csharp7(_3)?=3"),
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
      // Server settings: only non-default values are reported
      // Note: Properties with default values (ignoreHeaderComments=true, analyzeGeneratedCode=false) are filtered out
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_cs_analyzerazorcode.source=SQ_SERVER_SETTINGS"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_cs_opencover_reportspaths.source=SQ_SERVER_SETTINGS"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_filesize_limit.source=SQ_SERVER_SETTINGS"),
      // CLI properties (override server settings or add new ones)
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_cs_analyzegeneratedcode.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_cs_dotcover_reportspaths.source=CLI"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.begin.runtime=netcore"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.endstep.Sarif.V1_0_0_0.Valid=True"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.endstep.legacyTFS=NotCalled"),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.visual_studio_version="),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.msbuild_version="),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.netcore_sdk_version="),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.target_framework_moniker._netstandard_version_v1_6=3"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.test_project_in_proj.false=1"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.test_project_in_proj.true=1"),
      // Telemetry/Telemetry/Telemetry.csproj has TreatWarningsAsErrors=true (1 project)
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.override_treat_warnings_as_errors.true=1"),
      // Telemetry/Telemetry/Telemetry.csproj has ErrorLog set (1 project)
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.error_log_set.set=1"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.override_warnings_as_errors.true=3"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.nuget_project_style.packagereference=3"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.using_microsoft_net_sdk.true=3"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.exclusion_proj.true=1"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.exclusion_proj.false=2"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.exclusion_file.true=1"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.deterministic.true=3"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.sonar_properties_in_project_file.set=1"));
  }

  @Test
  @MSBuildMinVersion(16)
  @ServerMinVersion("2025.3")
  void telemetry_telemetryFiles_areCorrect_VB() {
    var result = runAnalysis("TelemetryVB");
    assertThatEndLogMetrics(result.end()).satisfiesExactlyInAnyOrder(
      x -> assertThat(x).matches("vbnet\\.vbnet\\.language_version\\.visualbasic(15|16|17_13)=3"),
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
      // Server settings: only non-default values are reported
      // Note: Properties with default values (ignoreHeaderComments=true, analyzeGeneratedCode=false) are filtered out
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_cs_analyzerazorcode.source=SQ_SERVER_SETTINGS"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_filesize_limit.source=SQ_SERVER_SETTINGS"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.endstep.legacyTFS=NotCalled"),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.visual_studio_version="),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.msbuild_version="),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.netcore_sdk_version="),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.target_framework_moniker._netstandard_version_v1_6=3"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.override_warnings_as_errors.true=3"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.nuget_project_style.packagereference=3"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.using_microsoft_net_sdk.true=3"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.deterministic.true=3"));
  }

  @Test
  @MSBuildMinVersion(16)
  @ServerMinVersion("2025.3")
  void telemetry_telemetryFiles_areCorrect_CSVB_Mixed() {
    var result = runAnalysis("TelemetryCSVBMixed");
    assertThatEndLogMetrics(result.end()).satisfiesExactlyInAnyOrder(
      x -> assertThat(x).matches("vbnet\\.vbnet\\.language_version\\.visualbasic(15|16|17_13)=1"),
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
      // Server settings: only non-default values are reported
      // Note: Properties with default values (ignoreHeaderComments=true, analyzeGeneratedCode=false) are filtered out
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_cs_analyzerazorcode.source=SQ_SERVER_SETTINGS"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_filesize_limit.source=SQ_SERVER_SETTINGS"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.endstep.legacyTFS=NotCalled"),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.visual_studio_version="),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.msbuild_version="),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.netcore_sdk_version="),
      x -> assertThat(x).matches("csharp\\.cs\\.language_version\\.csharp7(_3)?=2"),
      // VB telemetry (1 VB project)
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.target_framework_moniker._netstandard_version_v1_6=1"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.nuget_project_style.packagereference=1"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.using_microsoft_net_sdk.true=1"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.deterministic.true=1"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.override_warnings_as_errors.true=1"),
      // CS telemetry (2 CS projects)
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.target_framework_moniker._netstandard_version_v1_6=2"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.nuget_project_style.packagereference=2"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.using_microsoft_net_sdk.true=2"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.deterministic.true=2"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.override_warnings_as_errors.true=2"));
  }

  @Test
  @MSBuildMinVersion(18)
  @ServerMinVersion("2025.3")
  void telemetry_multiTargetFramework_tfmsAreCorrectlyRecorded() {
    var context = AnalysisContext.forServer(Paths.get("Telemetry", "TelemetryMultiTarget").toString());
    context.begin.setDebugLogs();
    var result = context.runAnalysis();

    assertThatEndLogMetrics(result.end()).satisfiesExactlyInAnyOrder(
      x -> assertThat(x).isEqualTo("csharp.cs.language_version.csharp12=2"),
      x -> assertThat(x).isEqualTo("csharp.cs.language_version.csharp14=2"),
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
      // Server settings: only non-default values are reported
      // Note: Properties with default values (ignoreHeaderComments=true, analyzeGeneratedCode=false) are filtered out
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_cs_analyzerazorcode.source=SQ_SERVER_SETTINGS"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.params.sonar_filesize_limit.source=SQ_SERVER_SETTINGS"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.endstep.legacyTFS=NotCalled"),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.visual_studio_version="),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.msbuild_version="),
      x -> assertThat(x).startsWith("dotnetenterprise.s4net.build.netcore_sdk_version="),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.target_framework_moniker._netcoreapp_version_v8_0=2"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.target_framework_moniker._netcoreapp_version_v10_0=2"),
      // 2 projects x 2 targets = 4 builds
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.override_warnings_as_errors.true=4"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.nuget_project_style.packagereference=4"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.using_microsoft_net_sdk.true=4"),
      x -> assertThat(x).isEqualTo("dotnetenterprise.s4net.build.deterministic.true=4"));
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

