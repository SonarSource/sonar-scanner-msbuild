/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

public class Constants {
  public final static Integer COMMAND_TIMEOUT = 2 * 60 * 1000;
  public final static String SCANNER_PATH = "../build/sonarscanner-net-framework/SonarScanner.MSBuild.exe";

  public final static String SONARCLOUD_ORGANIZATION = System.getenv("SONARCLOUD_ORGANIZATION");
  public final static String SONARCLOUD_URL = System.getenv("SONARCLOUD_URL");
  public final static String SONARCLOUD_API_URL = System.getenv("SONARCLOUD_API_URL");
}
