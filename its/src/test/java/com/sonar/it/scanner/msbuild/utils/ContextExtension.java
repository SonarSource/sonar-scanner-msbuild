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

import org.junit.jupiter.api.extension.AfterEachCallback;
import org.junit.jupiter.api.extension.BeforeEachCallback;
import org.junit.jupiter.api.extension.ExtensionContext;

public class ContextExtension implements BeforeEachCallback, AfterEachCallback {

  // IT classes run in parallel, and this keeps track of the CURRENT value from the thread where the test started.
  // Whenever a single test would use any form of multithreading, it can get a missing or wrong value here!
  private static final ThreadLocal<String> currentTestName = new ThreadLocal<>();

  @Override
  public void beforeEach(ExtensionContext context) {
    currentTestName.set(context.getRequiredTestMethod().getName());
  }

  @Override
  public void afterEach(ExtensionContext context) {
    currentTestName.remove();
  }

  public static String currentTestName() {
    var name = currentTestName.get();
    if (name == null) {
      throw new RuntimeException("ContextExtension.currentTestName is not available. The test class is probably missing @ExtendWith({ContextExtension.class}).");
    } else {
      return name;
    }
  }
}
