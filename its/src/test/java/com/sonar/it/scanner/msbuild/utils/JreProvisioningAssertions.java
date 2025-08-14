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

import com.sonar.orchestrator.build.BuildResult;

import static org.assertj.core.api.Assertions.assertThat;

public final class JreProvisioningAssertions {
  public static void cacheMissAssertions(AnalysisResult result, String sqApiUrl, String userHome, String oldJavaHome, String jreUrlPattern) {
    var os = OSPlatform.current().name().toLowerCase();
    var arch = OSPlatform.currentArchitecture().toLowerCase();
    oldJavaHome = oldJavaHome.replace("\\", "\\\\");
    var cacheFolderPattern = userHome.replace("\\", "\\\\") + "[\\\\/]cache.+";
    var beginLogs = result.begin().getLogs();

    assertThat(beginLogs).contains(
      "JreResolver: Resolving JRE path.",
      "Downloading from " + sqApiUrl + "/analysis/jres?os=" + os + "&arch=" + arch + "...",
      "Response received from " + sqApiUrl + "/analysis/jres?os=" + os + "&arch=" + arch + "...",
      "JreResolver: Cache miss. Attempting to download JRE.",
      "Starting the file download.");
    TestUtils.matchesSingleLine(beginLogs, "Downloading Java JRE from " + jreUrlPattern);
    TestUtils.matchesSingleLine(beginLogs, "The checksum of the downloaded file is '.+' and the expected checksum is '.+'");
    TestUtils.matchesSingleLine(beginLogs, "Starting extracting the Java runtime environment from archive '" + cacheFolderPattern + "' to folder '" + cacheFolderPattern + "'");
    TestUtils.matchesSingleLine(beginLogs, "Moving extracted Java runtime environment from '" + cacheFolderPattern + "' to '" + cacheFolderPattern + "_extracted'");
    TestUtils.matchesSingleLine(beginLogs, "The Java runtime environment was successfully added to '" + cacheFolderPattern + "_extracted'");
    TestUtils.matchesSingleLine(beginLogs, "JreResolver: Download success. JRE can be found at '" + cacheFolderPattern + "_extracted.+java(?:\\.exe)?'");
    var endLogs = result.end().getLogs();
    TestUtils.matchesSingleLine(endLogs, "Setting the JAVA_HOME for the scanner cli to " + cacheFolderPattern + "_extracted.+");
    TestUtils.matchesSingleLine(endLogs, "Overwriting the value of environment variable 'JAVA_HOME'. Old value: " + oldJavaHome + ", new value: " + cacheFolderPattern + "_extracted.+");
  }

  public static void cacheHitAssertions(BuildResult secondBegin, String userHome) {
    var javaPattern = userHome.replace("\\", "\\\\") + "[\\\\/]cache.+_extracted.+java(?:\\.exe)?";
    assertThat(secondBegin.isSuccess()).isTrue();
    TestUtils.matchesSingleLine(secondBegin.getLogs(),
      "JreResolver: Cache hit '" + javaPattern + "'");
    assertThat(secondBegin.getLogs()).doesNotContain(
      "JreResolver: Cache miss",
      "JreResolver: Cache failure",
      "Starting the file download.");
  }
}
