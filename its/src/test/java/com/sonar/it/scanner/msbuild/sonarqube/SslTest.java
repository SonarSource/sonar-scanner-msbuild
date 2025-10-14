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
import com.sonar.it.scanner.msbuild.utils.AnalysisResult;
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.HttpsReverseProxy;
import com.sonar.it.scanner.msbuild.utils.OSPlatform;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.SslExceptionMessages;
import com.sonar.it.scanner.msbuild.utils.SslUtils;
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Objects;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledOnOs;
import org.junit.jupiter.api.condition.OS;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.serverSupportsProvisioning;
import static com.sonar.it.scanner.msbuild.utils.SonarAssertions.assertThat;
import static org.junit.jupiter.api.Assertions.assertFalse;

@ExtendWith({ServerTests.class, ContextExtension.class})
class SslTest {
  private static final Logger LOG = LoggerFactory.getLogger(SslTest.class);
  private static final String SSL_KEYSTORE_PASSWORD_ENV = "SSL_KEYSTORE_PASSWORD";
  private static final String SSL_KEYSTORE_PATH_ENV = "SSL_KEYSTORE_PATH";

  private static String keystorePath;
  private static String keystorePassword;

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
   * <p>
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

  /**
   * Test SSL connection to SonarQube server while the certificate is trusted in the system store.
   * <p>This relies on the environment variables SSL_KEYSTORE_PATH and SSL_KEYSTORE_PASSWORD to be set.
   * <p>See the init method for more details.
   */
  @Test
  void trustedSelfSignedCertificate() throws IOException {
    try (var server = initSslTestAndServer(keystorePath, keystorePassword)) {
      var context = AnalysisContext.forServer("ProjectUnderTest").setEnvironmentVariable("SONAR_SCANNER_OPTS",
        "-Djavax.net.ssl.trustStore=" + keystorePath.replace('\\', '/') + " -Djavax.net.ssl.trustStorePassword=" + keystorePassword);
      context.begin.setProperty("sonar.host.url", server.getUrl());
      context.begin.setDebugLogs();
      var logs = context.runAnalysis().end().getLogs();

      // '-Djavax.net.ssl.trustStorePassword' & '-Djavax.net.ssl.trustStore' are part of the same argument.
      // They do not appear in logs as the argument contains sensitive data.
      assertThat(logs)
        .doesNotContain("-Djavax.net.ssl.trustStorePassword=\"" + keystorePassword + "\"")
        .doesNotContain(keystorePassword);
      assertThat(TestUtils.scannerEngineInputJson(context)).hasAllSecretsRedacted();
    }
  }

  @Test
  // The javax.net.ssl.trustStoreType=Windows-ROOT is not valid on Unix
  @EnabledOnOs(OS.WINDOWS)
  void trustedSelfSignedCertificate_WindowsRoot() throws IOException {
    try (var server = initSslTestAndServer(keystorePath, keystorePassword)) {
      var context = AnalysisContext.forServer("ProjectUnderTest", ScannerClassifier.NET_FRAMEWORK);
      context.begin
        .setProperty("sonar.host.url", server.getUrl())
        .setDebugLogs();

      var logs = context.runAnalysis().end().getLogs();
      if (serverSupportsProvisioning()) {
        assertThat(logs)
          .contains("Args: -Djavax.net.ssl.trustStoreType=Windows-ROOT");
        assertThat(TestUtils.scannerEngineInputJson(context)).hasAllSecretsRedacted();
      } else {
        assertThat(logs)
          .contains("SONAR_SCANNER_OPTS")
          .contains("-Djavax.net.ssl.trustStoreType=Windows-ROOT");
      }
    }
  }

  @Test
  void trustedSelfSignedCertificate_ExistingValueInScannerOpts() throws IOException {
    try (var server = initSslTestAndServer(keystorePath, keystorePassword)) {
      var context = AnalysisContext.forServer("ProjectUnderTest", ScannerClassifier.NET).setEnvironmentVariable("SONAR_SCANNER_OPTS", "-Xmx2048m");
      context.begin
        .setProperty("sonar.host.url", server.getUrl())
        .setDebugLogs();
      var logs = context.runAnalysis().end().getLogs();
      if (serverSupportsProvisioning()) {
        assertThat(logs).contains("Args: -Xmx2048m");
        assertThat(TestUtils.scannerEngineInputJson(context)).hasAllSecretsRedacted();
      } else {
        assertThat(logs).contains("SONAR_SCANNER_OPTS=-Xmx2048m");
      }
    }
  }

  @Test
  void untrustedSelfSignedCertificate() {
    try (var server = initSslTestAndServerWithTrustStore("p@ssw0rd42")) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin.setProperty("sonar.host.url", server.getUrl());
      var result = context.begin.execute(ORCHESTRATOR);

      assertFalse(result.isSuccess());
      assertThat(result.getLogs())
        .contains(SslExceptionMessages.sslConnectionFailed())
        .contains(SslExceptionMessages.untrustedRoot());
    }
  }

  @Test
  void selfSignedCertificateInGivenTrustStore() throws IOException {
    try (var server = initSslTestAndServerWithTrustStore("p@ssw0rd42")) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setProperty("sonar.host.url", server.getUrl())
        .setDebugLogs();
      context.end
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword());
      validateAnalysis(context, server);
    }
  }

  @Test
  void selfSignedCertificateInGivenTrustStore_EndStepPasswordProvidedInEnv() throws IOException {
    try (var server = initSslTestAndServerWithTrustStore("p@ssw0rd42")) {
      var passwordEnvValue = server.getKeystorePassword();
      if (OSPlatform.isWindows()) {
        passwordEnvValue = "\"" + passwordEnvValue + "\"";
      }
      var context = AnalysisContext.forServer("ProjectUnderTest")
        .setEnvironmentVariable("SONAR_SCANNER_OPTS", " -Djavax.net.ssl.trustStorePassword=" + passwordEnvValue);
      context.begin
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setProperty("sonar.host.url", server.getUrl())
        .setDebugLogs();
      var logs = context.runAnalysis().end().getLogs();

      if (serverSupportsProvisioning()) {
        assertThat(logs)
          .containsPattern("Args: -Djavax.net.ssl.trustStore=\"?" + server.getKeystorePath().replace('\\', '/'))
          .doesNotContain(server.getKeystorePassword());
        assertThat(TestUtils.scannerEngineInputJson(context)).hasAllSecretsRedacted();
      } else {
        assertThat(logs)
          .contains("SONAR_SCANNER_OPTS=-D<sensitive data removed>")
          .doesNotContain(server.getKeystorePassword());
      }
    }
  }

  @Test
  void selfSignedCertificateInGivenTrustStore_PasswordNotProvidedInEndStep() {
    try (var server = initSslTestAndServerWithTrustStore("p@ssw0rd42")) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setProperty("sonar.host.url", server.getUrl());

      var logs = context.runFailedAnalysis().end().getLogs();
      assertThat(logs)
        .contains("'sonar.scanner.truststorePassword' must be specified in the end step when specified during the begin step.");
    }
  }

  @Test
  // We don't support spaces in the truststore path and password on Unix
  // Running this test on Unix would always fail
  @EnabledOnOs(OS.WINDOWS)
  void selfSignedCertificateInGivenTrustStore_PathAndPasswordWithSpace() throws IOException {
    try (var server = initSslTestAndServerWithTrustStore("p@ssw0rd w1th sp@ce", Path.of("sub", "folder with spaces"))) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setProperty("sonar.host.url", server.getUrl())
        .setDebugLogs();
      context.end
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword());
      validateAnalysis(context, server);
    }
  }

  @Test
  void unmatchedDomainNameInCertificate() {
    try (var server = initSslTestAndServerWithTrustStore("p@ssw0rd42", Path.of(""), "not-localhost", "keystore.p12")) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setProperty("sonar.host.url", server.getUrl());
      var result = context.begin.execute(ORCHESTRATOR);

      assertFalse(result.isSuccess());
      assertThat(result.getLogs())
        .contains(SslExceptionMessages.sslConnectionFailed())
        .contains(SslExceptionMessages.certificateRejected());
    }
  }

  @Test
  void truststorePathNotFound() {
    var trustStorePath = Paths.get("does", "not", "exist.pfx").toAbsolutePath().toString();
    var context = AnalysisContext.forServer("ProjectUnderTest");
    context.begin.setProperty("sonar.scanner.truststorePath", trustStorePath);
    var result = context.begin.execute(ORCHESTRATOR);

    assertFalse(result.isSuccess());
    assertThat(result.getLogs())
      .contains("The specified sonar.scanner.truststorePath file '" + trustStorePath + "' can not be found");
  }

  @Test
  void incorrectPassword() {
    var trustStorePath = createKeyStore("changeit", "not-localhost");
    var context = AnalysisContext.forServer("ProjectUnderTest");
    var result = context.begin
      .setProperty("sonar.scanner.truststorePath", trustStorePath)
      .setProperty("sonar.scanner.truststorePassword", "notchangeit")
      .execute(ORCHESTRATOR);

    assertFalse(result.isSuccess());
    assertThat(result.getLogs())
      .contains(SslExceptionMessages.importFail(trustStorePath))
      .contains(SslExceptionMessages.incorrectPassword());
  }

  @ParameterizedTest
  @ValueSource(strings = {"changeit", "sonar"})
  void defaultTruststoreExist(String defaultPassword) throws IOException {
    var sonarHome = ContextExtension.currentTempDir().resolve("sonar").toAbsolutePath().toString();
    try (var server = initSslTestAndServerWithTrustStore(defaultPassword, Path.of("sonar", "ssl"), "truststore.p12")) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin
        .setProperty("sonar.host.url", server.getUrl())
        .setDebugLogs()
        .setProperty("sonar.userHome", sonarHome);

      var result = validateAnalysis(context, server);
      if (defaultPassword.equals("sonar")) {
        assertThat(result.begin().getLogs()).containsPattern("Could not import the truststore '.*truststore.p12' with the default password at index 0. Reason: .*");
        if (serverSupportsProvisioning()) {
          assertThat(result.end().getLogs()).containsPattern("WARNING: WARN: Using deprecated default password for truststore '\"?.*truststore.p12\"?'");
        } else {
          assertThat(result.end().getLogs()).containsPattern("Could not import the truststore '\"?.*truststore.p12\"?' with the default password at index 0. Reason: .*");

        }
      }
    }
  }

  @Test
  void defaultTruststoreExist_IncorrectPassword() throws IOException {
    var sonarHome = ContextExtension.currentTempDir().resolve("sonar").toAbsolutePath().toString();
    try (var server = initSslTestAndServerWithTrustStore("itchange", Path.of("sonar", "ssl"), "truststore.p12")) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin.setProperty("sonar.userHome", sonarHome);
      var result = context.begin.execute(ORCHESTRATOR);

      assertFalse(result.isSuccess());
      assertThat(result.getLogs())
        .contains(SslExceptionMessages.importFail(server.getKeystorePath()))
        .contains(SslExceptionMessages.incorrectPassword());
    }
  }

  @Test
  void defaultTruststoreExist_ProvidedPassword() throws IOException {
    var sonarHome = ContextExtension.currentTempDir().resolve("sonar").toAbsolutePath().toString();
    try (var server = initSslTestAndServerWithTrustStore("p@ssw0rd42", Path.of("sonar", "ssl"), "truststore.p12")) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin
        .setEnvironmentVariable("SONAR_USER_HOME", sonarHome)
        .setProperty("sonar.host.url", server.getUrl())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setDebugLogs();
      context.end
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword());
      validateAnalysis(context, server);
    }
  }

  @Test
  void defaultTruststoreExist_ProvidedPassword_UserHomeProperty() throws IOException {
    var sonarHome = ContextExtension.currentTempDir().resolve("sonar").toAbsolutePath().toString();
    try (var server = initSslTestAndServerWithTrustStore("p@ssw0rd42", Path.of("sonar", "ssl"), "truststore.p12")) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin
        .setProperty("sonar.host.url", server.getUrl())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setProperty("sonar.userHome", sonarHome)
        .setDebugLogs();
      context.end
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword());
      validateAnalysis(context, server);
    }
  }

  @ParameterizedTest
  @ValueSource(strings = {"changeit", "sonar"})
  void truststorePasswordNotProvided_UseDefaultPassword(String defaultPassword) throws IOException {
    try (var server = initSslTestAndServerWithTrustStore(defaultPassword)) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setDebugLogs()
        .setProperty("sonar.host.url", server.getUrl());

      var result = validateAnalysis(context, server);
      if (defaultPassword.equals("sonar")) {
        assertThat(result.begin().getLogs()).containsPattern("Could not import the truststore '.*keystore.p12' with the default password at index 0. Reason: .*");
        assertThat(result.end().getLogs()).containsPattern("Could not import the truststore '\"?.*keystore.p12\"?' with the default password at index 0. Reason: .*");
      }
    }
  }

  @Test
  void truststorePasswordNotProvided_UseDefaultPassword_Fail() {
    try (var server = initSslTestAndServerWithTrustStore("itchange")) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.host.url", server.getUrl());
      var result = context.begin.execute(ORCHESTRATOR);

      assertFalse(result.isSuccess());
      assertThat(result.getLogs())
        .contains(SslExceptionMessages.importFail(server.getKeystorePath()))
        .contains(SslExceptionMessages.incorrectPassword());
    }
  }

  private HttpsReverseProxy initSslTestAndServerWithTrustStore(String trustStorePassword) {
    return initSslTestAndServerWithTrustStore(trustStorePassword, Path.of(""));
  }

  private HttpsReverseProxy initSslTestAndServerWithTrustStore(String trustStorePassword, Path subFolder) {
    return initSslTestAndServerWithTrustStore(trustStorePassword, subFolder, "keystore.p12");
  }

  private HttpsReverseProxy initSslTestAndServerWithTrustStore(String trustStorePassword, Path subFolder, String keystoreName) {
    return initSslTestAndServerWithTrustStore(trustStorePassword, subFolder, "localhost", keystoreName);
  }

  private HttpsReverseProxy initSslTestAndServerWithTrustStore(String trustStorePassword, Path subFolder, String host, String keystoreName) {
    var trustStorePath = createKeyStore(trustStorePassword, subFolder, host, keystoreName);
    return initSslTestAndServer(trustStorePath, trustStorePassword);
  }

  private HttpsReverseProxy initSslTestAndServer(String trustStorePath, String trustStorePassword) {
    var server = new HttpsReverseProxy(ORCHESTRATOR.getServer().getUrl(), trustStorePath, trustStorePassword);
    try {
      server.start();
    } catch (Exception ex) {
      throw new RuntimeException(ex.getMessage(), ex);
    }
    return server;
  }

  private AnalysisResult validateAnalysis(AnalysisContext context, HttpsReverseProxy server) throws IOException {
    var result = context.runAnalysis();
    var logs = result.end().getLogs();
    var trustStorePath = server.getKeystorePath().replace('\\', '/');
    var trustStorePassword = server.getKeystorePassword();
    if (OSPlatform.isWindows()) {
      trustStorePath = "\"" + trustStorePath + "\"";
      trustStorePassword = "\"" + trustStorePassword + "\"";
    }
    if (serverSupportsProvisioning()) {
      assertThat(logs)
        .contains("Args: ")
        .contains("-Djavax.net.ssl.trustStore=" + trustStorePath)
        .doesNotContain("-Djavax.net.ssl.trustStorePassword=" + trustStorePassword);
    } else {
      assertThat(logs)
        .contains("SONAR_SCANNER_OPTS")
        .contains("-Djavax.net.ssl.trustStore=" + trustStorePath)
        .contains("-D<sensitive data removed>")
        .doesNotContain("-Djavax.net.ssl.trustStorePassword=" + trustStorePassword);
    }

    // When using the 'sonar' default password, check if it is not logged will always fail
    // because we have a lot of 'sonar' occurrences in the logs (e.g.: .sonarqube)
    if (!Objects.equals(server.getKeystorePassword(), "sonar")) {
      assertThat(logs).doesNotContain(server.getKeystorePassword());
      assertThat(TestUtils.scannerEngineInputJson(context)).hasAllSecretsRedacted();
    }

    return result;
  }

  private String createKeyStore(String password, String host) {
    return createKeyStore(password, Path.of(""), host, "keystore.pfx");
  }

  private String createKeyStore(String password, Path subFolder, String host, String keystoreName) {
    var keystoreLocation = ContextExtension.currentTempDir().resolve(subFolder.resolve(keystoreName)).toAbsolutePath();
    LOG.info("Creating keystore at {}", keystoreLocation);
    return SslUtils.generateKeyStore(keystoreLocation, host, password);
  }
}
