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

import com.sonar.it.scanner.msbuild.utils.TestUtils;
import java.nio.file.Files;
import java.nio.file.Path;
import org.junit.jupiter.api.extension.BeforeAllCallback;
import org.junit.jupiter.api.extension.ExtensionContext;

public class CloudTests implements BeforeAllCallback {

  private volatile boolean isStarted;

  @Override
  public void beforeAll(ExtensionContext extensionContext) throws Exception {
    synchronized (CloudTests.class) {
      if (!isStarted) {
        // To avoid a race condition in scanner file cache mechanism we analyze single project before any test to populate the cache
        analyzeEmptyProject();
        isStarted = true;
      }
    }
  }

  private void analyzeEmptyProject() throws Exception {
    Path temp = Files.createTempDirectory("CloudTests.Startup." + Thread.currentThread().getName());
    Path projectDir = TestUtils.projectDir(temp, "Empty");
    CloudUtils.runAnalysis(projectDir, "team-lang-dotnet_Empty");
    TestUtils.deleteDirectory(temp);
  }
}
