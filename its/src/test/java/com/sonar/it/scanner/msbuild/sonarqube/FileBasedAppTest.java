/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.sonarqube.ws.Components;

import static com.sonar.it.scanner.msbuild.utils.SonarAssertions.assertThat;

@ExtendWith({ServerTests.class, ContextExtension.class})
class FileBasedAppTest {

  @Test
  // File-based apps (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/preprocessor-directives#file-based-apps)
  void fileBasedApp_IsAnalyzed() {
    var context = AnalysisContext.forServer("FileBasedApp");
    context.setEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");
    // dotnet CLI only recognizes "file-based app" mode when the entry file name is the  sole build argument
    // any additional switch makes it fall back to project build
    context.build.useDotNet("build").skipExtraArgs().addArgument("app.cs");
    context.runAnalysis();

    // SCAN4NET-1694: app.cs issues are raised during complilation but not reported in the end.
    assertThat(TestUtils.listComponents(context.orchestrator, context.projectKey))
      .extracting(Components.Component::getKey)
      .containsExactlyInAnyOrder(context.projectKey + ":Lib/Greeter.cs");
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "files", context.orchestrator)).isEqualTo(1);
  }
}
