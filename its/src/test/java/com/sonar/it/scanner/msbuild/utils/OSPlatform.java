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

public class OSPlatform {
  private static OperatingSystem currentOS = null;

  public static OperatingSystem current() {
    if (currentOS == null) {
      String osName = System.getProperty("os.name").toLowerCase();
      if (osName.contains("windows")) {
        currentOS = OperatingSystem.Windows;
      // We only check for Linux as in the context of the ITs we should not have to deal with other Unix-like OS
      } else if (osName.contains("linux")) {
        currentOS = OperatingSystem.Linux;
      } else if (osName.contains("mac os")) {
        currentOS = OperatingSystem.MacOS;
      } else {
        throw new IllegalStateException("Unsupported OS: " + osName);
      }
    }
    return currentOS;
  }

  public static boolean isWindows() {
    return current() == OperatingSystem.Windows;
  }
}
