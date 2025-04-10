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

import com.sonar.it.scanner.msbuild.utils.AnalysisContext;
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.OSPlatform;
import org.junit.jupiter.api.extension.BeforeAllCallback;
import org.junit.jupiter.api.extension.ExtensionContext;

public class CloudTests implements BeforeAllCallback {

  private static volatile boolean isFirstTry = true;
  private static volatile boolean isStarted;

  @Override
  public void beforeAll(ExtensionContext extensionContext) {
    synchronized (CloudTests.class) {
      if (!isStarted) {
        if (isFirstTry) {
          isFirstTry = false;
          // To avoid a race condition in scanner file cache mechanism we analyze single project before any test to populate the cache
          analyzeEmptyProject();
          isStarted = true;
        } else if (!isStarted) {  // The second, third and any other caller should fail fast if something went wrong for the first one
          throw new IllegalStateException("Previous startup failed");
        }
      }
    }
  }

  private void analyzeEmptyProject() {
    ContextExtension.init("CloudTests.Startup." + Thread.currentThread().getName() + "." + OSPlatform.current().toString());
    AnalysisContext.forCloud("Empty").runAnalysis();
    ContextExtension.cleanup();
  }
}
