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

import com.sonar.it.scanner.msbuild.utils.AnalysisContext;
import com.sonar.it.scanner.msbuild.utils.BuildCommand;
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.OSPlatform;
import com.sonar.it.scanner.msbuild.utils.QualityProfile;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.it.scanner.msbuild.utils.Timeout;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import org.assertj.core.groups.Tuple;
import org.eclipse.jgit.api.Git;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledOnOs;
import org.junit.jupiter.api.condition.OS;
import org.junit.jupiter.api.extension.ExtendWith;
import org.sonarqube.ws.Issues.Issue;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;
import static org.junit.jupiter.api.Assumptions.assumeFalse;
import static org.junit.jupiter.api.Assumptions.assumeTrue;

@ExtendWith({ServerTests.class, ContextExtension.class})
class MultiLanguageTest {

  @Test
  void bothRoslynLanguages() {
    // SonarQube 10.8 changed the way the numbers are reported. To keep the test simple we only run the test on the latest versions.
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(10, 8));
    var context = AnalysisContext.forServer("ConsoleMultiLanguage")
      .setQualityProfile(QualityProfile.CS_S1134)
      .setQualityProfile(QualityProfile.VB_S3385_S2358);
    context.begin.setProperty("sonar.scm.disabled", "false");
    // Without the .git folder the scanner would pick up file that are ignored in the .gitignore resulting in an incorrect number of lines of code.
    try (var ignored = new CreateGitFolder(context.projectDir)) {
      context.runAnalysis();
      var issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
      // 1 CS, 2 vbnet
      assertThat(issues).hasSize(3);

      assertThat(issues).extracting(Issue::getRule).containsExactlyInAnyOrder(
        "vbnet:S3385",
        "vbnet:S2358",
        "csharpsquid:S1134");

      // Program.cs 30
      // Module1.vb 10
      // App.config +6 (Reported by Xml plugin)
      assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "ncloc", ORCHESTRATOR)).isEqualTo(46);
    }
  }

  @Test
  void esprojVueWithBackend() {
    // SonarQube 10.8 changed the way the numbers are reported. To keep the test simple we only run the test on the latest versions.
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThanOrEquals(10, 8));
    assumeTrue(!OSPlatform.isWindows() || BuildCommand.msBuildPath().contains("2022")); // This test is not supported on versions older than Visual Studio 2022
    // For this test also the .vscode folder has been included in the project folder:
    // https://developercommunity.visualstudio.com/t/visual-studio-2022-freezes-when-opening-esproj-fil/1581344
    var context = AnalysisContext.forServer("VueWithAspBackend");
    context.build.setTimeout(Timeout.FIVE_MINUTES);  // Longer timeout because of npm install
    context.end.setTimeout(Timeout.FIVE_MINUTES);    // End step was timing out, JS is slow
    ORCHESTRATOR.getServer().provisionProject(context.projectKey, context.projectKey);
    context.runAnalysis();

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    var version = ORCHESTRATOR.getServer().version();
    var expectedIssues = new Tuple[]{
      tuple("csharpsquid:S1134", context.projectKey + ":AspBackend/Controllers/WeatherForecastController.cs"),
      tuple("csharpsquid:S4487", context.projectKey + ":AspBackend/Controllers/WeatherForecastController.cs"),
      tuple("csharpsquid:S6966", context.projectKey + ":AspBackend/Program.cs"),
      tuple("javascript:S1134", context.projectKey + ":src/components/HelloWorld.vue"),
      tuple("javascript:S3504", context.projectKey + ":src/components/HelloWorld.vue"),
      tuple("javascript:S3358", context.projectKey + ":vite.config.js"),
      tuple("javascript:S1134", context.projectKey + ":src/main.js"),
      tuple("css:S4666", context.projectKey + ":src/assets/base.css"),
      tuple("php:S113", context.projectKey + ":node_modules/flatted/php/flatted.php"),
      tuple("php:S1131", context.projectKey + ":node_modules/flatted/php/flatted.php"),
      tuple("php:S121", context.projectKey + ":node_modules/flatted/php/flatted.php"),
      tuple("php:S121", context.projectKey + ":node_modules/flatted/php/flatted.php"),
      tuple("php:S121", context.projectKey + ":node_modules/flatted/php/flatted.php"),
      tuple("php:S121", context.projectKey + ":node_modules/flatted/php/flatted.php"),
      tuple("php:S121", context.projectKey + ":node_modules/flatted/php/flatted.php"),
      tuple("php:S121", context.projectKey + ":node_modules/flatted/php/flatted.php"),
      tuple("php:S1780", context.projectKey + ":node_modules/flatted/php/flatted.php"),
      tuple("python:S5754", context.projectKey + ":node_modules/flatted/python/flatted.py"),
      tuple("python:S5806", context.projectKey + ":node_modules/flatted/python/flatted.py"),
      tuple("python:S5806", context.projectKey + ":node_modules/flatted/python/flatted.py")};
    if (version.isGreaterThanOrEquals(2025, 1)) {
      assertThat(issues)
        .extracting(Issue::getRule, Issue::getComponent)
        .containsExactlyInAnyOrder(expectedIssues);
    } else {
      assertThat(issues).hasSize(83);
      assertThat(issues)
        .extracting(Issue::getRule, Issue::getComponent)
        .contains(expectedIssues);
    }
    // Different expected values are for different SQ and MsBuild versions and local run
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "lines", ORCHESTRATOR)).isGreaterThan(300);
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "ncloc", ORCHESTRATOR)).isGreaterThan(200);
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "files", ORCHESTRATOR)).isGreaterThanOrEqualTo(10);
  }

  @Test
  void sdkFormat() {
    // new SDK-style format was introduced with .NET Core, we can't run .NET Core SDK under VS 2017 CI context
    assumeFalse(OSPlatform.isWindows() && BuildCommand.msBuildPath().contains("2017"));
    var context = AnalysisContext.forServer("MultiLanguageSupport");
    context.begin.setDebugLogs();
    // Begin step runs in MultiLanguageSupport
    // Build step runs in MultiLanguageSupport/src
    context.build.addArgument("src/MultiLanguageSupport.sln");
    context.end.setTimeout(Timeout.TWO_MINUTES);
    // The project needs to be inside a git repository to be able to pick up files for the sonar-text-plugin analysis
    // Otherwise the files will be ignored as not part of a scm repository
    try (var git = new CreateGitFolder(context.projectDir)) {
      git.commitAll();
      var logs = context.runAnalysis().end().getLogs();

      var issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
      var version = ORCHESTRATOR.getServer().version();
      var expectedIssues = new ArrayList<>(List.of(
        tuple("go:S1135", context.projectKey + ":main.go"),
        // "src/MultiLanguageSupport" directory
        tuple("csharpsquid:S1134", context.projectKey + ":src/MultiLanguageSupport/Program.cs"),
        tuple("javascript:S1529", context.projectKey + ":src/MultiLanguageSupport/NotIncluded.js"),
        tuple("javascript:S1529", context.projectKey + ":src/MultiLanguageSupport/JavaScript.js"),
        tuple("plsql:S1134", context.projectKey + ":src/MultiLanguageSupport/NotIncluded.sql"),
        tuple("plsql:S1134", context.projectKey + ":src/MultiLanguageSupport/plsql.sql"),
        tuple("python:S1134", context.projectKey + ":src/MultiLanguageSupport/python.py"),
        tuple("go:S1135", context.projectKey + ":src/MultiLanguageSupport/main.go"),
        // "src/MultiLanguageSupport/php" directory
        tuple("php:S1134", context.projectKey + ":src/MultiLanguageSupport/Php/PageOne.phtml"),
        // "src/" directory
        tuple("plsql:S1134", context.projectKey + ":src/Outside.sql"),
        tuple("javascript:S1529", context.projectKey + ":src/Outside.js"),
        tuple("python:S1134", context.projectKey + ":src/Outside.py"),
        tuple("go:S1135", context.projectKey + ":src/main.go"),
        // "frontend/" directory
        tuple("javascript:S1529", context.projectKey + ":frontend/PageOne.js"),
        tuple("plsql:S1134", context.projectKey + ":frontend/PageOne.Query.sql"),
        tuple("python:S1134", context.projectKey + ":frontend/PageOne.Script.py")));

      if (version.isGreaterThan(8, 9)) {
        expectedIssues.addAll(List.of(
          tuple("javascript:S2699", context.projectKey + ":frontend/PageOne.test.js"),
          tuple("php:S4833", context.projectKey + ":src/MultiLanguageSupport/Php/Composer/test.php"),
          tuple("php:S113", context.projectKey + ":src/MultiLanguageSupport/Php/Commons.inc"),
          tuple("php:S113", context.projectKey + ":src/MultiLanguageSupport/Php/PageOne.php"),
          tuple("php:S113", context.projectKey + ":src/MultiLanguageSupport/Php/PageOne.php3"),
          tuple("php:S113", context.projectKey + ":src/MultiLanguageSupport/Php/PageOne.php4"),
          tuple("php:S113", context.projectKey + ":src/Outside.php"),
          tuple("docker:S6476", context.projectKey + ":Dockerfile"),
          tuple("docker:S6476", context.projectKey + ":src/MultiLanguageSupport/Dockerfile"),
          tuple("docker:S6476", context.projectKey + ":src/MultiLanguageSupport/Dockerfile.production"),
          tuple("terraform:S4423", context.projectKey + ":src/MultiLanguageSupport/terraform.tf"),
          tuple("terraform:S4423", context.projectKey + ":src/Outside.tf")));
      }
      if (version.getMajor() == 9) {
        expectedIssues.addAll(List.of(
          tuple("php:S1808", context.projectKey + ":src/MultiLanguageSupport/Php/Composer/src/Hello.php"),
          tuple("php:S1808", context.projectKey + ":src/MultiLanguageSupport/Php/PageOne.phtml")));
      } else {
        expectedIssues.addAll(List.of(
          tuple("typescript:S1128", context.projectKey + ":frontend/PageTwo.tsx")));
      }
      if (version.isGreaterThan(9, 9)) {
        expectedIssues.addAll(List.of(
          tuple("typescript:S6481", context.projectKey + ":frontend/PageTwo.tsx"),
          tuple("azureresourcemanager:S1135", context.projectKey + ":main.bicep"),
          tuple("azureresourcemanager:S4423", context.projectKey + ":main.bicep"),
          tuple("cloudformation:S1135", context.projectKey + ":cloudformation.yaml"),
          tuple("cloudformation:S6321", context.projectKey + ":cloudformation.yaml"),
          tuple("docker:S6476", context.projectKey + ":src/MultiLanguageSupport/MultiLangSupport.dockerfile"),
          tuple("ipython:S6711", context.projectKey + ":src/Intro.ipynb"),
          tuple("java:S6437", context.projectKey + ":src/main/resources/application.properties"),
          tuple("secrets:S6703", context.projectKey + ":src/main/resources/application.properties"),
          tuple("secrets:S6702", context.projectKey + ":src/main/resources/application.yml"),
          tuple("secrets:S6702", context.projectKey + ":src/main/resources/application.yaml"),
          tuple("secrets:S6702", context.projectKey + ":.aws/config"),
          tuple("secrets:S6702", context.projectKey + ":src/file.conf"),
          tuple("secrets:S6702", context.projectKey + ":src/file.config"),
          tuple("secrets:S6702", context.projectKey + ":src/file.pem"),
          tuple("secrets:S6702", context.projectKey + ":src/script.sh"),
          tuple("secrets:S6702", context.projectKey + ":src/script.bash"),
          tuple("secrets:S6702", context.projectKey + ":src/script.ksh"),
          tuple("secrets:S6702", context.projectKey + ":src/script.ps1"),
          tuple("secrets:S6702", context.projectKey + ":src/script.zsh")));
      }
      assertThat(issues)
        .extracting(Issue::getRule, Issue::getComponent)
        .containsExactlyInAnyOrder(expectedIssues.toArray(new Tuple[]{}));
      assertThat(logs).contains("MultiLanguageSupport/src/MultiLanguageSupport/Php/Composer/vendor/autoload.php] is excluded by 'sonar.php.exclusions' " +
        "property and will not be analyzed");
    }
  }

  @Test
  void react() {
    assumeTrue(!OSPlatform.isWindows() || BuildCommand.msBuildPath().contains("2022")); // .Net 7 is supported by VS 2022 and above
    var context = AnalysisContext.forServer("MultiLanguageSupportReact");
    context.build.setTimeout(Timeout.FIVE_MINUTES);  // Longer timeout because of npm install
    context.end.setTimeout(Timeout.FIVE_MINUTES);    // End step was timing out, JS is slow
    context.runAnalysis();

    var issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    var version = ORCHESTRATOR.getServer().version();
    var expectedIssues = new ArrayList<>(List.of(
      tuple("javascript:S2819", context.projectKey + ":ClientApp/src/service-worker.js"),
      tuple("javascript:S3358", context.projectKey + ":ClientApp/src/setupProxy.js"),
      tuple("csharpsquid:S4487", context.projectKey + ":Controllers/WeatherForecastController.cs"),
      tuple("csharpsquid:S4487", context.projectKey + ":Pages/Error.cshtml.cs")
    ));
    if (version.isGreaterThan(8, 9)) {
      expectedIssues.add(tuple("python:S5754", context.projectKey + ":ClientApp/node_modules/flatted/python/flatted.py"));
    }
    if (version.isGreaterThanOrEquals(2025, 1)) {
      expectedIssues.add(tuple("csharpsquid:S6966", context.projectKey + ":Program.cs"));
    }
    assertThat(issues).hasSizeGreaterThanOrEqualTo(6)// depending on the version we see 6 or 7 issues at the moment
      .extracting(Issue::getRule, Issue::getComponent)
      .contains(expectedIssues.toArray(new Tuple[]{}));
  }

  @Test
  void angular() {
    assumeTrue(!OSPlatform.isWindows() || BuildCommand.msBuildPath().contains("2022")); // .Net 7 is supported by VS 2022 and above
    var context = AnalysisContext.forServer("MultiLanguageSupportAngular");
    context.build.setTimeout(Timeout.FIVE_MINUTES);  // Longer timeout because of npm install
    context.end.setTimeout(Timeout.FIVE_MINUTES);    // End step was timing out, JS is slow
    context.runAnalysis();

    var issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    var version = ORCHESTRATOR.getServer().version();
    var expectedIssues = new ArrayList<>(List.of(
      // "src/MultiLanguageSupport" directory
      tuple("javascript:S3358", context.projectKey + ":ClientApp/proxy.conf.js"),
      tuple("csharpsquid:S4487", context.projectKey + ":Controllers/WeatherForecastController.cs"),
      tuple("csharpsquid:S4487", context.projectKey + ":Pages/Error.cshtml.cs")));
    if (version.getMajor() == 8) {
      expectedIssues.addAll(List.of(
        tuple("csharpsquid:S3903", context.projectKey + ":Pages/Error.cshtml.cs"),
        tuple("csharpsquid:S3903", context.projectKey + ":Controllers/WeatherForecastController.cs"),
        tuple("csharpsquid:S3903", context.projectKey + ":WeatherForecast.cs")));
    }
    if (version.isGreaterThan(8, 9)) {
      expectedIssues.addAll(List.of(
        tuple("typescript:S1874", context.projectKey + ":ClientApp/src/app/fetch-data/fetch-data.component.ts"),
        tuple("typescript:S125", context.projectKey + ":ClientApp/src/environments/environment.ts"),
        tuple("typescript:S125", context.projectKey + ":ClientApp/src/polyfills.ts"),
        tuple("typescript:S125", context.projectKey + ":ClientApp/src/polyfills.ts")));
    }
    if (version.isGreaterThanOrEquals(2025, 1)) {
      expectedIssues.add(tuple("csharpsquid:S6966", context.projectKey + ":Program.cs"));
    }

    assertThat(issues)
      .filteredOn(x -> !(x.getRule().startsWith("css") || x.getRule().startsWith("python") || x.getRule().startsWith("php")))
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(expectedIssues.toArray(new Tuple[]{}));

    assertThat(issues)
      .filteredOn(x -> x.getRule().startsWith("python"))
      .extracting(Issue::getRule, Issue::getComponent)
      .contains(
        tuple("python:S5754", context.projectKey + ":ClientApp/node_modules/flatted/python/flatted.py")
      )
      .size()
      .isIn(1053, 1210, 1212, 1234); // 8.9 = 1053, 9.9 = 1210, 2025.1 = 1234

    assertThat(issues)
      .filteredOn(x -> x.getRule().startsWith("php"))
      .extracting(Issue::getRule, Issue::getComponent)
      .contains(
        tuple("php:S121", context.projectKey + ":ClientApp/node_modules/flatted/php/flatted.php")
      )
      .size()
      .isIn(6, 9, 28);

    if (ORCHESTRATOR.getServer().version().getMajor() == 8) {
      // In version 8.9 css files are handled by a dedicated plugin and node_modules are not filtered in that plugin.
      // This is because the IT are running without scm support. Normally these files are excluded by the scm ignore settings.
      assertThat(issues)
        .extracting(Issue::getRule, Issue::getComponent)
        .contains(
          tuple("css:S4649", context.projectKey + ":ClientApp/node_modules/serve-index/public/style.css"),
          tuple("css:S4654", context.projectKey + ":ClientApp/node_modules/less/test/browser/less/urls.less"),
          tuple("css:S4654", context.projectKey + ":ClientApp/node_modules/bootstrap/scss/forms/_form-check.scss"));
    }
  }

  @Test
  @EnabledOnOs(OS.WINDOWS)
  void nonSdkFormat() {
    assumeTrue(ORCHESTRATOR.getServer().version().isGreaterThan(9, 9)); // Multi-language unsupported in SQ99
    var context = AnalysisContext.forServer("MultiLanguageSupportNonSdk");
    context.runAnalysis();

    var issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    assertThat(issues).hasSize(5)
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(
        tuple("csharpsquid:S2094", context.projectKey + ":MultiLanguageSupportNonSdk/Foo.cs"),
        tuple("javascript:S1529", context.projectKey + ":MultiLanguageSupportNonSdk/Included.js"),
        tuple("javascript:S1529", context.projectKey + ":MultiLanguageSupportNonSdk/NotIncluded.js"),
        tuple("plsql:S1134", context.projectKey + ":MultiLanguageSupportNonSdk/Included.sql"),
        tuple("plsql:S1134", context.projectKey + ":MultiLanguageSupportNonSdk/NotIncluded.sql"));
  }

  // This class is used to create a .git folder in the project directory.
  // This is required for the sonar-text-plugin to work correctly.
  // For file extensions that are not owned by a specific plugin to be analyzed by the sonar-text-plugin,
  // it is required them to be part of a git repository.
  // See https://docs.sonarsource.com/sonarqube-server/latest/analyzing-source-code/languages/secrets/#adding-files-based-on-pathmatching-patterns
  public class CreateGitFolder implements AutoCloseable {
    Path gitDir;

    public CreateGitFolder(Path projectDir) {
      gitDir = projectDir.resolve(".git");
      deleteGitFolder();
      try {
        // Initialize a new repository
        Git git = Git.init().setDirectory(projectDir.toFile()).call();
        System.out.println("Initialized empty Git repository in " + git.getRepository().getDirectory());
        git.close();
      } catch (Exception ex) {
        throw new RuntimeException(ex.getMessage(), ex);
      }
    }

    // Add and commit all files of the current folder in the git repository
    public void commitAll() {
      try (var git = Git.open(gitDir.toFile())) {
        git.add().addFilepattern(".").call();
        git.commit().setMessage("Initial commit").call();
      } catch (Exception ex) {
        throw new RuntimeException(ex.getMessage(), ex);
      }
    }

    @Override
    public void close() {
      deleteGitFolder();
    }

    private void deleteGitFolder() {
      if (gitDir.toFile().exists()) {
        TestUtils.deleteDirectory(gitDir);
      }
    }
  }
}
