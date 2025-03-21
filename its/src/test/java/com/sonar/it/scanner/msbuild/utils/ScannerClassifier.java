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

import com.sonar.it.scanner.msbuild.sonarcloud.CloudConstants;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.locator.Location;
import com.sonar.orchestrator.util.Command;
import java.io.File;
import java.nio.file.Paths;

public enum ScannerClassifier {
  // ToDo: SCAN4NET-200 Unify, cleanup
  NET("net", "dotnet", new File("../build/sonarscanner-net/SonarScanner.MSBuild.dll").getAbsolutePath().toString()),
  NET_FRAMEWORK("net-framework", new File(CloudConstants.SCANNER_PATH).getAbsolutePath().toString(), null);

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

  public String toZipName() {
    return "sonarscanner-" + classifier + ".zip";
  }

  public Location toLocation(String scannerLocation) {
    return FileLocation.of(Paths.get(scannerLocation, toZipName()).toFile());
  }

  public boolean isDotNetCore() {
    return classifier.equals(NET.classifier);
  }
}
