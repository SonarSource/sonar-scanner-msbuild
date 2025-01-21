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

import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.locator.Location;

import java.nio.file.Paths;

public enum ScannerClassifier {
  NET("net"),
  NET_FRAMEWORK("net-framework");

  private final String classifier;

  ScannerClassifier(String classifier) {
    this.classifier = classifier;
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
