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

import java.nio.file.Paths;

public enum Workload {
  XAMARIN_BUILD_TOOLS("Microsoft.VisualStudio.Workload.XamarinBuildTools", "Xamarin\\iOS\\Xamarin.iOS.CSharp.targets"),
  ;

  private final String id;
  private final String checkFile;

  Workload(String id, String checkFile) {
    this.id = id;
    this.checkFile = checkFile;
  }
  
  public boolean isInstalled() {
    var msBuildPath = BuildCommand.msBuildPath();
    var path = Paths.get(msBuildPath, "..", "..", "..", checkFile);
    return path.toFile().exists();
  }

  public String id() {
    return id;
  }
}
