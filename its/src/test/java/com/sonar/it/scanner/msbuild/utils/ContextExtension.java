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

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import org.junit.jupiter.api.extension.AfterEachCallback;
import org.junit.jupiter.api.extension.BeforeEachCallback;
import org.junit.jupiter.api.extension.ExtensionContext;

public class ContextExtension implements BeforeEachCallback, AfterEachCallback {

  // IT classes run in parallel, and this keeps track of the CURRENT value from the thread where the test started.
  // Whenever a single test would use any form of multithreading, it can get a missing or wrong value here!
  private static final ThreadLocal<String> currentTestName = new ThreadLocal<>();
  private static final ThreadLocal<Path> currentTempDir = new ThreadLocal<>();

  @Override
  public void beforeEach(ExtensionContext context) throws IOException {
    init(context.getRequiredTestMethod().getName());
  }

  @Override
  public void afterEach(ExtensionContext context) {
    cleanup();
  }

  public static void init(String testName) throws IOException {
    currentTestName.set(testName);
    currentTempDir.set(Files.createTempDirectory("junit5-ContextExtension-" + testName + "-").toRealPath());
  }

  public static void cleanup() {
    TestUtils.deleteDirectory(currentTempDir());
    currentTestName.remove();
    currentTempDir.remove();
  }

  public static String currentTestName() {
    return ensureNotNull(currentTestName);
  }

  public static Path currentTempDir() {
    return ensureNotNull(currentTempDir);
  }

  private static <T> T ensureNotNull(ThreadLocal<T> threadLocal) {
    var value = threadLocal.get();
    if (value == null) {
      throw new RuntimeException("ContextExtension.currentTestName is not available. The test class is probably missing @ExtendWith({ContextExtension.class}).");
    } else {
      return value;
    }
  }
}
