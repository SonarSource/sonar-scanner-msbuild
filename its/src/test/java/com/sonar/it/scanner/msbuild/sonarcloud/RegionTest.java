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
package com.sonar.it.scanner.msbuild.sonarcloud;

import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.ScannerCommand;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import java.io.IOException;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static org.assertj.core.api.Assertions.assertThat;
import static org.junit.jupiter.api.Assertions.assertFalse;

public class RegionTest {
  private static final Logger LOG = LoggerFactory.getLogger(RegionTest.class);
  private static final String SONARCLOUD_PROJECT_KEY = "team-lang-dotnet_region-parameter";
  private static final String PROJECT_NAME = "ProjectUnderTest";

  @TempDir
  public Path basePath;

  @Test
  void region_us() throws IOException {
    var projectDir = TestUtils.projectDir(basePath, PROJECT_NAME);

    var result = ScannerCommand.createBeginStep(ScannerClassifier.NET_FRAMEWORK, null, projectDir, SONARCLOUD_PROJECT_KEY)
      .setOrganization(CloudConstants.SONARCLOUD_ORGANIZATION)
      .setProperty("sonar.region", "us")
      .setDebugLogs(true)
      .execute(null);

    assertFalse(result.isSuccess());
    assertThat(result.getLogs()).contains(
      "Server Url: https://sonarqube.us",
      "Api Url: https://api.sonarqube.us",
      "Is SonarCloud: True",
      "Downloading from https://sonarqube.us/api/settings/values?component=unknown",
      "Downloading from https://api.sonarqube.us/analysis/version",
      "Using SonarCloud.",
      "Downloading from https://sonarqube.us/api/settings/values?component=team-lang-dotnet_region-parameter...",
      "Cannot download quality profile. Check scanner arguments and the reported URL for more information.",
      "Pre-processing failed. Exit code: 1");
  }
}
