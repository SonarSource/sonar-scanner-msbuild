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

import com.sonar.it.scanner.msbuild.sonarcloud.Constants;
import com.sonar.it.scanner.msbuild.sonarqube.Tests;
import java.nio.file.Path;

public class AnalysisContext {

  public final String projectKey;
  public final Path projectDir;
  public final String token;

  public AnalysisContext(String projectKey, Path projectDir, String token) {
    this.projectKey = projectKey;
    this.projectDir = projectDir;
    this.token = token;
  }

  public static AnalysisContext forServer(String projectKey, Path temp, String directoryName) {
    return new AnalysisContext(projectKey, TestUtils.projectDir(temp, directoryName), Tests.token());
  }

  public static AnalysisContext forCloud(String projectKey, Path temp, String directoryName) {
    return new AnalysisContext(projectKey, TestUtils.projectDir(temp, directoryName), Constants.SONARCLOUD_TOKEN);
  }

}
