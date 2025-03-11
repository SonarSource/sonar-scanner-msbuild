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
import java.io.IOException;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Collections;
import java.util.List;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.api.io.TempDir;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static com.sonar.it.scanner.msbuild.sonarqube.Tests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assumptions.assumeThat;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.junit.jupiter.api.Assertions.fail;

@ExtendWith(Tests.class)
class SslTest {
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
  static void init() {
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
  void setUp() {
    TestUtils.reset(ORCHESTRATOR);
  }

  /**
   * Test SSL connection to SonarQube server while the certificate is trusted in the system store.
   * <p>This relies on the environment variables SSL_KEYSTORE_PATH and SSL_KEYSTORE_PASSWORD to be set.
   * <p>See the init method for more details.
   */
  @Test
  void trustedSelfSignedCertificate() throws Exception {
    try (var server = initSslTestAndServer(keystorePath, keystorePassword)) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProperty("sonar.host.url", server.getUrl())
        .setProjectName("sample")
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProjectVersion("1.0"));

      TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

    var env = List.of(new EnvironmentVariable("SONAR_SCANNER_OPTS",
      "-Djavax.net.ssl.trustStore=" + keystorePath.replace('\\', '/') + " -Djavax.net.ssl.trustStorePassword=" + keystorePassword));
    BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY, token, env);

      assertTrue(result.isSuccess());
    }
  }

  @Test
  void trustedSelfSignedCertificate_WindowsRoot() throws Exception {
    // The javax.net.ssl.trustStoreType=Windows-ROOT is not valid on Unix
    assumeThat(System.getProperty("os.name")).contains("Windows");

    try (var server = initSslTestAndServer(keystorePath, keystorePassword)) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProperty("sonar.host.url", server.getUrl())
        .setProjectName("sample")
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProjectVersion("1.0"));

      TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

      BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY, token);

      assertTrue(result.isSuccess());
      assertThat(result.getLogs())
        .contains("SONAR_SCANNER_OPTS")
        .contains("-Djavax.net.ssl.trustStoreType=Windows-ROOT");
    }
  }

  @Test
  void trustedSelfSignedCertificate_ExistingValueInScannerOpts() throws Exception {
    try (var server = initSslTestAndServer(keystorePath, keystorePassword)) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProperty("sonar.host.url", server.getUrl())
        .setProjectName("sample")
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProjectVersion("1.0"));

      TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

      var env = List.of(new EnvironmentVariable("SONAR_SCANNER_OPTS", "-Xmx2048m"));
      BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY, token, env);

      assertTrue(result.isSuccess());
      assertThat(result.getLogs())
        .contains("SONAR_SCANNER_OPTS=-Xmx2048m");
    }
  }

  @Test
  void untrustedSelfSignedCertificate() throws Exception {
    try (var server = initSslTestAndServerWithTrustStore("password")) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ScannerForMSBuild build = TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
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
      }
    }
  }

  @Test
  void selfSignedCertificateInGivenTrustStore() throws Exception {
    try (var server = initSslTestAndServerWithTrustStore("password")) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setProperty("sonar.host.url", server.getUrl())
        .setProjectName("sample")
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProjectVersion("1.0"));

      TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

      BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY, token, Collections.emptyList(), List.of("sonar.scanner.truststorePassword=" + server.getKeystorePassword()));

      assertSslAnalysisSucceed(result, server.getKeystorePath(), server.getKeystorePassword());
    }
  }

  @Test
  void selfSignedCertificateInGivenTrustStore_EndStepPasswordProvidedInEnv() throws Exception {
    try (var server = initSslTestAndServerWithTrustStore("password")) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setProperty("sonar.host.url", server.getUrl())
        .setProjectName("sample")
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProjectVersion("1.0"));

      TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

      var env = List.of(new EnvironmentVariable("SONAR_SCANNER_OPTS", " -Djavax.net.ssl.trustStorePassword=\"password\""));
      BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY, token, env);

      assertThat(result.isSuccess()).isTrue();
    }
  }

  @Test
  void selfSignedCertificateInGivenTrustStore_PasswordNotProvidedInEndStep() throws Exception {
    try (var server = initSslTestAndServerWithTrustStore("password")) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setProperty("sonar.host.url", server.getUrl())
        .setProjectName("sample")
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProjectVersion("1.0"));

      TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);
      try {
        TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY, token, Collections.emptyList());
        fail("Expecting to fail as the 'sonar.scanner.truststorePassword' property is not provided in the end step");
      } catch (BuildFailureException e) {
        assertThat(e.getResult().isSuccess()).isFalse();
        assertThat(e.getResult().getLogs())
          .contains("'sonar.scanner.truststorePassword' must be specified in the end step when specified during the begin step.");
      }
    }
  }

  @Test
  void selfSignedCertificateInGivenTrustStore_PathAndPasswordWithSpace() throws Exception {
    // We don't support spaces in the truststore path and password on Unix
    // Running this test on Linux would always fail
    assumeThat(System.getProperty("os.name")).contains("Windows");

    try (var server = initSslTestAndServerWithTrustStore("change it", Path.of("sub", "folder with spaces"))) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setProperty("sonar.host.url", server.getUrl())
        .setProjectName("sample")
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProjectVersion("1.0"));

      TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

      BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY, token, Collections.emptyList(), List.of("sonar.scanner.truststorePassword=" + server.getKeystorePassword()));

      assertSslAnalysisSucceed(result, server.getKeystorePath(), server.getKeystorePassword());
    }
  }

  @Test
  void unmatchedDomainNameInCertificate() throws Exception {
    try (var server = initSslTestAndServerWithTrustStore("password", Path.of(""), "not-localhost", "keystore.p12")) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ScannerForMSBuild build = TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
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
      }
    }
  }

  @Test
  void truststorePathNotFound() throws IOException {
    var trustStorePath = Paths.get("does", "not", "exist.pfx").toAbsolutePath().toString();
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    ScannerForMSBuild build = TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProperty("sonar.scanner.truststorePath", trustStorePath)
      .setProjectName("sample")
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setProjectVersion("1.0");

    try {
      ORCHESTRATOR.executeBuild(build);
      fail("Expecting to fail during the begin with an SSL error");
    } catch (BuildFailureException e) {
      assertFalse(e.getResult().isSuccess());
      assertThat(e.getResult().getLogs())
        .contains("The specified sonar.scanner.truststorePath file '" + trustStorePath + "' can not be found");
    }
  }

  @Test
  void incorrectPassword() throws IOException {
    var trustStorePath = createKeyStore("changeit", "not-localhost");
    String token = TestUtils.getNewToken(ORCHESTRATOR);
    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    ScannerForMSBuild build = TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProperty("sonar.scanner.truststorePath", trustStorePath)
      .setProperty("sonar.scanner.truststorePassword", "notchangeit")
      .setProjectName("sample")
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setProjectVersion("1.0");

    try {
      ORCHESTRATOR.executeBuild(build);
      fail("Expecting to fail during the begin with an SSL error");
    } catch (BuildFailureException e) {
      assertFalse(e.getResult().isSuccess());
      assertThat(e.getResult().getLogs())
        .contains("Failed to import the sonar.scanner.truststorePath file " + trustStorePath + ": The specified network password is not correct.")
        .contains("System.Security.Cryptography.CryptographicException: The specified network password is not correct.");
    }
  }

  @Test
  void defaultTruststoreExist() throws Exception {
    var sonarHome = basePath.resolve("sonar").toAbsolutePath().toString();
    try (var server = initSslTestAndServerWithTrustStore("changeit", Path.of("sonar", "ssl"), "truststore.p12")) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProperty("sonar.host.url", server.getUrl())
        .setProperty("sonar.userHome", sonarHome)
        .setProjectName("sample")
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProjectVersion("1.0"));

      TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

      BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY, token);

      assertSslAnalysisSucceed(result, server.getKeystorePath(), server.getKeystorePassword());
    }
  }

  @Test
  void defaultTruststoreExist_IncorrectPassword() throws Exception {
    var sonarHome = basePath.resolve("sonar").toAbsolutePath().toString();
    try (var server = initSslTestAndServerWithTrustStore("itchange", Path.of("sonar", "ssl"), "truststore.p12")) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ScannerForMSBuild build = TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProjectName("sample")
        .setProperty("sonar.userHome", sonarHome)
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProjectVersion("1.0");

      try {
        ORCHESTRATOR.executeBuild(build);
        fail("Expecting to fail during the begin with an SSL error");
      } catch (BuildFailureException e) {
        assertFalse(e.getResult().isSuccess());
        assertThat(e.getResult().getLogs())
          .contains("Failed to import the sonar.scanner.truststorePath file " + server.getKeystorePath() + ": The specified network password is not correct.")
          .contains("System.Security.Cryptography.CryptographicException: The specified network password is not correct.");
      }
    }
  }

  @Test
  void defaultTruststoreExist_ProvidedPassword() throws Exception {
    var sonarHome = basePath.resolve("sonar").toAbsolutePath().toString();
    try (var server = initSslTestAndServerWithTrustStore("itchange", Path.of("sonar", "ssl"), "truststore.p12")) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setEnvironmentVariable("SONAR_USER_HOME", sonarHome)
        .setProjectKey(PROJECT_KEY)
        .setProperty("sonar.host.url", server.getUrl())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setDebugLogs(true)
        .setProjectName("sample")
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProjectVersion("1.0"));

      TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

      BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY, token, Collections.emptyList(), List.of("sonar.scanner.truststorePassword=" + server.getKeystorePassword()));

      assertSslAnalysisSucceed(result, server.getKeystorePath(), server.getKeystorePassword());
    }
  }

  @Test
  void defaultTruststoreExist_ProvidedPassword_UserHomeProperty() throws Exception {
    var sonarHome = basePath.resolve("sonar").toAbsolutePath().toString();
    try (var server = initSslTestAndServerWithTrustStore("itchange", Path.of("sonar", "ssl"), "truststore.p12")) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProperty("sonar.host.url", server.getUrl())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setProperty("sonar.userHome", sonarHome)
        .setDebugLogs(true)
        .setProjectName("sample")
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProjectVersion("1.0"));

      TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

      BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY, token, Collections.emptyList(), List.of("sonar.scanner.truststorePassword=" + server.getKeystorePassword()));

      assertSslAnalysisSucceed(result, server.getKeystorePath(), server.getKeystorePassword());
    }
  }

  @Test
  void truststorePasswordNotProvided_UseDefaultPassword() throws Exception {
    try (var server = initSslTestAndServerWithTrustStore("changeit")) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
      ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.host.url", server.getUrl())
        .setProjectName("sample")
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProjectVersion("1.0"));

      TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

      BuildResult result = TestUtils.executeEndStepAndDumpResults(ORCHESTRATOR, projectDir, PROJECT_KEY, token);

      assertSslAnalysisSucceed(result, server.getKeystorePath(), server.getKeystorePassword());
    }

  }

  @Test
  void truststorePasswordNotProvided_UseDefaultPassword_Fail() throws Exception {
    try (var server = initSslTestAndServerWithTrustStore("itchange")) {
      String token = TestUtils.getNewToken(ORCHESTRATOR);
      Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");

      ScannerForMSBuild build = TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
        .addArgument("begin")
        .setProjectKey(PROJECT_KEY)
        .setProjectName("sample")
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.host.url", server.getUrl())
        .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
        .setProjectVersion("1.0");

      try {
        ORCHESTRATOR.executeBuild(build);
        fail("Expecting to fail during the begin with an SSL error");
      } catch (BuildFailureException e) {
        assertFalse(e.getResult().isSuccess());
        assertThat(e.getResult().getLogs())
          .contains("Failed to import the sonar.scanner.truststorePath file " + server.getKeystorePath() + ": The specified network password is not correct.")
          .contains("System.Security.Cryptography.CryptographicException: The specified network password is not correct.");
      }
    }
  }

  private HttpsReverseProxy initSslTestAndServerWithTrustStore(String trustStorePassword) throws Exception {
    return initSslTestAndServerWithTrustStore(trustStorePassword, Path.of(""));
  }

  private HttpsReverseProxy initSslTestAndServerWithTrustStore(String trustStorePassword, Path subFolder) throws Exception {
    return initSslTestAndServerWithTrustStore(trustStorePassword, subFolder, "keystore.p12");
  }

  private HttpsReverseProxy initSslTestAndServerWithTrustStore(String trustStorePassword, Path subFolder, String keystoreName) throws Exception {
    return initSslTestAndServerWithTrustStore(trustStorePassword, subFolder, "localhost", keystoreName);
  }

  private HttpsReverseProxy initSslTestAndServerWithTrustStore(String trustStorePassword, Path subFolder, String host, String keystoreName) throws Exception {
    var trustStorePath = createKeyStore(trustStorePassword, subFolder, host, keystoreName);
    return initSslTestAndServer(trustStorePath, trustStorePassword);
  }

  private HttpsReverseProxy initSslTestAndServer(String trustStorePath, String trustStorePassword) throws Exception {
    var server = new HttpsReverseProxy(ORCHESTRATOR.getServer().getUrl(), trustStorePath, trustStorePassword);
    server.start();
    return server;
  }

  private void assertSslAnalysisSucceed(BuildResult result, String trustStorePath, String trustStorePassword) {
    assertTrue(result.isSuccess());
    assertThat(result.getLogs())
      .contains("SONAR_SCANNER_OPTS")
      .contains("-Djavax.net.ssl.trustStore=\"" + trustStorePath.replace('\\', '/') + "\"")
      .contains("-Djavax.net.ssl.trustStorePassword=\"" + trustStorePassword + "\"");
  }

  private String createKeyStore(String password, String host) {
    return createKeyStore(password, Path.of(""), host, "keystore.pfx");
  }

  private String createKeyStore(String password, Path subFolder, String host, String keystoreName) {
    var keystoreLocation = basePath.resolve(subFolder.resolve(keystoreName)).toAbsolutePath();
    LOG.info("Creating keystore at {}", keystoreLocation);
    return SslUtils.generateKeyStore(keystoreLocation, host, password);
  }
}
