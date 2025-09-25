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
package com.sonar.it.scanner.msbuild.others;

import com.google.gson.Gson;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import org.junit.jupiter.api.Test;

import static com.sonar.it.scanner.msbuild.utils.SonarAssertions.assertThat;

class RuntimeConfigTest {

  @Test
  void TestRuntimeConfig() throws IOException {
    Gson gson = new Gson();
    Path runtimeConfigPath = Paths.get("..", "build", "sonarscanner-net", "SonarScanner.MSBuild.runtimeconfig.json");
    assertThat(runtimeConfigPath.toAbsolutePath()).exists();

    try (var reader = Files.newBufferedReader(runtimeConfigPath)) {
      Config config = gson.fromJson(reader, Config.class);
      assertThat(config.runtimeOptions).isNotNull();
      assertThat(config.runtimeOptions.tfm).isEqualToIgnoringCase("netcoreapp3.1");
      assertThat(config.runtimeOptions.rollForward).isEqualToIgnoringCase("LatestMajor");
    }
  }

  // https://docs.microsoft.com/en-us/dotnet/core/run-time-config/#runtimeconfigjson
  private static class Config {
    public RuntimeOptions runtimeOptions;
  }

  private static class RuntimeOptions {
    public String tfm;
    public String rollForward;
  }
}
