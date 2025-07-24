/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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
package com.sonar.it.scanner.msbuild.utils;

import com.sonar.orchestrator.util.NetworkUtils;
import java.net.InetAddress;
import java.nio.file.Path;
import java.nio.file.Paths;
import org.eclipse.jetty.ee10.servlet.ServletContextHandler;
import org.eclipse.jetty.ee10.servlet.ServletHandler;
import org.eclipse.jetty.ee10.servlet.ServletHolder;
import org.eclipse.jetty.http.HttpVersion;
import org.eclipse.jetty.ee10.proxy.ProxyServlet;
import org.eclipse.jetty.server.Handler;
import org.eclipse.jetty.server.HttpConfiguration;
import org.eclipse.jetty.server.HttpConnectionFactory;
import org.eclipse.jetty.server.Server;
import org.eclipse.jetty.server.ServerConnector;
import org.eclipse.jetty.server.SslConnectionFactory;
import org.eclipse.jetty.server.handler.DefaultHandler;
import org.eclipse.jetty.util.ssl.SslContextFactory;
import org.eclipse.jetty.util.thread.QueuedThreadPool;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static com.sonar.it.scanner.msbuild.utils.SonarAssertions.assertThat;

public class HttpsReverseProxy implements AutoCloseable {
  static final Logger LOG = LoggerFactory.getLogger(HttpsReverseProxy.class);

  private final String proxyTo;
  private final String keystorePath;
  private final String keystorePassword;
  private Server server;
  private int httpsPort;

  public HttpsReverseProxy(String proxyTo, String keystorePath, String keystorePassword) {
    this.proxyTo = proxyTo;
    this.keystorePath = keystorePath;
    this.keystorePassword = keystorePassword;
  }

  // https://github.com/SonarSource/sonar-scanner-java-library/blob/6f65b90dad474521e0711f80b637a1ebe6c7c493/its/it-tests/src/test/java/com/sonar/scanner/lib/it/SSLTest.java#L99-L159
  public void start() throws Exception {
    int httpPort = NetworkUtils.getNextAvailablePort(InetAddress.getLocalHost());
    httpsPort = NetworkUtils.getNextAvailablePort(InetAddress.getLocalHost());
    LOG.info("Starting HTTPS reverse proxy on port {}", httpsPort);

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
    var handlers = new Handler.Sequence();
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
    LOG.info("HTTPS reverse proxy started on {}", getUrl());
  }

  public String getKeystorePath() {
    return this.keystorePath;
  }

  public String getKeystorePassword() {
    return this.keystorePassword;
  }

  @Override
  public void close() {
    try {
      stop();
    } catch (Exception e) {
      throw new IllegalStateException("Failed to stop HTTPS reverse proxy", e);
    }
  }

  public void stop() throws Exception {
    if (server != null && server.isStarted()) {
      server.stop();
      server.join();
    }
  }

  public String getUrl() {
    return "https://localhost:" + httpsPort;
  }

  private ServletContextHandler proxyHandler() {
    ServletContextHandler contextHandler = new ServletContextHandler();
    contextHandler.setServletHandler(newServletHandler());
    return contextHandler;
  }

  private ServletHandler newServletHandler() {
    ServletHandler handler = new ServletHandler();
    ServletHolder holder = handler.addServletWithMapping(ProxyServlet.Transparent.class, "/*");
    holder.setInitParameter("proxyTo", this.proxyTo);
    return handler;
  }

}
