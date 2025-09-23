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

import com.sonar.it.scanner.msbuild.sonarqube.ServerTests;
import java.util.Arrays;
import org.junit.jupiter.api.extension.ConditionEvaluationResult;
import org.junit.jupiter.api.extension.ExecutionCondition;
import org.junit.jupiter.api.extension.ExtensionContext;

public class EditionSupportCondition implements ExecutionCondition {
  @Override
  public ConditionEvaluationResult evaluateExecutionCondition(ExtensionContext context) {
    final var method = context.getRequiredTestMethod();
    final var enableOnEdition = method.getDeclaredAnnotation(EnableOnEdition.class);
    final var disableOnEdition = method.getDeclaredAnnotation(DisableOnEdition.class);
    if (enableOnEdition == null && disableOnEdition == null) {
      return ConditionEvaluationResult.enabled("Test enabled");
    }
    var serverEdition = ServerTests.ORCHESTRATOR.getServer().getEdition();
    if (enableOnEdition != null) {
      var supported = Arrays.asList(enableOnEdition.value());
      return supported.contains(serverEdition)
        ? ConditionEvaluationResult.enabled("Edition " + serverEdition.toString() + " is supported.")
        : ConditionEvaluationResult.disabled("Edition " + serverEdition.toString() + " is not supported.");
    }
    var unsupported = Arrays.asList(disableOnEdition.value());
    return unsupported.contains(serverEdition)
      ? ConditionEvaluationResult.disabled("Edition " + serverEdition.toString() + " is not supported.")
      : ConditionEvaluationResult.enabled("Edition " + serverEdition.toString() + " is supported.");
  }
}
