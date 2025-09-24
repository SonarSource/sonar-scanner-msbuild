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
package com.sonar.it.scanner.msbuild.sonarqube;

import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.ScannerCommand;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.version.Version;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assumptions.assumeThat;

@ExtendWith({ContextExtension.class})
class ProvisioningServerSettingTest {
  private static final String DIRECTORY_NAME = "JreProvisioning";

  @Test
  void jreAutoProvisioning_disabled() {
    // sonar.jreAutoProvisioning.disabled is a server wide setting and errors with "Setting 'sonar.jreAutoProvisioning.disabled' cannot be set on a Project"
    // We need our own server instance here so we do not interfere with other JRE tests.
    var orchestrator = ServerTests.orchestratorBuilder()
      .activateLicense()
      .setServerProperty("sonar.jreAutoProvisioning.disabled", "true")
      .build();
    var server = orchestrator.install();
    assumeThat(server.version()).as("sonar.jreAutoProvisioning.disabled was introduced in SQS 2025.6").isGreaterThanOrEqualTo(Version.create("2025.6"));
    orchestrator.start();
    try {
      var begin = ScannerCommand.createBeginStep(
          ScannerClassifier.NET,
          orchestrator.getDefaultAdminToken(),
          TestUtils.projectDir(ContextExtension.currentTempDir(), DIRECTORY_NAME),
          ContextExtension.currentTestName())
        .setDebugLogs()
        .setProperty("sonar.scanner.skipJreProvisioning", "false")
        .execute(orchestrator);
      assertThat(begin.getLogs())
        .contains("JreResolver: Resolving JRE path.")
        .contains("WARNING: JRE Metadata could not be retrieved from analysis/jres")
        .contains("JreResolver: Metadata could not be retrieved.")
        .as("An empty list of JREs is supposed to be invalid. Therefore a single retry is attempted.")
        .containsOnlyOnce("JreResolver: Resolving JRE path. Retrying...");
    } finally {
      orchestrator.stop();
    }
  }
}
