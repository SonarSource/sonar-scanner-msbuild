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
import com.sonar.it.scanner.msbuild.utils.BuildCommand;
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.GeneralCommand;
import com.sonar.it.scanner.msbuild.utils.OSPlatform;
import com.sonar.it.scanner.msbuild.utils.ScannerCommand;
import com.sonar.it.scanner.msbuild.utils.Timeout;
import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.util.Command;
import java.io.IOException;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.TimeUnit;
import org.apache.commons.exec.DefaultExecutor;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.junit.jupiter.api.Assertions.assertTrue;


@ExtendWith({ServerTests.class, ContextExtension.class})
class AzureTest {
  private static final Logger LOG = LoggerFactory.getLogger(AzureTest.class);

  @ParameterizedTest
  @ValueSource(strings = {"TF_Build", "tf_build", "tf_BUILD"})
  void AzureEnvVariables_WrongCase_FailInUnix_SucceedsInWindows(String tfBuild) throws IOException {
    var sourceDir = Paths.get("src", "path").toAbsolutePath().toString();
    var sonarConfigFile = generateSonarConfigFile(tfBuild, sourceDir);

    assertThat(Files.exists(sonarConfigFile)).isTrue();
    var content = Files.readString(sonarConfigFile);
    if (OSPlatform.isWindows()) {
      LOG.info("############## ENVIRONMENT VARIABLES ##############");
      new GeneralCommand("set", Path.of(".")).execute();
      LOG.info("############ END ENVIRONMENT VARIABLES ############");
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

    // This ugly hack is needed to make sure the command is executed without any TF_BUILD environment variable already set.
    // In Azure DevOps, the TF_BUILD environment variable is set, with the ScannerCommand we emptied it, but it still exists.
    // When setting TF_BUILD with different case, Java will add a different environment variable even on Windows. However, C#
    // will read the first TF_BUILD it finds which leads to inconsistent behavior in the test.
    //
    // In order to make sure the command is executed without any TF_BUILD environment variable already set, we need to remove it.
    // Unfortunately, nor the Command or the CommandExecutor from the Orchestrator allows it, we can only add new environment variables.
    // This can be removed once we move out of Azure DevOps or the Orchestrator Command allows to remove existing environment variable.
    String[] rawCommand;
    try {
      var method = ScannerCommand.class.getDeclaredMethod("createCommand", Orchestrator.class);
      method.setAccessible(true);
      var beginCommand = (Command)method.invoke(context.begin, ORCHESTRATOR);

      method = Command.class.getDeclaredMethod("toStrings");
      method.setAccessible(true);
      rawCommand = (String[]) method.invoke(beginCommand);

    } catch (NoSuchMethodException | InvocationTargetException | IllegalAccessException e) {
      throw new RuntimeException(e);
    }

    ProcessBuilder beginProcess = new ProcessBuilder(rawCommand);
    var environment = beginProcess.environment();
    environment.remove("TF_BUILD");
    environment.put(tfBuild, "true");
    environment.put(AzureDevOps.BUILD_BUILDURI, "fake-uri");
    environment.put(AzureDevOps.BUILD_SOURCESDIRECTORY, sourceDir);
    environment.put(AzureDevOps.AGENT_BUILDDIRECTORY, agentDir.toString());

    try {
      var process = beginProcess.start();
      boolean finished = process.waitFor(Timeout.TWO_MINUTES.miliseconds, TimeUnit.MILLISECONDS);
      assertTrue(finished, "Process did not finish in time");
    } catch (IOException | InterruptedException e) {
      throw new RuntimeException(e);
    }

    return sonarConfigFile;
  }

}
