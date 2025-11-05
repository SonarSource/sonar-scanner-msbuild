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
import java.util.Optional;

import org.junit.jupiter.api.extension.ConditionEvaluationResult;
import org.junit.jupiter.api.extension.ExecutionCondition;
import org.junit.jupiter.api.extension.ExtensionContext;
import org.junit.platform.commons.util.AnnotationUtils;

public class MSBuildVersionCondition implements ExecutionCondition {
  private static int msbuildMajorVersion = -1;

  @Override
  public ConditionEvaluationResult evaluateExecutionCondition(ExtensionContext context) {
    if (OSPlatform.isWindows()) {
      final var method = context.getRequiredTestMethod();
      Optional<MSBuildMinVersion> minAnnotation = AnnotationUtils.findAnnotation(method, MSBuildMinVersion.class);
      Optional<MSBuildMaxVersion> maxAnnotation = AnnotationUtils.findAnnotation(method, MSBuildMaxVersion.class);

      if (minAnnotation.isPresent() || maxAnnotation.isPresent()) {
        var currentVersion = getMSBuildMajorVersion();
        int minVersion = minAnnotation.map(MSBuildMinVersion::value).orElse(Integer.MIN_VALUE);
        int maxVersion = maxAnnotation.map(MSBuildMaxVersion::value).orElse(Integer.MAX_VALUE);

        if (currentVersion >= minVersion && currentVersion <= maxVersion) {
          return ConditionEvaluationResult.enabled(
            String.format("MSBuild version %d satisfies constraints [min: %s, max: %s]",
              currentVersion,
              minAnnotation.isPresent() ? minVersion : "N/A",
              maxAnnotation.isPresent() ? maxVersion : "N/A"));
        }
        else {
          return ConditionEvaluationResult.disabled(
          String.format("MSBuild version check failed: MsBuild Version %d [min: %s, max: %s]",
            currentVersion,
            minAnnotation.isPresent() ? String.valueOf(minVersion) : "N/A",
            maxAnnotation.isPresent() ? String.valueOf(maxVersion) : "N/A"));
        }
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
