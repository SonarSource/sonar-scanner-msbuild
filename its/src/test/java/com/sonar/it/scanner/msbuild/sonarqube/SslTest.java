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

import com.sonar.it.scanner.msbuild.utils.EnvironmentVariable;
import com.sonar.it.scanner.msbuild.utils.HttpsReverseProxy;
import com.sonar.it.scanner.msbuild.utils.SslUtils;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildFailureException;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.build.ScannerForMSBuild;
import com.sonar.orchestrator.locator.FileLocation;
import java.nio.file.Path;
import java.util.List;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.sonarqube.ws.Issues;

import static com.sonar.it.scanner.msbuild.sonarqube.Tests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assumptions.assumeThat;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assertions.fail;

@ExtendWith(Tests.class)
public class SslTest {
  private static final Logger LOG = LoggerFactory.getLogger(SslTest.class);
  private static final String PROJECT_KEY = "ssl-certificate";
  private static final String SSL_KEYSTORE_PASSWORD_ENV = "SSL_KEYSTORE_PASSWORD";
  private static final String SSL_KEYSTORE_PATH_ENV = "SSL_KEYSTORE_PATH";

  private static String keystorePath;
  private static String keystorePassword;

  @TempDir
  public Path basePath;

  /**
   * Some tests rely on the following environment variables to be set:
   * <ul>
   *   <li>SSL_KEYSTORE_PATH: path to the keystore file</li>
   *   <li>SSL_KEYSTORE_PASSWORD: password of the keystore file</li>
   * </ul>
   * <p>
   * The keystore file should be a PKCS12 file containing the certificate and the private key.
   * Prior to running the test, the keystore should be added to the Windows ROOT store:
   * <pre>
   *   certutil -f -p password -importPFX path-to-keystore
   * </pre>
   *
   * The <code>scripts\generate-and-trust-self-signed-certificate.ps1</code> script can be used to:
   * <ul>
   *   <li>generate the self-signed certificate</li>
   *   <li>add it to the system truststore</li>
   *   <li>set the environment variables</li>
   * </ul>
   */
  @BeforeAll
  public static void init() {
    keystorePath = System.getenv(SSL_KEYSTORE_PATH_ENV);

    if (keystorePath == null) {
      LOG.error("Missing environment variable: " + SSL_KEYSTORE_PATH_ENV);
      throw new IllegalStateException("Missing environment variable: " + SSL_KEYSTORE_PATH_ENV);
    }

    keystorePassword = System.getenv(SSL_KEYSTORE_PASSWORD_ENV);

    if (keystorePassword == null) {
      LOG.error("Missing environment variable: " + SSL_KEYSTORE_PASSWORD_ENV);
      throw new IllegalStateException("Missing environment variable: " + SSL_KEYSTORE_PASSWORD_ENV);
    }
  }

  @BeforeEach
  public void setUp() {
    TestUtils.reset(ORCHESTRATOR);
  }

  /**
   * Test SSL connection to SonarQube server while the certificate is trusted in the system store.
   * <p>This relies on the environment variables SSL_KEYSTORE_PATH and SSL_KEYSTORE_PASSWORD to be set.
   * <p>See the init method for more details.
   */
  @Test
  void trustedSelfSignedCertificate() throws Exception {
    var projectKey = PROJECT_KEY + "-trusted";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", "ProfileForTest");
    var server = new HttpsReverseProxy(ORCHESTRATOR.getServer().getUrl(), keystorePath, keystorePassword);
    server.start();

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(projectKey)
      .setProperty("sonar.host.url", server.getUrl())
      .setProjectName("sample")
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setProjectVersion("1.0"));

    TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

    var env = List.of(new EnvironmentVariable("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStore=" + keystorePath.replace('\\', '/') + " -Djavax.net.ssl.trustStorePassword=" + keystorePassword));
    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token, env);

    assertTrue(result.isSuccess());
    List<Issues.Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(1);
    server.stop();
  }

  @Test
  void untrustedSelfSignedCertificate() throws Exception {
    var projectKey = PROJECT_KEY + "-untrusted";
    var server = new HttpsReverseProxy(ORCHESTRATOR.getServer().getUrl(), createKeyStore("changeit"), "changeit");
    server.start();

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");

    ScannerForMSBuild build = TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(projectKey)
      .setProperty("sonar.host.url", server.getUrl())
      .setProjectName("sample")
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setProjectVersion("1.0");

    try {
      ORCHESTRATOR.executeBuild(build);
      fail("Expecting to fail during the begin with an SSL error");
    } catch (BuildFailureException e) {
      assertFalse(e.getResult().isSuccess());
      assertThat(e.getResult().getLogs())
        .contains("System.Net.WebException: The underlying connection was closed: Could not establish trust relationship for the SSL/TLS secure channel.")
        .contains("System.Security.Authentication.AuthenticationException: The remote certificate is invalid according to the validation procedure.");
    } finally {
      server.stop();
    }
  }

  @Test
  void selfSignedCertificateInGivenTrustStore() throws Exception {
    var projectKey = PROJECT_KEY + "-truststore";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", "ProfileForTest");
    var trustStorePassword = "changeit";
    var trustStorePath = createKeyStore(trustStorePassword);
    var server = new HttpsReverseProxy(ORCHESTRATOR.getServer().getUrl(), trustStorePath, trustStorePassword);
    server.start();

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(projectKey)
      .setProperty("sonar.scanner.truststorePath", trustStorePath)
      .setProperty("sonar.scanner.truststorePassword", trustStorePassword)
      .setProperty("sonar.host.url", server.getUrl())
      .setProjectName("sample")
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setProjectVersion("1.0"));

    TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);

    assertTrue(result.isSuccess());
    assertThat(result.getLogs())
      .contains("SONAR_SCANNER_OPTS")
      .contains("-Djavax.net.ssl.trustStore=\"" + trustStorePath.replace('\\', '/') + "\"")
      .contains("-Djavax.net.ssl.trustStorePassword=\"" + trustStorePassword + "\"");
    List<Issues.Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(1);
    server.stop();
  }

  @Test
  void selfSignedCertificateInGivenTrustStore_PathAndPasswordWithSpace() throws Exception {
    // We don't support spaces in the truststore path and password on Unix
    // Running this test on Linux would always fail
    assumeThat(System.getProperty("os.name")).contains("Windows");

    var projectKey = PROJECT_KEY + "-spaced-truststore";
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(projectKey, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(projectKey, "cs", "ProfileForTest");
    var trustStorePassword = "change it";
    var trustStorePath = createKeyStore(trustStorePassword, Path.of("sub", "folder with spaces"));
    var server = new HttpsReverseProxy(ORCHESTRATOR.getServer().getUrl(), trustStorePath, trustStorePassword);
    server.start();

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(projectKey)
      .setProperty("sonar.scanner.truststorePath", trustStorePath)
      .setProperty("sonar.scanner.truststorePassword", trustStorePassword)
      .setProperty("sonar.host.url", server.getUrl())
      .setProjectName("sample")
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setProjectVersion("1.0"));

    TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, projectKey, token);

    assertTrue(result.isSuccess());
    assertThat(result.getLogs())
      .contains("SONAR_SCANNER_OPTS")
      .contains("-Djavax.net.ssl.trustStore=\"" + trustStorePath.replace('\\', '/') + "\"")
      .contains("-Djavax.net.ssl.trustStorePassword=\"" + trustStorePassword + "\"");
    List<Issues.Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(1);
    server.stop();
  }

  @Test
  void unmatchedDomainNameInCertificate() throws Exception {
    var projectKey = PROJECT_KEY + "-unmatched-domain";
    var trustStorePassword = "changeit";
    var trustStorePath = createKeyStore(trustStorePassword, "not-localhost");
    var server = new HttpsReverseProxy(ORCHESTRATOR.getServer().getUrl(), trustStorePath, trustStorePassword);
    server.start();

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");

    ScannerForMSBuild build = TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(projectKey)
      .setProperty("sonar.scanner.truststorePath", trustStorePath)
      .setProperty("sonar.scanner.truststorePassword", trustStorePassword)
      .setProperty("sonar.host.url", server.getUrl())
      .setProjectName("sample")
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setProjectVersion("1.0");

    try {
      ORCHESTRATOR.executeBuild(build);
      fail("Expecting to fail during the begin with an SSL error");
    } catch (BuildFailureException e) {
      assertFalse(e.getResult().isSuccess());
      assertThat(e.getResult().getLogs())
        .contains("System.Net.WebException: The underlying connection was closed: Could not establish trust relationship for the SSL/TLS secure channel.")
        .contains("System.Security.Authentication.AuthenticationException: The remote certificate is invalid according to the validation procedure.");
    } finally {
      server.stop();
    }
  }

  private String createKeyStore(String password) {
    return createKeyStore(password, Path.of(""), "localhost");
  }

  private String createKeyStore(String password, Path subFolder) {
    return createKeyStore(password, subFolder, "localhost");
  }

  private String createKeyStore(String password, String host) {
    return createKeyStore(password, Path.of(""), host);
  }

  private String createKeyStore(String password, Path subFolder, String host) {
    var keystoreLocation = basePath.resolve(subFolder.resolve("keystore.pfx")).toAbsolutePath();
    LOG.info("Creating keystore at {}", keystoreLocation);
    return SslUtils.generateKeyStore(keystoreLocation, host, password);
  }
}
