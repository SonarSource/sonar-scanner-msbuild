/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.DisableOnEdition;
import com.sonar.it.scanner.msbuild.utils.MSBuildMinVersion;
import com.sonar.it.scanner.msbuild.utils.QualityProfile;
import com.sonar.it.scanner.msbuild.utils.ServerMinVersion;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.it.scanner.msbuild.utils.Timeout;
import com.sonar.orchestrator.container.Edition;
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
import static com.sonar.it.scanner.msbuild.utils.SonarAssertions.assertThat;
import static org.assertj.core.api.Assertions.tuple;

@ExtendWith({ServerTests.class, ContextExtension.class})
class MultiLanguageTest {

  @Test
  // SonarQube 10.8 changed the way the numbers are reported. To keep the test simple we only run the test on the latest versions.
  @ServerMinVersion("10.8")
  void bothRoslynLanguages() {
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
  @EnabledOnOs({OS.WINDOWS, OS.LINUX}) // macOS fails with ERR_SSL_CIPHER_OPERATION_FAILED during npm install - see SCAN4NET-1142
  // SonarQube 10.8 changed the way the numbers are reported. To keep the test simple we only run the test on the latest versions.
  @ServerMinVersion("10.8")
  // This test is not supported on versions older than Visual Studio 2026
  @MSBuildMinVersion(18)
  @DisableOnEdition(Edition.COMMUNITY)
  void esprojVueWithBackend() {
    // For this test also the .vscode folder has been included in the project folder:
    // https://developercommunity.visualstudio.com/t/visual-studio-2022-freezes-when-opening-esproj-fil/1581344
    var context = AnalysisContext.forServer("VueWithAspBackend");
    context.begin.CreateAndSetUserHomeFolder("junit-esproj-vue-");
    context.build.setTimeout(Timeout.FIVE_MINUTES);  // Longer timeout because of npm install
    context.end.setTimeout(Timeout.FIVE_MINUTES);    // End step was timing out, JS is slow
    ORCHESTRATOR.getServer().provisionProject(context.projectKey, context.projectKey);
    context.runAnalysis();

    List<Issue> issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    var version = ORCHESTRATOR.getServer().version();
    var expectedCSIssues = new ArrayList<>(List.of(
      tuple("csharpsquid:S1134", context.projectKey + ":AspBackend/Controllers/WeatherForecastController.cs"),
      tuple("csharpsquid:S4487", context.projectKey + ":AspBackend/Controllers/WeatherForecastController.cs")));
    if (version.isGreaterThanOrEquals(2025, 1)) {
      expectedCSIssues.add(tuple("csharpsquid:S6966", context.projectKey + ":AspBackend/Program.cs"));
    }
    assertThat(issues)
      .filteredOn(x -> x.getRule().startsWith("csharpsquid"))
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(expectedCSIssues.toArray(new Tuple[]{}));
    assertThat(issues).filteredOn(x -> x.getRule().startsWith("javascript")).isNotEmpty();
    assertThat(issues).filteredOn(x -> x.getRule().startsWith("typescript")).isNotEmpty();
    assertThat(issues).filteredOn(x -> x.getRule().startsWith("php")).isNotEmpty();
    assertThat(issues).filteredOn(x -> x.getRule().startsWith("python")).isNotEmpty();
    // Different expected values are for different SQ and MsBuild versions and local run
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "lines", ORCHESTRATOR)).isGreaterThan(300);
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "ncloc", ORCHESTRATOR)).isGreaterThan(200);
    assertThat(TestUtils.getMeasureAsInteger(context.projectKey, "files", ORCHESTRATOR)).isGreaterThanOrEqualTo(9);
  }

  @Test
  // new SDK-style format was introduced with .NET Core, we can't run .NET Core SDK under VS 2017 CI context
  @MSBuildMinVersion(16)
  @DisableOnEdition(Edition.COMMUNITY)
  void sdkFormat() {
    var context = AnalysisContext.forServer("MultiLanguageSupport");
    context.begin.setDebugLogs();
    context.begin.CreateAndSetUserHomeFolder("junit-sdkFormat-");
    // Begin step runs in MultiLanguageSupport
    // Build step runs in MultiLanguageSupport/src
    context.build.addArgument("src/MultiLanguageSupport.sln");
    context.end.setTimeout(Timeout.FIVE_MINUTES);  // End step was timing out, multi-language analysis is slow
    // The project needs to be inside a git repository to be able to pick up files for the sonar-text-plugin analysis
    // Otherwise the files will be ignored as not part of a scm repository
    try (var git = new CreateGitFolder(context.projectDir)) {
      git.commitAll();
      var logs = context.runAnalysis().end().getLogs();

      var issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
      var version = ORCHESTRATOR.getServer().version();

      assertThat(issues)
        .filteredOn(x -> x.getRule().startsWith("csharpsquid"))
        .extracting(Issue::getRule, Issue::getComponent)
        .containsExactlyInAnyOrder(
          tuple("csharpsquid:S1134", context.projectKey + ":src/MultiLanguageSupport/Program.cs"));
      assertThat(issues).filteredOn(x -> x.getRule().startsWith("go")).isNotEmpty();
      assertThat(issues).filteredOn(x -> x.getRule().startsWith("javascript")).isNotEmpty();
      assertThat(issues).filteredOn(x -> x.getRule().startsWith("plsql")).isNotEmpty();
      assertThat(issues).filteredOn(x -> x.getRule().startsWith("python")).isNotEmpty();
      assertThat(issues).filteredOn(x -> x.getRule().startsWith("php")).isNotEmpty();
      if (version.isGreaterThan(8, 9)) {
        assertThat(issues).filteredOn(x -> x.getRule().startsWith("docker")).isNotEmpty();
        assertThat(issues).filteredOn(x -> x.getRule().startsWith("terraform")).isNotEmpty();
      }
      if (version.isGreaterThan(9, 9)) {
        assertThat(issues).filteredOn(x -> x.getRule().startsWith("azureresourcemanager")).isNotEmpty();
        assertThat(issues).filteredOn(x -> x.getRule().startsWith("cloudformation")).isNotEmpty();
        assertThat(issues).filteredOn(x -> x.getRule().startsWith("secrets")).isNotEmpty();
        assertThat(issues).filteredOn(x -> x.getRule().startsWith("java")).isNotEmpty();
        assertThat(issues).filteredOn(x -> x.getRule().startsWith("ipython")).isNotEmpty();
        assertThat(issues).filteredOn(x -> x.getRule().startsWith("typescript")).isNotEmpty();
      }
      assertThat(logs).contains("MultiLanguageSupport/src/MultiLanguageSupport/Php/Composer/vendor/autoload.php] is excluded by 'sonar.php.exclusions' " +
        "property and will not be analyzed");
    }
  }

  @Test
  // .Net 7 is supported by VS 2022 and above
  @MSBuildMinVersion(17)
  void react() {
    var context = AnalysisContext.forServer("MultiLanguageSupportReact");
    context.begin.CreateAndSetUserHomeFolder("junit-react-");
    context.build.setTimeout(Timeout.TEN_MINUTES);   // Longer timeout because of npm install
    context.end.setTimeout(Timeout.TWENTY_MINUTES);  // End step is timing out on macOS, JS analysis is slow - see SCAN4NET-1144
    context.runAnalysis();

    var issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    var version = ORCHESTRATOR.getServer().version();
    var expectedCSIssues = new ArrayList<>(List.of(
      tuple("csharpsquid:S4487", context.projectKey + ":Controllers/WeatherForecastController.cs"),
      tuple("csharpsquid:S4487", context.projectKey + ":Pages/Error.cshtml.cs")));
    if (version.isGreaterThanOrEquals(2025, 1)) {
      expectedCSIssues.add(tuple("csharpsquid:S6966", context.projectKey + ":Program.cs"));
    }
    assertThat(issues)
      .filteredOn(x -> x.getRule().startsWith("csharpsquid"))
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(expectedCSIssues.toArray(new Tuple[]{}));
    assertThat(issues).filteredOn(x -> x.getRule().startsWith("javascript")).isNotEmpty();
    if (version.isGreaterThan(8, 9)) {
      assertThat(issues).filteredOn(x -> x.getRule().startsWith("python")).isNotEmpty();
    }
  }

  @Test
  @EnabledOnOs({OS.WINDOWS, OS.LINUX}) // macOS fails with ERR_SSL_CIPHER_OPERATION_FAILED during npm install - see SCAN4NET-1142
  // .Net 7 is supported by VS 2022 and above
  @MSBuildMinVersion(17)
  @DisableOnEdition(Edition.COMMUNITY)
  void angular() {
    var context = AnalysisContext.forServer("MultiLanguageSupportAngular");
    context.begin.CreateAndSetUserHomeFolder("junit-angular-");
    context.build.setTimeout(Timeout.TEN_MINUTES);  // Longer timeout because of npm install
    context.end.setTimeout(Timeout.TEN_MINUTES);    // End step was timing out, JS is slow
    context.runAnalysis();

    var issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    var version = ORCHESTRATOR.getServer().version();
    var expectedCSIssues = new ArrayList<>(List.of(
      tuple("csharpsquid:S4487", context.projectKey + ":Controllers/WeatherForecastController.cs"),
      tuple("csharpsquid:S4487", context.projectKey + ":Pages/Error.cshtml.cs")));
    if (version.getMajor() == 8) {
      expectedCSIssues.addAll(List.of(
        tuple("csharpsquid:S3903", context.projectKey + ":Pages/Error.cshtml.cs"),
        tuple("csharpsquid:S3903", context.projectKey + ":Controllers/WeatherForecastController.cs"),
        tuple("csharpsquid:S3903", context.projectKey + ":WeatherForecast.cs")));
    }
    if (version.isGreaterThanOrEquals(2025, 1)) {
      expectedCSIssues.add(tuple("csharpsquid:S6966", context.projectKey + ":Program.cs"));
    }
    assertThat(issues)
      .filteredOn(x -> x.getRule().startsWith("csharpsquid"))
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(expectedCSIssues.toArray(new Tuple[]{}));
    assertThat(issues).filteredOn(x -> x.getRule().startsWith("javascript")).isNotEmpty();
    if (version.isGreaterThan(8, 9)) {
      assertThat(issues).filteredOn(x -> x.getRule().startsWith("typescript")).isNotEmpty();
    }
    if (version.isGreaterThanOrEquals(2025, 5)) {
      // Only verify githubactions analyzer is active — exact counts change with each IAC release
      assertThat(issues).filteredOn(x -> x.getRule().startsWith("githubactions")).isNotEmpty();
    }

    assertThat(issues)
      .filteredOn(x -> x.getRule().startsWith("python"))
      .extracting(Issue::getRule, Issue::getComponent)
      .contains(
        tuple("python:S5754", context.projectKey + ":ClientApp/node_modules/flatted/python/flatted.py")
      )
      .size()
      .isGreaterThan(1000);

    assertThat(issues)
      .filteredOn(x -> x.getRule().startsWith("php"))
      .extracting(Issue::getRule, Issue::getComponent)
      .contains(
        tuple("php:S121", context.projectKey + ":ClientApp/node_modules/flatted/php/flatted.php")
      )
      .size()
      .isGreaterThan(5);

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
  // Multi-language unsupported in SQ99
  @ServerMinVersion("10.0")
  @EnabledOnOs(OS.WINDOWS)
  @DisableOnEdition(Edition.COMMUNITY)
  void nonSdkFormat() {
    var context = AnalysisContext.forServer("MultiLanguageSupportNonSdk");
    context.begin.CreateAndSetUserHomeFolder("junit-nonSdkFormat-");
    context.runAnalysis();

    var issues = TestUtils.projectIssues(ORCHESTRATOR, context.projectKey);
    assertThat(issues)
      .filteredOn(x -> x.getRule().startsWith("csharpsquid"))
      .extracting(Issue::getRule, Issue::getComponent)
      .containsExactlyInAnyOrder(
        tuple("csharpsquid:S2094", context.projectKey + ":MultiLanguageSupportNonSdk/Foo.cs"));
    assertThat(issues).filteredOn(x -> x.getRule().startsWith("javascript")).isNotEmpty();
    assertThat(issues).filteredOn(x -> x.getRule().startsWith("plsql")).isNotEmpty();
  }


  // This class is used to create a .git folder in the project directory.
  // This is required for the sonar-text-plugin to work correctly.
  // For file extensions that are not owned by a specific plugin to be analyzed by the sonar-text-plugin,
  // it is required them to be part of a git repository.
  // See https://docs.sonarsource.com/sonarqube-server/2025.2/analyzing-source-code/languages/secrets/#adding-files-based-on-pathmatching-patterns
  public static class CreateGitFolder implements AutoCloseable {
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
        git.commit().setMessage("Initial commit").setSign(false).call();
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
