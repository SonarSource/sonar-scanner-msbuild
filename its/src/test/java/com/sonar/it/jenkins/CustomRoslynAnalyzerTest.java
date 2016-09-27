/*
 * Scanner for MSBuild :: Integration Tests
 * Copyright (C) 2016-2016 SonarSource SA
 * mailto:contact AT sonarsource DOT com
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
package com.sonar.it.jenkins;

import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.build.ScannerForMSBuild;
import com.sonar.orchestrator.junit.SingleStartExternalResource;
import com.sonar.orchestrator.locator.FileLocation;
import java.nio.file.Path;
import java.util.List;
import org.junit.Before;
import org.junit.ClassRule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.sonar.wsclient.issue.Issue;
import org.sonar.wsclient.issue.IssueQuery;

import static org.assertj.core.api.Assertions.assertThat;

public class CustomRoslynAnalyzerTest {
  private final static Logger LOG = LoggerFactory.getLogger(CustomRoslynAnalyzerTest.class);

  @ClassRule
  public static TemporaryFolder temp = new TemporaryFolder();

  public static Orchestrator ORCHESTRATOR;
  private static String scannerVersion;

  @ClassRule
  public static SingleStartExternalResource resource = new SingleStartExternalResource() {

    @Override
    protected void beforeAll() {
      scannerVersion = TestUtils.getScannerVersion();
      LOG.info("Using Scanner for MSBuild " + scannerVersion);
      Path modifiedCs = TestUtils.prepareCSharpPlugin(temp);
      Path customRoslyn = TestUtils.getCustomRoslynPlugin();
      ORCHESTRATOR = Orchestrator.builderEnv()
        .addPlugin(FileLocation.of(modifiedCs.toFile()))
        .addPlugin(FileLocation.of(customRoslyn.toFile()))
        .build();
      ORCHESTRATOR.start();
    }

    @Override
    protected void afterAll() {
      ORCHESTRATOR.stop();
    }
  };

  @Before
  public void cleanup() {
    ORCHESTRATOR.resetData();
  }

  @Test
  public void testSample() throws Exception {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfileCustomRoslyn.xml"));
    ORCHESTRATOR.getServer().provisionProject("foo", "Foo");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile("foo", "cs", "ProfileForTestCustomRoslyn");

    Path projectDir = TestUtils.projectDir(temp, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(ScannerForMSBuild.create(projectDir.toFile())
      .setScannerVersion(scannerVersion)
      .addArgument("begin")
      .setProjectKey("foo")
      .setProjectName("Foo")
      .setProjectVersion("1.0"));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(ScannerForMSBuild.create(projectDir.toFile())
      .setScannerVersion(scannerVersion)
      .addArgument("end"));

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    assertThat(issues).hasSize(4 + 37 + 1);
  }
}
