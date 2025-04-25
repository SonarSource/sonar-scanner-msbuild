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
import com.sonar.it.scanner.msbuild.utils.AzureDevOps;
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.OSPlatform;
import com.sonar.it.scanner.msbuild.utils.ScannerCommand;
import com.sonar.it.scanner.msbuild.utils.Timeout;
import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.util.Command;
import java.io.IOException;
import java.lang.reflect.InvocationTargetException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.junit.jupiter.api.Assertions.assertTrue;


@ExtendWith({ServerTests.class, ContextExtension.class})
class AzureTest {
  @ParameterizedTest
  @ValueSource(strings = {"TF_Build", "tf_build", "tf_BUILD"})
  void AzureEnvVariables_WrongCase_FailInUnix_SucceedsInWindows(String tfBuild) throws IOException {
    var sourceDir = Paths.get("src", "path").toAbsolutePath().toString();
    var sonarConfigFile = generateSonarConfigFile(tfBuild, sourceDir);

    assertThat(Files.exists(sonarConfigFile)).isTrue();
    var content = Files.readString(sonarConfigFile);
    if (OSPlatform.isWindows()) {
      assertThat(content).contains("<SourcesDirectory>" + sourceDir + "</SourcesDirectory>");
    } else {
      assertThat(content).doesNotMatch("<SourcesDirectory>.+</SourcesDirectory>");
    }
  }

  @Test
  void AzureEnvVariables_UpperCase_Succeeds() throws IOException {
    var sourceDir = Paths.get("src", "path").toAbsolutePath().toString();
    var sonarConfigFile = generateSonarConfigFile("TF_BUILD", sourceDir);

    assertThat(Files.exists(sonarConfigFile)).isTrue();
    assertThat(Files.readString(sonarConfigFile)).contains("<SourcesDirectory>" + sourceDir + "</SourcesDirectory>");
  }

  private static Path generateSonarConfigFile(String tfBuild, String sourceDir) {
    var context = AnalysisContext.forServer("Empty");
    var agentDir = context.projectDir.resolve("agent").resolve("path").toAbsolutePath();
    var sonarConfigFile = agentDir.resolve(".sonarqube").resolve("conf").resolve("SonarQubeAnalysisConfig.xml");

    context.begin
      .setEnvironmentVariable(tfBuild, "true")
      .setEnvironmentVariable(AzureDevOps.BUILD_BUILDURI, "fake-uri")
      .setEnvironmentVariable(AzureDevOps.BUILD_SOURCESDIRECTORY, sourceDir)
      .setEnvironmentVariable(AzureDevOps.AGENT_BUILDDIRECTORY, agentDir.toString());

    context.begin.execute(ORCHESTRATOR);

    return sonarConfigFile;
  }

}
