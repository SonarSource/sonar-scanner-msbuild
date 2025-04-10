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
import org.junit.jupiter.api.extension.ConditionEvaluationResult;
import org.junit.jupiter.api.extension.ExecutionCondition;
import org.junit.jupiter.api.extension.ExtensionContext;

public class MSBuildMinVersionCondition implements ExecutionCondition {
  private static int msbuildMajorVersion = -1;

  @Override
  public ConditionEvaluationResult evaluateExecutionCondition(ExtensionContext context) {
    if (OSPlatform.isWindows()) {
      final var method = context.getRequiredTestMethod();
      final var annotation = method.getDeclaredAnnotation(MSBuildMinVersion.class);
      if (annotation != null) {
        var currentVersion = getMSBuildMajorVersion();
        var minVersion = annotation.value();
        return currentVersion >= minVersion
          ? ConditionEvaluationResult.enabled("MSBuild version is " + currentVersion + ", which is greater than or equal to " + minVersion)
          : ConditionEvaluationResult.disabled("MSBuild version is " + currentVersion + ", which is less than " + minVersion);
      }
    }
    return ConditionEvaluationResult.enabled("Test enabled");
  }

  private int getMSBuildMajorVersion() {
    if (msbuildMajorVersion == -1) {
      var result = new GeneralCommand(BuildCommand.msBuildPath(), Path.of("."))
        .addArgument("-version")
        .execute();

      // Extract the second line and parse the major version
      String[] lines = result.getLogs().split("\n");
      if (lines.length > 0) {
        String versionLine = lines[lines.length - 1].trim();
        String majorVersion = versionLine.split("\\.")[0];
        msbuildMajorVersion = Integer.parseInt(majorVersion);
      }
    }
    return msbuildMajorVersion;
  }
}
