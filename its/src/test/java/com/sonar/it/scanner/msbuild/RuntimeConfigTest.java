/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
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
package com.sonar.it.scanner.msbuild;

import com.google.gson.Gson;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.HashMap;
import java.util.Map;
import org.junit.Test;

import static org.assertj.core.api.Assertions.assertThat;

public class RuntimeConfigTest {

  @Test
  public void TestRuntimConfigRollForward() throws IOException {
    Map<String, String> releasedSuffixAndTfm  = new HashMap<String, String>() {{
      put("netcoreapp2.0", "netcoreapp2.1");
      put("netcoreapp3.0", "netcoreapp3.1");
      put("net5.0", "net5.0");
    }};

    Gson gson = new Gson();
    for (Map.Entry<String, String> suffixAndTfm : releasedSuffixAndTfm.entrySet()) {
      Path runtimeConfigPath = Paths.get("..", "build", "sonarscanner-msbuild-" + suffixAndTfm.getKey(), "SonarScanner.MSBuild.runtimeconfig.json");
      assertThat(runtimeConfigPath.toAbsolutePath()).exists();

      Config config = gson.fromJson(Files.newBufferedReader(runtimeConfigPath), Config.class);
      assertThat(config.runtimeOptions).isNotNull();
      assertThat(config.runtimeOptions.tfm).isEqualToIgnoringCase(suffixAndTfm.getValue());
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
