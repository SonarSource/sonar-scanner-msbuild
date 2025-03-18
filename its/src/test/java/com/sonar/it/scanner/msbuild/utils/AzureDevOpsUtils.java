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

public class AzureDevOpsUtils {

  public final static String TF_BUILD = "TF_BUILD";
  public final static String BUILD_BUILDURI = "BUILD_BUILDURI";
  public final static String AGENT_BUILDDIRECTORY = "AGENT_BUILDDIRECTORY";
  public final static String BUILD_SOURCESDIRECTORY = "BUILD_SOURCESDIRECTORY";

  public static Boolean isRunningUnderAzureDevOps() {
    return System.getenv(AGENT_BUILDDIRECTORY) != null;
  }

  public static String buildSourcesDirectory() {
    return System.getenv(BUILD_SOURCESDIRECTORY);
  }

  public static String agentBuildDirectory() {
    return System.getenv(AGENT_BUILDDIRECTORY);
  }
}
