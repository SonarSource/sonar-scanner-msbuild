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

import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.locator.FileLocation;
import java.io.IOException;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.stream.Collectors;
import org.apache.commons.lang.StringUtils;
import org.assertj.core.groups.Tuple;
import org.eclipse.jgit.api.Git;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.sonarqube.ws.Issues.Issue;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assumptions.assumeFalse;
import static org.junit.jupiter.api.Assumptions.assumeTrue;

@ExtendWith(ServerTests.class)
class MultiLanguageTest {
  private static final String SONAR_RULES_PREFIX = "csharpsquid:";

  @TempDir
  public Path basePath;

  @Test
  void testMultiLanguage() throws Exception {
    // SonarQube 10.8 changed the way the numbers are reported.
    // To keep the test simple we only run the test on the latest versions.
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(10, 8));

    String projectKey = "testMultiLanguage";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ConsoleMultiLanguage/TestQualityProfileCSharp.xml"));
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ConsoleMultiLanguage/TestQualityProfileVBNet.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKey, "multilang");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", "ProfileForTestCSharp");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "vbnet", "ProfileForTestVBNet");

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    Path projectDir = TestUtils.projectDir(basePath, "ConsoleMultiLanguage");

    // Without the .git folder the scanner would pick up file that are ignored in the .gitignore
    // Resulting in an incorrect number of lines of code.
    try (var ignored = new CreateGitFolder(projectDir)) {
      TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK)
        .setProperty("sonar.scm.disabled", "false")
        .execute(ORCHESTRATOR);
      TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);
      BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);

      assertTrue(result.isSuccess());

      List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, projectKey);
      // 1 CS, 2 vbnet
      assertThat(issues).hasSize(3);

      List<String> ruleKeys = issues.stream().map(Issue::getRule).collect(Collectors.toList());
      assertThat(ruleKeys).containsAll(Arrays.asList("vbnet:S3385",
        "vbnet:S2358",
        SONAR_RULES_PREFIX + "S1134"));

      // Program.cs 30
      // Module1.vb 10
      // App.config +6 (Reported by Xml plugin)
      assertThat(TestUtils.getMeasureAsInteger(projectKey, "ncloc", ORCHESTRATOR)).isEqualTo(46);
    }
  }

  @Test
  void testEsprojVueWithBackend() throws IOException {
    // SonarQube 10.8 changed the way the numbers are reported.
    // To keep the test simple we only run the test on the latest versions.
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(10, 8));

    // For this test also the .vscode folder has been included in the project folder:
    // https://developercommunity.visualstudio.com/t/visual-studio-2022-freezes-when-opening-esproj-fil/1581344
    String projectKey = "VueWithAspBackend";
    ORCHESTRATOR.getServer().provisionProject(projectKey, projectKey);

    if (!TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2022")) {
      return; // This test is not supported on versions older than Visual Studio 22
    }

    Path projectDir = TestUtils.projectDir(basePath, projectKey);
    String token = TestUtils.getNewToken(ORCHESTRATOR);

    TestUtils.newScannerBegin(ORCHESTRATOR, projectKey, projectDir, token, ScannerClassifier.NET_FRAMEWORK).execute(ORCHESTRATOR);
    TestUtils.runNuGet(ORCHESTRATOR, projectDir, true, "restore");
    TestUtils.runMSBuild(ORCHESTRATOR, projectDir, Collections.emptyList(), 180 * 1000, "/t:Rebuild", "/nr:false");

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);
    assertTrue(result.isSuccess());

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, projectKey);
    var version = ORCHESTRATOR.getServer().version();
    var expectedIssues = new ArrayList<>(List.of(
      tuple("csharpsquid:S1134", projectKey + ":AspBackend/Controllers/WeatherForecastController.cs"),
      tuple("csharpsquid:S4487", projectKey + ":AspBackend/Controllers/WeatherForecastController.cs"),
      tuple("typescript:S3626", projectKey + ":src/components/HelloWorld.vue"),
      tuple("javascript:S2703", projectKey + ":src/main.js"),
      tuple("javascript:S2703", projectKey + ":src/main.js")));
    if (version.isGreaterThanOrEquals(2025, 1)) {
      assertThat(issues)
        .extracting(Issue::getRule, Issue::getComponent)
        .containsExactlyInAnyOrder(expectedIssues.toArray(new Tuple[]{}));
    } else {
      assertThat(issues).hasSize(83);
      assertThat(issues)
        .extracting(Issue::getRule, Issue::getComponent)
        .contains(expectedIssues.toArray(new Tuple[]{}));
    }
    // Different expected values are for different SQ and MsBuild versions and local run
    assertThat(TestUtils.getMeasureAsInteger(projectKey, "lines", ORCHESTRATOR)).isGreaterThan(300);
    assertThat(TestUtils.getMeasureAsInteger(projectKey, "ncloc", ORCHESTRATOR)).isGreaterThan(200);
    assertThat(TestUtils.getMeasureAsInteger(projectKey, "files", ORCHESTRATOR)).isGreaterThanOrEqualTo(10);
  }

  @Test
  void checkMultiLanguageSupportWithSdkFormat() throws Exception {
    // new SDK-style format was introduced with .NET Core, we can't run .NET Core SDK under VS 2017 CI context
    assumeFalse(TestUtils.getMsBuildPath(ORCHESTRATOR).toString().contains("2017"));
    Path projectDir = TestUtils.projectDir(basePath, "MultiLanguageSupport");
    // The project needs to be inside a git repository to be able to pick up files for the sonar-text-plugin analysis
    // Otherwise the files will be ignored as not part of a scm repository
    try (var ignored = new CreateGitFolder(projectDir)) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      String folderName = projectDir.getFileName().toString();
      // Begin step in MultiLanguageSupport folder
      TestUtils.newScannerBegin(ORCHESTRATOR, folderName, projectDir, token)
        .setProperty("sonar.sourceEncoding", "UTF-8")
        .setDebugLogs()
        .execute(ORCHESTRATOR);
      // Build solution inside MultiLanguageSupport/src folder
      TestUtils.runMSBuild(ORCHESTRATOR, projectDir, "/t:Restore,Rebuild", "src/MultiLanguageSupport.sln");
      // End step in MultiLanguageSupport folder
      var result = TestUtils.newScannerEnd(ORCHESTRATOR, projectDir, token).execute(ORCHESTRATOR);
      assertTrue(result.isSuccess());
      TestUtils.dumpComponentList(ORCHESTRATOR, folderName);
      TestUtils.dumpProjectIssues(ORCHESTRATOR, folderName);

      List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, folderName);
      var version = ORCHESTRATOR.getServer().version();
      var expectedIssues = new ArrayList<>(List.of(
        tuple("go:S1135", "MultiLanguageSupport:main.go"),
        // "src/MultiLanguageSupport" directory
        tuple("csharpsquid:S1134", "MultiLanguageSupport:src/MultiLanguageSupport/Program.cs"),
        tuple("javascript:S1529", "MultiLanguageSupport:src/MultiLanguageSupport/NotIncluded.js"),
        tuple("javascript:S1529", "MultiLanguageSupport:src/MultiLanguageSupport/JavaScript.js"),
        tuple("plsql:S1134", "MultiLanguageSupport:src/MultiLanguageSupport/NotIncluded.sql"),
        tuple("plsql:S1134", "MultiLanguageSupport:src/MultiLanguageSupport/plsql.sql"),
        tuple("python:S1134", "MultiLanguageSupport:src/MultiLanguageSupport/python.py"),
        tuple("go:S1135", "MultiLanguageSupport:src/MultiLanguageSupport/main.go"),
        // "src/MultiLanguageSupport/php" directory
        tuple("php:S1134", "MultiLanguageSupport:src/MultiLanguageSupport/Php/PageOne.phtml"),
        // "src/" directory
        tuple("plsql:S1134", "MultiLanguageSupport:src/Outside.sql"),
        tuple("javascript:S1529", "MultiLanguageSupport:src/Outside.js"),
        tuple("python:S1134", "MultiLanguageSupport:src/Outside.py"),
        tuple("go:S1135", "MultiLanguageSupport:src/main.go"),
        // "frontend/" directory
        tuple("javascript:S1529", "MultiLanguageSupport:frontend/PageOne.js"),
        tuple("plsql:S1134", "MultiLanguageSupport:frontend/PageOne.Query.sql"),
        tuple("python:S1134", "MultiLanguageSupport:frontend/PageOne.Script.py")));

      if (version.isGreaterThan(8, 9)) {
        expectedIssues.addAll(List.of(
          tuple("javascript:S2699", "MultiLanguageSupport:frontend/PageOne.test.js"),
          tuple("php:S4833", "MultiLanguageSupport:src/MultiLanguageSupport/Php/Composer/test.php"),
          tuple("php:S113", "MultiLanguageSupport:src/MultiLanguageSupport/Php/Commons.inc"),
          tuple("php:S113", "MultiLanguageSupport:src/MultiLanguageSupport/Php/PageOne.php"),
          tuple("php:S113", "MultiLanguageSupport:src/MultiLanguageSupport/Php/PageOne.php3"),
          tuple("php:S113", "MultiLanguageSupport:src/MultiLanguageSupport/Php/PageOne.php4"),
          tuple("php:S113", "MultiLanguageSupport:src/Outside.php"),
          tuple("docker:S6476", "MultiLanguageSupport:Dockerfile"),
          tuple("docker:S6476", "MultiLanguageSupport:src/MultiLanguageSupport/Dockerfile"),
          tuple("docker:S6476", "MultiLanguageSupport:src/MultiLanguageSupport/Dockerfile.production"),
          tuple("terraform:S4423", "MultiLanguageSupport:src/MultiLanguageSupport/terraform.tf"),
          tuple("terraform:S4423", "MultiLanguageSupport:src/Outside.tf")));
      }
      if (version.getMajor() == 9) {
        expectedIssues.addAll(List.of(
          tuple("php:S1808", "MultiLanguageSupport:src/MultiLanguageSupport/Php/Composer/src/Hello.php"),
          tuple("php:S1808", "MultiLanguageSupport:src/MultiLanguageSupport/Php/PageOne.phtml")));
      } else {
        expectedIssues.addAll(List.of(
          tuple("typescript:S1128", "MultiLanguageSupport:frontend/PageTwo.tsx")));
      }
      if (version.isGreaterThan(9, 9)) {
        expectedIssues.addAll(List.of(
          tuple("typescript:S6481", "MultiLanguageSupport:frontend/PageTwo.tsx"),
          tuple("azureresourcemanager:S1135", "MultiLanguageSupport:main.bicep"),
          tuple("azureresourcemanager:S4423", "MultiLanguageSupport:main.bicep"),
          tuple("cloudformation:S1135", "MultiLanguageSupport:cloudformation.yaml"),
          tuple("cloudformation:S6321", "MultiLanguageSupport:cloudformation.yaml"),
          tuple("docker:S6476", "MultiLanguageSupport:src/MultiLanguageSupport/MultiLangSupport.dockerfile"),
          tuple("ipython:S6711", "MultiLanguageSupport:src/Intro.ipynb"),
          tuple("java:S6437", "MultiLanguageSupport:src/main/resources/application.properties"),
          tuple("secrets:S6703", "MultiLanguageSupport:src/main/resources/application.properties"),
          tuple("secrets:S6702", "MultiLanguageSupport:src/main/resources/application.yml"),
          tuple("secrets:S6702", "MultiLanguageSupport:src/main/resources/application.yaml"),
          tuple("secrets:S6702", "MultiLanguageSupport:.aws/config"),
          tuple("secrets:S6702", "MultiLanguageSupport:src/file.conf"),
          tuple("secrets:S6702", "MultiLanguageSupport:src/file.config"),
          tuple("secrets:S6702", "MultiLanguageSupport:src/file.pem"),
          tuple("secrets:S6702", "MultiLanguageSupport:src/script.sh"),
          tuple("secrets:S6702", "MultiLanguageSupport:src/script.bash"),
          tuple("secrets:S6702", "MultiLanguageSupport:src/script.ksh"),
          tuple("secrets:S6702", "MultiLanguageSupport:src/script.ps1"),
          tuple("secrets:S6702", "MultiLanguageSupport:src/script.zsh")));
      }
      assertThat(issues)
        .extracting(Issue::getRule, Issue::getComponent)
        .containsExactlyInAnyOrder(expectedIssues.toArray(new Tuple[]{}));
      var log = result.getLogs();
      assertThat(log).contains("MultiLanguageSupport/src/MultiLanguageSupport/Php/Composer/vendor/autoload.php] is excluded by 'sonar.php.exclusions' " +
        "property and will not be analyzed");
    }
  }

  @Test
  void checkMultiLanguageSupportReact() throws Exception {
    assumeTrue(StringUtils.indexOfAny(TestUtils.getMsBuildPath(ORCHESTRATOR).toString(), new String[]{"2017", "2019"}) == -1); // "CRA target .Net 7"
    Path projectDir = TestUtils.projectDir(basePath, "MultiLanguageSupportReact");
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    String folderName = projectDir.getFileName().toString();
    // Begin step in MultiLanguageSupport folder
    TestUtils.newScannerBegin(ORCHESTRATOR, folderName, projectDir, token)
      .setProperty("sonar.sourceEncoding", "UTF-8")
      .execute(ORCHESTRATOR);
    // Build solution inside MultiLanguageSupport/src folder
    TestUtils.runMSBuild(
      ORCHESTRATOR,
      projectDir,
      Collections.emptyList(),
      TestUtils.TIMEOUT_LIMIT * 5, // Longer timeout because of npm install
      "/t:Restore,Rebuild",
      "MultiLanguageSupportReact.csproj"
    );
    // End step in MultiLanguageSupport folder
    var result = TestUtils.newScannerEnd(ORCHESTRATOR, projectDir, token).execute(ORCHESTRATOR);

    assertTrue(result.isSuccess());
    TestUtils.dumpComponentList(ORCHESTRATOR, folderName);
    TestUtils.dumpProjectIssues(ORCHESTRATOR, folderName);
    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, folderName);
    var version = ORCHESTRATOR.getServer().version();
    var expectedIssues = new ArrayList<>(List.of(
      tuple("javascript:S2819", "MultiLanguageSupportReact:ClientApp/src/service-worker.js"),
      tuple("javascript:S3358", "MultiLanguageSupportReact:ClientApp/src/setupProxy.js"),
      tuple("csharpsquid:S4487", "MultiLanguageSupportReact:Controllers/WeatherForecastController.cs"),
      tuple("csharpsquid:S4487", "MultiLanguageSupportReact:Pages/Error.cshtml.cs")
    ));
    if (version.isGreaterThan(8, 9)) {
      expectedIssues.add(tuple("python:S5754", "MultiLanguageSupportReact:ClientApp/node_modules/flatted/python/flatted.py"));
    }
    if (version.isGreaterThanOrEquals(2025, 1)) {
      expectedIssues.add(tuple("csharpsquid:S6966", "MultiLanguageSupportReact:Program.cs"));
    }
    assertThat(issues).hasSizeGreaterThanOrEqualTo(6)// depending on the version we see 6 or 7 issues at the moment
      .extracting(Issue::getRule, Issue::getComponent)
      .contains(expectedIssues.toArray(new Tuple[]{}));
  }

  @Test
  void checkMultiLanguageSupportAngular() throws Exception {
    assumeTrue(StringUtils.indexOfAny(TestUtils.getMsBuildPath(ORCHESTRATOR).toString(), new String[]{"2017", "2019"}) == -1); // .Net 7 is supported by VS 2022 and above
    Path projectDir = TestUtils.projectDir(basePath, "MultiLanguageSupportAngular");
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    String folderName = projectDir.getFileName().toString();
    // Begin step in MultiLanguageSupport folder
    TestUtils.newScannerBegin(ORCHESTRATOR, folderName, projectDir, token)
      .setProperty("sonar.sourceEncoding", "UTF-8")
      .execute(ORCHESTRATOR);
    // Build solution inside MultiLanguageSupport/src folder
    TestUtils.runMSBuild(
      ORCHESTRATOR,
      projectDir,
      Collections.emptyList(),
      TestUtils.TIMEOUT_LIMIT * 5, // Longer timeout because of npm install
      "/t:Restore,Rebuild",
      "MultiLanguageSupportAngular.csproj"
    );
    // End step in MultiLanguageSupport folder
    var result = TestUtils.newScannerEnd(ORCHESTRATOR, projectDir, token).execute(ORCHESTRATOR);
    assertTrue(result.isSuccess());
    TestUtils.dumpComponentList(ORCHESTRATOR, folderName);
    TestUtils.dumpProjectIssues(ORCHESTRATOR, folderName);

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, folderName);
    var version = ORCHESTRATOR.getServer().version();
    var expectedIssues = new ArrayList<>(List.of(
      // "src/MultiLanguageSupport" directory
      tuple("javascript:S3358", "MultiLanguageSupportAngular:ClientApp/proxy.conf.js"),
      tuple("csharpsquid:S4487", "MultiLanguageSupportAngular:Controllers/WeatherForecastController.cs"),
      tuple("csharpsquid:S4487", "MultiLanguageSupportAngular:Pages/Error.cshtml.cs")));
    if (version.getMajor() == 8) {
      expectedIssues.addAll(List.of(
        tuple("csharpsquid:S3903", "MultiLanguageSupportAngular:Pages/Error.cshtml.cs"),
        tuple("csharpsquid:S3903", "MultiLanguageSupportAngular:Controllers/WeatherForecastController.cs"),
        tuple("csharpsquid:S3903", "MultiLanguageSupportAngular:WeatherForecast.cs")));
    }
    if (version.isGreaterThan(8, 9)) {
      expectedIssues.addAll(List.of(
        tuple("typescript:S1874", "MultiLanguageSupportAngular:ClientApp/src/app/fetch-data/fetch-data.component.ts"),
        tuple("typescript:S125", "MultiLanguageSupportAngular:ClientApp/src/environments/environment.ts"),
        tuple("typescript:S125", "MultiLanguageSupportAngular:ClientApp/src/polyfills.ts"),
        tuple("typescript:S125", "MultiLanguageSupportAngular:ClientApp/src/polyfills.ts")));
    }
    if (version.isGreaterThanOrEquals(2025, 1)) {
      expectedIssues.add(tuple("csharpsquid:S6966", "MultiLanguageSupportAngular:Program.cs"));
    }

    assertThat(issues)
      .filteredOn(x -> !(x.getRule().startsWith("css") || x.getRule().startsWith("python") || x.getRule().startsWith("php")))
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(expectedIssues.toArray(new Tuple[]{}));

    assertThat(issues)
      .filteredOn(x -> x.getRule().startsWith("python"))
      .extracting(Issue::getRule, Issue::getComponent)
      .contains(
        tuple("python:S5754", "MultiLanguageSupportAngular:ClientApp/node_modules/flatted/python/flatted.py")
      )
      .size()
      .isIn(1053, 1210, 1212, 1234); // 8.9 = 1053, 9.9 = 1210, 2025.1 = 1234

    assertThat(issues)
      .filteredOn(x -> x.getRule().startsWith("php"))
      .extracting(Issue::getRule, Issue::getComponent)
      .contains(
        tuple("php:S121", "MultiLanguageSupportAngular:ClientApp/node_modules/flatted/php/flatted.php")
      )
      .size()
      .isIn(6, 9, 28);

    if (ORCHESTRATOR.getServer().version().getMajor() == 8) {
      // In version 8.9 css files are handled by a dedicated plugin and node_modules are not filtered in that plugin.
      // This is because the IT are running without scm support. Normally these files are excluded by the scm ignore settings.
      assertThat(issues)
        .extracting(Issue::getRule, Issue::getComponent)
        .contains(
          tuple("css:S4649", "MultiLanguageSupportAngular:ClientApp/node_modules/serve-index/public/style.css"),
          tuple("css:S4654", "MultiLanguageSupportAngular:ClientApp/node_modules/less/test/browser/less/urls.less"),
          tuple("css:S4654", "MultiLanguageSupportAngular:ClientApp/node_modules/bootstrap/scss/forms/_form-check.scss"));
    }
  }

  @Test
  void checkMultiLanguageSupportWithNonSdkFormat() throws Exception {
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThan(9, 9)); // Multi-language unsupported in SQ99
    var projectKey = "MultiLanguageSupportNonSdk";
    Path projectDir = TestUtils.projectDir(basePath, projectKey);

    BuildResult result = TestUtils.runAnalysis(projectDir, projectKey, false);
    assertTrue(result.isSuccess());

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, projectKey);
    assertThat(issues).hasSize(5)
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(
        tuple("csharpsquid:S2094", "MultiLanguageSupportNonSdk:MultiLanguageSupportNonSdk/Foo.cs"),
        tuple("javascript:S1529", "MultiLanguageSupportNonSdk:MultiLanguageSupportNonSdk/Included.js"),
        tuple("javascript:S1529", "MultiLanguageSupportNonSdk:MultiLanguageSupportNonSdk/NotIncluded.js"),
        tuple("plsql:S1134", "MultiLanguageSupportNonSdk:MultiLanguageSupportNonSdk/Included.sql"),
        tuple("plsql:S1134", "MultiLanguageSupportNonSdk:MultiLanguageSupportNonSdk/NotIncluded.sql"));
  }

  public class CreateGitFolder implements AutoCloseable {

    Path gitDir;

    public CreateGitFolder(Path projectDir) throws Exception {
      gitDir = projectDir.resolve(".git");
      deleteGitFolder();
      // Initialize a new repository
      Git git = Git.init().setDirectory(projectDir.toFile()).call();
      System.out.println("Initialized empty Git repository in " + git.getRepository().getDirectory());
      git.close();
    }

    @Override
    public void close() throws Exception {
      deleteGitFolder();
    }

    private void deleteGitFolder() throws Exception {
      if (gitDir.toFile().exists()) {
        TestUtils.deleteDirectory(gitDir);
      }
    }
  }
}
