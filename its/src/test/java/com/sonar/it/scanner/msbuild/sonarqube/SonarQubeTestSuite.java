/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
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

import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.container.Edition;
import com.sonar.orchestrator.locator.FileLocation;
import org.junit.ClassRule;
import org.junit.runner.RunWith;
import org.junit.runners.Suite;
import org.junit.runners.Suite.SuiteClasses;

@RunWith(Suite.class)
@SuiteClasses({
  CppTest.class,
  SQLServerTest.class,
  ScannerMSBuildTest.class
})
public class SonarQubeTestSuite {

  @ClassRule
  public static final Orchestrator ORCHESTRATOR = Orchestrator.builderEnv()
    .useDefaultAdminCredentialsForBuilds(true)
    .setSonarVersion(TestUtils.replaceLtsVersion(System.getProperty("sonar.runtimeVersion", "DEV")))
    .setEdition(Edition.DEVELOPER)
    .addPlugin(TestUtils.getMavenLocation("com.sonarsource.cpp", "sonar-cfamily-plugin", System.getProperty("sonar.cfamilyplugin.version", "LATEST_RELEASE")))
    .addPlugin(FileLocation.of(TestUtils.getCustomRoslynPlugin().toFile()))
    .addPlugin(TestUtils.getMavenLocation("org.sonarsource.dotnet", "sonar-csharp-plugin", System.getProperty("sonar.csharpplugin.version", "DEV")))
    .addPlugin(TestUtils.getMavenLocation("org.sonarsource.dotnet", "sonar-vbnet-plugin", System.getProperty("sonar.vbnetplugin.version", "DEV")))
    .addPlugin(TestUtils.getMavenLocation("org.sonarsource.javascript", "sonar-javascript-plugin", System.getProperty("sonar.javascriptplugin.version", "9.13.0.20537")))
    .activateLicense()
    .build();

}
