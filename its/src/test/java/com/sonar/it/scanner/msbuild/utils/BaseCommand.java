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

import java.nio.file.Path;
import java.util.HashMap;
import java.util.Map;

public abstract class BaseCommand<T extends BaseCommand<T>> {

  protected final Path projectDir;
  protected final Map<String, String> environment = new HashMap();
  protected Timeout timeout = Timeout.ONE_MINUTE;

  protected abstract T self();

  public BaseCommand(Path projectDir) {
    this.projectDir = projectDir;
    // Overriding environment variables to fall back to projectBaseDir detection
    // Because the QA runs in AZD, the surrounding environment makes S4NET think it's inside normal AZD run.
    // Therefore, it is picking up paths to .sonarqube folder like C:\sonar-ci\_work\1\.sonarqube\conf\SonarQubeAnalysisConfig.xml
    // This can be removed once we move our CI out of Azure DevOps.
    setEnvironmentVariable(AzureDevOps.TF_BUILD, "");
    setEnvironmentVariable(AzureDevOps.AGENT_BUILDDIRECTORY, "");
    setEnvironmentVariable(AzureDevOps.BUILD_SOURCESDIRECTORY, "");
  }

  public T setEnvironmentVariable(String name, String value) {
    if (value == null) {
      environment.remove(name);
    } else {
      environment.put(name, value);
    }
    return self();
  }

  public T setTimeout(Timeout timeout) {
    this.timeout = timeout;
    return self();
  }
}
