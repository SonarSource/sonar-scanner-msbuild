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
package com.sonar.it.scanner.msbuild.utils;

import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.build.BuildResult;
import java.nio.file.Path;

public class BuildCommand extends BaseCommand<BuildCommand> {

  private int timeout = 60 * 1000;

  public BuildCommand(Path projectDir) {
    super(projectDir);
  }

  public BuildCommand setTimeout(int timeout) {
    this.timeout = timeout;
    return this;
  }

  public BuildResult execute(Orchestrator orchestrator) {
    // ToDo: SCAN4NET-312: Extract createCommand, add support for parameters, add support for MsBuild vs dotnet. Should be similar to ScannerCommand.execute()
    // ToDo: SCAN4NET-312: Run restore & rebuild. Restore could be via NuGet (on request, or every time for simplicity?), MsBuild or dotnet
    return TestUtils.runMSBuild(orchestrator, projectDir, environment.entrySet().stream().map(x -> new EnvironmentVariable(x.getKey(), x.getValue())).toList(), timeout, "/t:Restore,Rebuild");
  }

  @Override
  protected BuildCommand self() {
    return this;
  }
}
