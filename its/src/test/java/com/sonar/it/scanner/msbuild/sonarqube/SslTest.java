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
import com.sonar.it.scanner.msbuild.utils.ContextExtension;
import com.sonar.it.scanner.msbuild.utils.HttpsReverseProxy;
import com.sonar.it.scanner.msbuild.utils.OSPlatform;
import com.sonar.it.scanner.msbuild.utils.ScannerClassifier;
import com.sonar.it.scanner.msbuild.utils.SslExceptionMessages;
import com.sonar.it.scanner.msbuild.utils.SslUtils;
import java.nio.file.Path;
import java.nio.file.Paths;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static com.sonar.it.scanner.msbuild.sonarqube.ServerTests.ORCHESTRATOR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assumptions.assumeTrue;

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

  /**
   * Test SSL connection to SonarQube server while the certificate is trusted in the system store.
   * <p>This relies on the environment variables SSL_KEYSTORE_PATH and SSL_KEYSTORE_PASSWORD to be set.
   * <p>See the init method for more details.
   */
  @Test
  void trustedSelfSignedCertificate() {
    try (var server = initSslTestAndServer(keystorePath, keystorePassword)) {
      var context = AnalysisContext.forServer("ProjectUnderTest").setEnvironmentVariable("SONAR_SCANNER_OPTS",
        "-Djavax.net.ssl.trustStore=" + keystorePath.replace('\\', '/') + " -Djavax.net.ssl.trustStorePassword=" + keystorePassword);
      context.begin.setProperty("sonar.host.url", server.getUrl());
      var logs = context.runAnalysis().end().getLogs();

      assertThat(logs)
        .doesNotContain("-Djavax.net.ssl.trustStorePassword=\"" + keystorePassword + "\"")
        .doesNotContain(keystorePassword);
    }
  }

  @Test
  void trustedSelfSignedCertificate_WindowsRoot() {
    // The javax.net.ssl.trustStoreType=Windows-ROOT is not valid on Unix
    assumeTrue(OSPlatform.isWindows());

    try (var server = initSslTestAndServer(keystorePath, keystorePassword)) {
      var context = AnalysisContext.forServer("ProjectUnderTest", ScannerClassifier.NET_FRAMEWORK);
      context.begin.setProperty("sonar.host.url", server.getUrl());
      var logs = context.runAnalysis().end().getLogs();

      assertThat(logs)
        .contains("SONAR_SCANNER_OPTS")
        .contains("-Djavax.net.ssl.trustStoreType=Windows-ROOT");
    }
  }

  @Test
  void trustedSelfSignedCertificate_ExistingValueInScannerOpts() {
    try (var server = initSslTestAndServer(keystorePath, keystorePassword)) {
      var context = AnalysisContext.forServer("ProjectUnderTest", ScannerClassifier.NET).setEnvironmentVariable("SONAR_SCANNER_OPTS", "-Xmx2048m");
      context.begin.setProperty("sonar.host.url", server.getUrl());
      var logs = context.runAnalysis().end().getLogs();

      assertThat(logs).contains("SONAR_SCANNER_OPTS=-Xmx2048m");
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
  void selfSignedCertificateInGivenTrustStore() {
    try (var server = initSslTestAndServerWithTrustStore("p@ssw0rd42")) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setProperty("sonar.host.url", server.getUrl());
      context.end
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword());
      validateAnalysis(context, server);
    }
  }

  @Test
  void selfSignedCertificateInGivenTrustStore_EndStepPasswordProvidedInEnv() {
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
        .setProperty("sonar.host.url", server.getUrl());
      var logs = context.runAnalysis().end().getLogs();

      assertThat(logs)
        .contains("SONAR_SCANNER_OPTS=-D<sensitive data removed>")
        .doesNotContain(server.getKeystorePassword());
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
  void selfSignedCertificateInGivenTrustStore_PathAndPasswordWithSpace() {
    // We don't support spaces in the truststore path and password on Unix
    // Running this test on Linux would always fail
    assumeTrue(OSPlatform.isWindows());

    try (var server = initSslTestAndServerWithTrustStore("p@ssw0rd w1th sp@ce", Path.of("sub", "folder with spaces"))) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.scanner.truststorePassword", server.getKeystorePassword())
        .setProperty("sonar.host.url", server.getUrl());
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

  @Test
  void defaultTruststoreExist() {
    var sonarHome = ContextExtension.currentTempDir().resolve("sonar").toAbsolutePath().toString();
    try (var server = initSslTestAndServerWithTrustStore("changeit", Path.of("sonar", "ssl"), "truststore.p12")) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin
        .setProperty("sonar.host.url", server.getUrl())
        .setProperty("sonar.userHome", sonarHome);
      validateAnalysis(context, server);
    }
  }

  @Test
  void defaultTruststoreExist_IncorrectPassword() {
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
  void defaultTruststoreExist_ProvidedPassword() {
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
  void defaultTruststoreExist_ProvidedPassword_UserHomeProperty() {
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

  @Test
  void truststorePasswordNotProvided_UseDefaultPassword() {
    try (var server = initSslTestAndServerWithTrustStore("changeit")) {
      var context = AnalysisContext.forServer("ProjectUnderTest");
      context.begin
        .setProperty("sonar.scanner.truststorePath", server.getKeystorePath())
        .setProperty("sonar.host.url", server.getUrl());
      validateAnalysis(context, server);
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

  private void validateAnalysis(AnalysisContext context, HttpsReverseProxy server) {
    var logs = context.runAnalysis().end().getLogs();

    var trustStorePath = server.getKeystorePath().replace('\\', '/');
    var trustStorePassword = server.getKeystorePassword();
    if (OSPlatform.isWindows()) {
      trustStorePath = "\"" + trustStorePath + "\"";
      trustStorePassword = "\"" + trustStorePassword + "\"";
    }

    assertThat(logs)
      .contains("SONAR_SCANNER_OPTS")
      .contains("-Djavax.net.ssl.trustStore=" + trustStorePath)
      .contains("-D<sensitive data removed>")
      .doesNotContain("-Djavax.net.ssl.trustStorePassword=" + trustStorePassword)
      .doesNotContain(server.getKeystorePassword());
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
