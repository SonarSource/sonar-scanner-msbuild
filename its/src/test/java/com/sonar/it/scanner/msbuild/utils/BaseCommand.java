/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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
  protected final Map<String, String> environment = new HashMap<>(System.getenv());
  protected Timeout timeout = Timeout.TWO_MINUTES;

  protected abstract T self();

  public BaseCommand(Path projectDir) {
    this.projectDir = projectDir;
    // Overriding environment variables to fall back to projectBaseDir detection.
    // Our QA runs under GitHub Actions, and the surrounding environment makes S4NET and the scanner engine think
    // they're inside a normal CI run (e.g. since GITHUB_ACTION is always set, colliding with tests that simulate
    // another CI vendor via setEnvironmentVariable, which trips sonar-scanner-engine's "Multiple CI environments
    // are detected" check. GITHUB_BASE_REF is also real on any pull_request-triggered run, which makes the scanner
    // auto-detect a PR base branch that tests not expecting a CI context don't account for).
    // Individual tests that want to simulate a specific CI vendor re-add its variables explicitly.
    setEnvironmentVariable(GithubActions.GITHUB_ACTIONS, null);
    setEnvironmentVariable(GithubActions.GITHUB_ACTION, null);
    setEnvironmentVariable(GithubActions.GITHUB_BASE_REF, null);
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
