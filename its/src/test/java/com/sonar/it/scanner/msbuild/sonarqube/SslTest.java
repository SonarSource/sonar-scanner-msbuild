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
import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.util.NetworkUtils;
import java.net.InetAddress;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.List;
import org.eclipse.jetty.http.HttpVersion;
import org.eclipse.jetty.proxy.ProxyServlet;
import org.eclipse.jetty.server.Handler;
import org.eclipse.jetty.server.HttpConfiguration;
import org.eclipse.jetty.server.HttpConnectionFactory;
import org.eclipse.jetty.server.Server;
import org.eclipse.jetty.server.ServerConnector;
import org.eclipse.jetty.server.SslConnectionFactory;
import org.eclipse.jetty.server.handler.DefaultHandler;
import org.eclipse.jetty.server.handler.HandlerCollection;
import org.eclipse.jetty.servlet.ServletContextHandler;
import org.eclipse.jetty.servlet.ServletHandler;
import org.eclipse.jetty.servlet.ServletHolder;
import org.eclipse.jetty.util.ssl.SslContextFactory;
import org.eclipse.jetty.util.thread.QueuedThreadPool;
import org.junit.jupiter.api.AfterEach;
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
import static org.junit.jupiter.api.Assertions.assertTrue;

@ExtendWith(Tests.class)
public class SslTest {
  private static final Logger LOG = LoggerFactory.getLogger(SslTest.class);
  private static final String PROJECT_KEY = "ssl-certificate";
  private static final String SSL_KEYSTORE_PASSWORD_ENV = "SSL_KEYSTORE_PASSWORD";
  private static final String SSL_KEYSTORE_PATH_ENV = "SSL_KEYSTORE_PATH";

  private static Server server;
  private static String keystorePath;
  private static String keystorePassword;

  @TempDir
  public Path basePath;

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

  @AfterEach
  public void stopProxy() throws Exception {
    if (server != null && server.isStarted()) {
      server.stop();
      server.join();
    }
  }

  /**
   * Test SSL connection to SonarQube server while the certificate is trusted in the system store.
   * <p>
   * To run this test locally, it is required to set the following environment variables:
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
   */
  @Test
  void selfSignedCertificateInSystemTrustStore() throws Exception {
    ORCHESTRATOR.getServer().restoreProfile(FileLocation.of("projects/ProjectUnderTest/TestQualityProfile.xml"));
    ORCHESTRATOR.getServer().provisionProject(PROJECT_KEY, "sample");
    ORCHESTRATOR.getServer().associateProjectToQualityProfile(PROJECT_KEY, "cs", "ProfileForTest");
    int httpsPort = startSSLTransparentReverseProxy();

    String token = TestUtils.getNewToken(ORCHESTRATOR);

    Path projectDir = TestUtils.projectDir(basePath, "ProjectUnderTest");
    ORCHESTRATOR.executeBuild(TestUtils.newScanner(ORCHESTRATOR, projectDir, token)
      .addArgument("begin")
      .setProjectKey(PROJECT_KEY)
      .setProperty("sonar.host.url", "https://localhost:" + httpsPort)
      .setProjectName("sample")
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      .setProjectVersion("1.0"));

    TestUtils.buildMSBuild(ORCHESTRATOR, projectDir);

    BuildResult result = TestUtils.executeEndStepAndDumpResults(
      ORCHESTRATOR,
      projectDir,
      PROJECT_KEY, token,
      List.of(
        new EnvironmentVariable("SONAR_SCANNER_OPTS", "-Djavax.net.ssl.trustStore=" + keystorePath.replace('\\', '/') + " -Djavax.net.ssl.trustStorePassword=" + keystorePassword)
      )
    );
    assertTrue(result.isSuccess());
    List<Issues.Issue> issues = TestUtils.allIssues(ORCHESTRATOR);
    assertThat(issues).hasSize(1);
  }

  // https://github.com/SonarSource/sonar-scanner-java-library/blob/6f65b90dad474521e0711f80b637a1ebe6c7c493/its/it-tests/src/test/java/com/sonar/scanner/lib/it/SSLTest.java#L99-L159
  private static int startSSLTransparentReverseProxy() throws Exception {
    int httpPort = NetworkUtils.getNextAvailablePort(InetAddress.getLocalHost());
    int httpsPort = NetworkUtils.getNextAvailablePort(InetAddress.getLocalHost());

    // Setup Threadpool
    QueuedThreadPool threadPool = new QueuedThreadPool();
    threadPool.setMaxThreads(500);

    server = new Server(threadPool);

    // HTTP Configuration
    HttpConfiguration httpConfig = new HttpConfiguration();
    httpConfig.setSecureScheme("https");
    httpConfig.setSecurePort(httpsPort);
    httpConfig.setSendServerVersion(true);
    httpConfig.setSendDateHeader(false);

    // Handler Structure
    HandlerCollection handlers = new HandlerCollection();
    handlers.setHandlers(new Handler[]{proxyHandler(), new DefaultHandler()});
    server.setHandler(handlers);

    ServerConnector http = new ServerConnector(server, new HttpConnectionFactory(httpConfig));
    http.setPort(httpPort);
    server.addConnector(http);

    Path serverKeyStore = Paths.get(keystorePath).toAbsolutePath();
    assertThat(serverKeyStore).exists();

    // SSL Context Factory
    SslContextFactory.Server sslContextFactory = new SslContextFactory.Server();
    sslContextFactory.setKeyStorePath(serverKeyStore.toString());
    sslContextFactory.setKeyStorePassword(keystorePassword);
    sslContextFactory.setKeyManagerPassword(keystorePassword);
    sslContextFactory.setNeedClientAuth(false);
    sslContextFactory.setExcludeCipherSuites("SSL_RSA_WITH_DES_CBC_SHA",
      "SSL_DHE_RSA_WITH_DES_CBC_SHA",
      "SSL_DHE_DSS_WITH_DES_CBC_SHA",
      "SSL_RSA_EXPORT_WITH_RC4_40_MD5",
      "SSL_RSA_EXPORT_WITH_DES40_CBC_SHA",
      "SSL_DHE_RSA_EXPORT_WITH_DES40_CBC_SHA",
      "SSL_DHE_DSS_EXPORT_WITH_DES40_CBC_SHA");

    // SSL HTTP Configuration
    HttpConfiguration httpsConfig = new HttpConfiguration(httpConfig);

    // SSL Connector
    ServerConnector sslConnector = new ServerConnector(server,
      new SslConnectionFactory(sslContextFactory, HttpVersion.HTTP_1_1.asString()),
      new HttpConnectionFactory(httpsConfig));
    sslConnector.setPort(httpsPort);
    server.addConnector(sslConnector);

    server.start();
    return httpsPort;
  }

  private static ServletContextHandler proxyHandler() {
    ServletContextHandler contextHandler = new ServletContextHandler();
    contextHandler.setServletHandler(newServletHandler());
    return contextHandler;
  }

  private static ServletHandler newServletHandler() {
    ServletHandler handler = new ServletHandler();
    ServletHolder holder = handler.addServletWithMapping(ProxyServlet.Transparent.class, "/*");
    holder.setInitParameter("proxyTo", ORCHESTRATOR.getServer().getUrl());
    return handler;
  }
}
