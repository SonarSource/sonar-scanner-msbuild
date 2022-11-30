/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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
package com.sonar.it.scanner;

import com.sonar.it.scanner.msbuild.CppTest;
import com.sonar.it.scanner.msbuild.SQLServerTest;
import com.sonar.it.scanner.msbuild.ScannerMSBuildTest;
import com.sonar.it.scanner.msbuild.TestUtils;
import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.container.Edition;
import com.sonar.orchestrator.locator.FileLocation;
import org.junit.ClassRule;
import org.junit.runner.RunWith;
import org.junit.runners.Suite;
import org.junit.runners.Suite.SuiteClasses;

@RunWith(Suite.class)
@SuiteClasses({ScannerMSBuildTest.class})
public class SonarScannerTestSuite {

  @ClassRule
  public static final Orchestrator ORCHESTRATOR = Orchestrator.builderEnv()
    .useDefaultAdminCredentialsForBuilds(true)
    .setSonarVersion(TestUtils.replaceLtsVersion(System.getProperty("sonar.runtimeVersion", "LATEST_RELEASE")))
    .setEdition(Edition.DEVELOPER)
    .addPlugin(TestUtils.getMavenLocation("com.sonarsource.cpp", "sonar-cfamily-plugin", System.getProperty("sonar.cfamilyplugin.version", "LATEST_RELEASE")))
    .addPlugin(FileLocation.of(TestUtils.getCustomRoslynPlugin().toFile()))
    .addPlugin(TestUtils.getMavenLocation("org.sonarsource.dotnet", "sonar-csharp-plugin", System.getProperty("sonar.csharpplugin.version", "LATEST_RELEASE")))
    .addPlugin(TestUtils.getMavenLocation("org.sonarsource.dotnet", "sonar-vbnet-plugin", System.getProperty("sonar.vbnetplugin.version", "LATEST_RELEASE")))
    .activateLicense()
    .build();

}
