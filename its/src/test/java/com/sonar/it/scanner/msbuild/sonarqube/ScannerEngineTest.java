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
import com.sonar.it.scanner.msbuild.utils.ServerMinVersion;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.it.scanner.msbuild.utils.Timeout;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import com.sonar.orchestrator.util.StreamConsumer;
import java.io.File;
import java.io.IOException;
import java.io.StringWriter;
import java.nio.file.Paths;
import java.util.Arrays;
import java.util.regex.Pattern;
import java.util.stream.Collectors;
import javax.xml.parsers.DocumentBuilderFactory;
import javax.xml.parsers.ParserConfigurationException;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.sonarqube.ws.ProjectAnalyses;
import org.sonarqube.ws.client.projectanalyses.SearchRequest;
import org.xml.sax.SAXException;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static com.sonar.it.scanner.msbuild.utils.SonarAssertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;
import static org.junit.jupiter.api.Assertions.assertTrue;

@ExtendWith({ServerTests.class, ContextExtension.class})
class ScannerEngineTest {

  @ParameterizedTest
  @ValueSource(booleans = {true, false})
  @ServerMinVersion("2025.1")
  void scannerInput_UTF8(boolean useSonarScannerCLI) {
    var context = AnalysisContext.forServer(Paths.get("ScannerEngine", "UTF8Filenames_äöü").toString());
    context.begin
      .setProperty("sonar.scanner.useSonarScannerCLI", Boolean.toString(useSonarScannerCLI))
      .setProperty("sonar.buildString", "'_äöüß_😊_ソナー") // Round trip a string property with problematic characters from the begin step to the final analysis result on the server
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

  @ParameterizedTest
  @ValueSource(booleans = {true, false})
  @ServerMinVersion("2025.1")
  void javaExe_fromPath(boolean useSonarScannerCLI) throws ParserConfigurationException, IOException, SAXException {
    // Test if java.exe is found via %PATH% when skipJreProvisioning=true and JAVA_HOME=null
    var context = AnalysisContext.forServer("Empty");
    context.begin
      .setProperty("sonar.scanner.useSonarScannerCLI", Boolean.toString(useSonarScannerCLI))
      .setProperty("sonar.scanner.skipJreProvisioning", "false") // Download a JRE we can use in %PATH%
      .execute(ORCHESTRATOR);
    var jreDetails = jreDetailsFromSonarQubeAnalysisConfig(context);
    context.begin
      .setProperty("sonar.scanner.skipJreProvisioning", "true")
      .execute(ORCHESTRATOR); // Re-run the begin step with skipJreProvisioning, so JavaExePath is no longer present in SonarQubeAnalysisConfig.xml
    context.build.execute();
    var result = context.end
      .setEnvironmentVariable("JAVA_HOME", null)
      // %PATH% must be kept, because we run "dotnet.exe". We add the path of the JRE in the beginning, so it is found first.
      .setEnvironmentVariable("PATH", jreDetails.javaExe.getParent() + File.pathSeparator + System.getenv("PATH"))
      .setEnvironmentVariable("Path", null) // Windows: "Path" is the default name, and we need to make sure there is only one PATH
      .execute(ORCHESTRATOR);
    assertThat(result.isSuccess()).isTrue();
    var logs = result.getLogs();
    // https://github.com/SonarSource/sonar-scanner-cli/blob/5.0.2.4997/src/main/java/org/sonarsource/scanner/cli/SystemInfo.java#L62-L74
    assertThat(logs).contains("Java " + jreDetails.version + " " + jreDetails.vendor);
    if (!useSonarScannerCLI) {
      assertThat(logs)
        .contains("Could not find Java in Analysis Config")
        .contains("'JAVA_HOME' environment variable not set")
        .contains("Could not find Java, falling back to using PATH: java");
      assertThat(TestUtils.scannerEngineInputJson(context))
        .hasAllSecretsRedacted()
        .containsKey("sonar.token");
    }
  }

  private static JreDetails jreDetailsFromSonarQubeAnalysisConfig(AnalysisContext context) throws ParserConfigurationException, IOException, SAXException {
    // Extract provisioned JRE from SonarQubeAnalysisConfig.xml -> <JavaExePath>
    var javaExe = new File(DocumentBuilderFactory.newInstance().newDocumentBuilder()
      .parse(context.projectDir.resolve(".sonarqube").resolve("conf").resolve("SonarQubeAnalysisConfig.xml").toFile())
      .getDocumentElement().getElementsByTagName("JavaExePath").item(0).getTextContent());
    assertThat(javaExe.exists()).isTrue();
    // Extract vendor and version from "jre/java.exe -XshowSettings:properties -version"
    var jreVersion = new StringWriter();
    CommandExecutor.create().execute(
      Command.create(javaExe.getAbsolutePath()).addArguments("-XshowSettings:properties", "-version"), new StreamConsumer.Pipe(jreVersion), Timeout.ONE_MINUTE.miliseconds);
    var jreVersionLines = jreVersion.toString().split("\\r?\\n\\s*");
    var jreVersionProps = Arrays.stream(jreVersionLines).map(elem -> elem.split("="))
      .filter(elem -> elem.length == 2)
      .collect(Collectors.toMap(e -> e[0].trim(), e -> e[1].trim()));
    return new JreDetails(javaExe, jreVersionProps.get("java.version"), jreVersionProps.get("java.vendor"));
  }

  record JreDetails(File javaExe, String version, String vendor) {
  }
}