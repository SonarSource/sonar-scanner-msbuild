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

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;

public enum Workload {
  // When using glob patterns, Windows requires escaping backslashes, so we use double backslashes.
  // Path differ depending on the MSBuild version:
  // - MSBuild 2017: Common7\IDE\VC\VCTargets\Microsoft.Cpp.Default.props
  // - MSBuild 2019: MSBuild\Microsoft\VC\v160\Microsoft.Cpp.Default.props
  // - MSBuild 2022: MSBuild\Microsoft\VC\v170\Microsoft.Cpp.Default.props
  VC_TOOLS("Microsoft.VisualStudio.Workload.VCTools", "**\\\\Microsoft.Cpp.Default.props"),
  XAMARIN_BUILD_TOOLS("Microsoft.VisualStudio.Workload.XamarinBuildTools", "MSBuild\\Xamarin\\iOS\\Xamarin.iOS.CSharp.targets"),
  ;

  private final String id;
  private final String checkFile;

  Workload(String id, String checkFile) {
    this.id = id;
    this.checkFile = checkFile;
  }

  public boolean isInstalled() {
    var msBuildPath = BuildCommand.msBuildPath();
    // Searching from MSBuild parent folder, e.g.: C:\Program Files\Microsoft Visual Studio\2022\Community\
    var basePath = Paths.get(msBuildPath).getParent().getParent().getParent().getParent();
    if (!basePath.toFile().exists()) {
      return false;
    }
    if (!checkFile.contains("*")) {
      var file = basePath.resolve(checkFile).toFile();
      return file.exists();
    }
    try {
      var matcher = basePath.getFileSystem().getPathMatcher("glob:" + checkFile);
      try (var stream = Files.walk(basePath, 5)) {
        return stream
          .filter(Files::isRegularFile)
          .anyMatch(path -> matcher.matches(basePath.relativize(path)));
      }
    } catch (IOException e) {
      return false;
    }
  }

  public String id() {
    return id;
  }
}
