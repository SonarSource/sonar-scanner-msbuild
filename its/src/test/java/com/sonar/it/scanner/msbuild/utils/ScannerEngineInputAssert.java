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

import java.util.List;
import java.util.Objects;
import java.util.Set;
import java.util.stream.Collectors;
import org.assertj.core.api.AbstractAssert;
import org.sonar.api.internal.apachecommons.lang3.StringUtils;

public class ScannerEngineInputAssert extends AbstractAssert<ScannerEngineInputAssert, ScannerEngineInput> {

  // Keep in sync with SonarProperties.SensitivePropertyKeys
  // https://github.com/SonarSource/sonar-scanner-msbuild/blob/master/src/SonarScanner.MSBuild.Common/SonarProperties.cs#L96
  private static final Set<String> sensitivePropertyKeys = Set.of(
    "sonar.token",
    "sonar.password",
    "sonar.login",
    "sonar.clientcert.password",
    "sonar.scanner.truststorePassword",
    "javax.net.ssl.trustStorePassword");

  protected ScannerEngineInputAssert(ScannerEngineInput scannerEngineInput) {
    super(scannerEngineInput, ScannerEngineInputAssert.class);
  }

  public static ScannerEngineInputAssert assertThat(ScannerEngineInput actual) {
    return new ScannerEngineInputAssert(actual);
  }

  /// Asserts that all secrets in ScannerEngineInput are redacted.
  public ScannerEngineInputAssert hasAllSecretsRedacted() {
    isNotNull();
    var unredactedValues = actual.scannerProperties().stream().filter(x -> sensitivePropertyKeys.contains(x.key()) && !Objects.equals(x.value(), "***")).toList();
    // check condition
    if (!unredactedValues.isEmpty()) {
      failWithMessage("ScannerInputJson should have all sensitive properties redacted, but found %s key(s) with sensitive data: %s",
        unredactedValues.size(),
        properties(unredactedValues));
    }
    var unknownRedactedValues =
      actual.scannerProperties().stream().filter(x -> !sensitivePropertyKeys.contains(x.key()) && StringUtils.equalsIgnoreCase(x.value(), "***")).toList();
    if (!unknownRedactedValues.isEmpty()) {
      failWithMessage("ScannerInputJson has %s redacted value(s), but the keys are not listed in ScannerEngineInputAssert.sensitivePropertyKeys: %s",
        unknownRedactedValues.size(),
        properties(unknownRedactedValues));
    }
    return this;
  }

  public ScannerEngineInputAssert containsKey(String key) {
    isNotNull();
    if (actual.scannerProperties().stream().noneMatch(x -> StringUtils.equalsIgnoreCase(x.key(), key))) {
      failWithMessage("ScannerInputJson misses key %s. It only contains %s", key, properties(actual.scannerProperties()));
    }
    return this;
  }

  private static String properties(List<ScannerEngineInput.ScannerProperty> properties) {
    return properties.stream().map(ScannerEngineInput.ScannerProperty::toString).collect(Collectors.joining(System.lineSeparator()));
  }
}
