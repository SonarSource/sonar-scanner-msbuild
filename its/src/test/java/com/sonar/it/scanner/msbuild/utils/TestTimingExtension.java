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

import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.extension.AfterAllCallback;
import org.junit.jupiter.api.extension.AfterEachCallback;
import org.junit.jupiter.api.extension.BeforeAllCallback;
import org.junit.jupiter.api.extension.BeforeEachCallback;
import org.junit.jupiter.api.extension.ExtensionContext;

public class TestTimingExtension implements BeforeAllCallback, AfterAllCallback, BeforeEachCallback, AfterEachCallback {

  private static final ExtensionContext.Namespace NAMESPACE = ExtensionContext.Namespace.create(TestTimingExtension.class);

  @Override
  public void beforeAll(ExtensionContext context) {
    context.getStore(NAMESPACE).put("classStart", System.nanoTime());
  }

  @Override
  public void afterAll(ExtensionContext context) {
    logDuration("class", context.getRequiredTestClass().getName(), context, "classStart");
  }

  @Override
  public void beforeEach(ExtensionContext context) {
    context.getStore(NAMESPACE).put("methodStart", System.nanoTime());
  }

  @Override
  public void afterEach(ExtensionContext context) {
    var name = context.getRequiredTestClass().getName() + "#" + context.getRequiredTestMethod().getName();
    logDuration("method", name + " [" + context.getDisplayName().replaceAll("\\R", " ") + "]", context, "methodStart");
  }

  private static void logDuration(String scope, String name, ExtensionContext context, String key) {
    Long start = context.getStore(NAMESPACE).remove(key, Long.class);
    if (start != null) {
      long elapsedMillis = TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - start);
      System.out.printf("[IT-TIMING] %s %s: %d ms%n", scope, name, elapsedMillis);
    }
  }
}
