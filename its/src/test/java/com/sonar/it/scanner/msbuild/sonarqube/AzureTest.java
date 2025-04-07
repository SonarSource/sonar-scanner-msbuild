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

import static org.assertj.core.api.Assertions.assertThat;


@ExtendWith({ServerTests.class, ContextExtension.class})
class AzureTest {

  @Test
  void AzureEnvVariables_WrongEnvVariableCase_FailInUnix_SucceedsInWindows() {
//    var context = AnalysisContext.forServer("CSharp.SDK.8");
//    // Simulate Azure Devops: SonarQube.Integration.ImportBefore.targets determines paths based on these environment variables.
//    var result = context
//      .setEnvironmentVariable("TF_Build", "true")             // Simulate Azure Devops CI environment
//      .setEnvironmentVariable(AzureDevOps.BUILD_BUILDURI, "fake-uri")   //Must have value (can be anything)
//      .setEnvironmentVariable(AzureDevOps.BUILD_SOURCESDIRECTORY, context.projectDir.toString())
//      .runAnalysis()
//      .end()
//      .isSuccess();
//    assertThat(System.getProperty("os.name")).isEqualTo("windows");

    if(System.getProperty("os.name").toLowerCase().startsWith("windows"))
    {
      assertThat(System.getProperty("os.name")).isEqualTo("windows");
      //assertThat(result).isTrue();
    }
    else
    {
      assertThat(System.getProperty("os.name")).isEqualTo("linux");
     // assertThat(result).isTrue();
    }
  }
}
