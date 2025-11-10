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
package com.sonar.it.scanner.msbuild.sonarcloud;

import com.sonar.it.scanner.msbuild.utils.OSPlatform;

public class CloudConstants {
  // These ITs run against SQ-C staging environment. Values are configured in "sonar-scanner-dotnet-variables" library in Azure DevOps.
  // There exists a dedicated organization s4net-its. Any user that has Execute Analysis permission (explicitly, or by being an owner) can create a new token.
  // As of 2025, s4net-its owners are: Pavel, Martin, Tim.
  // When debugging against a different organization, don't forget that SQ-C project keys must be UNIQUE across all organizations.
  public static final String SONARCLOUD_ORGANIZATION = System.getenv("SONARCLOUD_ORGANIZATION");
  public static final String SONARCLOUD_URL = System.getenv("SONARCLOUD_URL");
  public static final String SONARCLOUD_API_URL = System.getenv("SONARCLOUD_API_URL");
  public static final String SONARCLOUD_TOKEN = OSPlatform.isWindows() ? "%SONARCLOUD_PROJECT_TOKEN%" : "$SONARCLOUD_PROJECT_TOKEN";
}
