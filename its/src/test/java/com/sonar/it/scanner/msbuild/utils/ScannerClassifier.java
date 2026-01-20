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
package com.sonar.it.scanner.msbuild.utils;

import com.sonar.orchestrator.util.command.Command;
import java.nio.file.Path;

public enum ScannerClassifier {
  NET("net", "dotnet", Path.of("../build/sonarscanner-net/SonarScanner.MSBuild.dll").toAbsolutePath().toString()),
  NET_FRAMEWORK("net-framework", Path.of("../build/sonarscanner-net-framework/SonarScanner.MSBuild.exe").toAbsolutePath().toString(), null);

  private final String classifier;
  private final String executable;
  private final String firstArgument;

  ScannerClassifier(String classifier, String executable, String firstArgument) {
    this.classifier = classifier;
    this.executable = executable;
    this.firstArgument = firstArgument;
  }

  public Command createBaseCommand() {
    var command = Command.create(executable);
    if (firstArgument != null) {
      command.addArgument(firstArgument);
    }
    return command;
  }

  public String toString() {
    return classifier;
  }
}
