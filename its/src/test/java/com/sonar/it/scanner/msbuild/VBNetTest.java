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
package com.sonar.it.scanner.msbuild;

import static com.sonar.it.scanner.msbuild.TestSuite.ORCHESTRATOR;

import java.nio.file.Path;
import java.util.Arrays;
import java.util.List;
import java.util.stream.Collectors;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.Before;
import org.junit.ClassRule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;
import org.sonar.wsclient.issue.Issue;
import org.sonar.wsclient.issue.IssueQuery;
import org.sonar.wsclient.services.Measure;
import org.sonar.wsclient.services.Resource;
import org.sonar.wsclient.services.ResourceQuery;

import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.junit.SingleStartExternalResource;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.locator.PluginLocation;

public class VBNetTest {
  private static final String PROJECT_KEY = "my.project";
  private static final String FILE_KEY = "my.project:my.project:60FFCB5D-A35A-43B2-8FE3-F37C8F3B742B:Module1.vb"; 
  
  @ClassRule
  public static TemporaryFolder temp = TestSuite.temp;

  @ClassRule
  public static SingleStartExternalResource resource = new SingleStartExternalResource() {
    @Override
    protected void beforeAll() {
      
      ORCHESTRATOR = Orchestrator.builderEnv()
        .addPlugin(PluginLocation.of("com.sonarsource.vbnet", "sonar-vbnet-plugin", "2.5.0.240"))
        .addPlugin("fxcop")
        .activateLicense("vbnet")
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
  public void testMultiLanguage() throws Exception {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ConsoleMultiLanguage/TestQualityProfileCSharp.xml"));
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ConsoleMultiLanguage/TestQualityProfileVBNet.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "multilang");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "vbnet", "ProfileForTestVBNet");

    Path projectDir = TestUtils.projectDir(temp, "ConsoleMultiLanguage");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProjectName("multilang")
      .setProjectVersion("1.0")
      .setDebugLogs(true));

    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Rebuild");

    ORCHESTRATOR.executeBuild(TestUtils.newScanner(projectDir)
      .addArgument("end"));

    List<Issue> issues = ORCHESTRATOR.getServer().wsClient().issueClient().find(IssueQuery.create()).list();
    
    List<String> keys = issues.stream().map(i -> i.ruleKey()).collect(Collectors.toList());
    assertThat(keys).containsAll(Arrays.asList("vbnet:S3385", 
      "vbnet:S2358", 
      "fxcop-vbnet:AvoidUnusedPrivateFields", 
      "fxcop-vbnet:AvoidUncalledPrivateCode"));
    
    assertThat(getProjectMeasure("ncloc").getIntValue()).isEqualTo(23);
    assertThat(getFileMeasure("ncloc").getIntValue()).isEqualTo(10);
  }
  
  private Measure getFileMeasure(String metricKey) {
    Resource resource = ORCHESTRATOR.getServer().getWsClient().find(ResourceQuery.createForMetrics(FILE_KEY, metricKey));
    return resource != null ? resource.getMeasure(metricKey) : null;
  }

  private Measure getProjectMeasure(String metricKey) {
    Resource resource = ORCHESTRATOR.getServer().getWsClient().find(ResourceQuery.createForMetrics(PROJECT_KEY, metricKey));
    return resource != null ? resource.getMeasure(metricKey) : null;
  }
}
