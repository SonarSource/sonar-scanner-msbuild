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
import com.sonar.it.scanner.msbuild.utils.OSPlatform;
import com.sonar.it.scanner.msbuild.utils.OperatingSystem;
import com.sonar.it.scanner.msbuild.utils.ServerMinVersion;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import java.nio.file.Paths;
import java.util.regex.Pattern;
import java.util.stream.Collectors;
import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.sonarqube.ws.ProjectAnalyses;
import org.sonarqube.ws.client.projectanalyses.SearchRequest;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.not;
import static org.assertj.core.api.Assertions.tuple;
import static org.assertj.core.api.Assumptions.assumeThat;
import static org.junit.jupiter.api.Assertions.assertTrue;

@ExtendWith({ServerTests.class, ContextExtension.class})
class ScannerEngineTest {

  @ParameterizedTest
  @ValueSource(booleans = { true, false })
  @ServerMinVersion("2025.1")
  void scannerInput_UTF8(boolean useSonarScannerCLI) {
    assumeThat(OSPlatform.current())
      .as("Issues raised in UTF8Filename_äöüß_ソナー_😊.cs are not ending up in the server on MacOS because C# is not detected. See https://sonarsource.atlassian.net/browse/SCAN4NET-892")
      .isNotEqualTo(OperatingSystem.MacOS);
    var context = AnalysisContext.forServer(Paths.get("ScannerEngine", "UTF8Filenames_äöü").toString());
    context.begin
      .setProperty("sonar.scanner.useSonarScannerCLI", Boolean.toString(useSonarScannerCLI))
      .setProperty("sonar.buildString", "'_äöüß_😊_ソナー") // Round trip a string property with problematic characters from the begin step to the final analysis result on the server
      .setProperty("sonar.json.activate", "false")
      .setProperty("sonar.json.file.suffixes", ".gibberish")
      .setDebugLogs(); // So we can assert filenames with problematic characters in the log output.
    var result = context.runAnalysis();

    assertTrue(result.isSuccess());
    var issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    assertThat(issues)
      .extracting(x -> tuple(x.getComponent(), x.getRule(), x.getMessage()))
      .containsExactlyInAnyOrder(
        tuple(
          context.projectKey + ":UTF8Filenames/UTF8Filename_äöüß_ソナー_😊.cs",
          "csharpsquid:S101",
          "Rename class 'UTF8Filename_äöüß_ソナー' to match pascal case naming rules, consider using 'Utf8Filenameäöüßソナー'.")
      );
    var analyses = TestUtils.newWsClient(ORCHESTRATOR).projectAnalyses().search(new SearchRequest().setProject(context.projectKey)).getAnalysesList();
    assertThat(analyses)
      .extracting(ProjectAnalyses.Analysis::getBuildString)
      .as("The round-tripped sonar.buildString property must match the input.")
      .containsExactly("'_äöüß_😊_ソナー");
    var logs = result.end().getLogs();
    var matchResults = Pattern.compile("DEBUG: 'UTF8Filenames/(UTF8Filename_.*\\.cs)' indexed with language 'cs'").matcher(logs).results();
    assertThat(matchResults)
      .extracting(x -> x.group(1))
      .hasSize(1)
      .as("Any of these can be found in the Windows/Linux/MacOS and Scanner-CLI/Scanner-Engine combination" +
        "UTF8Filename_����_???_?.cs" +
        "UTF8Filename_����_???_??.cs" +
        "UTF8Filename_????_???_??.cs" +
        "UTF8Filename_äöüß_ソナー_😊.cs")
      .allSatisfy(x -> assertThat(x).matches("UTF8Filename_.{4}_[?|ソ][?|ナ][?|ー]_[?|😊]\\??.cs"));
  }
}
