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
//
//  ========================================================================
//  Copyright (c) 1995-2016 Mort Bay Consulting Pty. Ltd.
//  ------------------------------------------------------------------------
//  All rights reserved. This program and the accompanying materials
//  are made available under the terms of the Eclipse Public License v1.0
//  and Apache License v2.0 which accompanies this distribution.
//
//      The Eclipse Public License is available at
//      http://www.eclipse.org/legal/epl-v10.html
//
//      The Apache License v2.0 is available at
//      http://www.opensource.org/licenses/apache2.0.php
//
//  You may elect to redistribute this code under either of these licenses.
//  ========================================================================
//

package com.sonar.it.scanner.msbuild.utils;

import jakarta.servlet.ServletRequest;
import jakarta.servlet.ServletResponse;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import org.eclipse.jetty.client.Authentication;
import org.eclipse.jetty.ee9.security.UserAuthentication;
import org.eclipse.jetty.ee9.security.authentication.DeferredAuthentication;
import org.eclipse.jetty.http.HttpHeader;
import org.eclipse.jetty.security.AuthenticationState;
import org.eclipse.jetty.security.Authenticator;
import org.eclipse.jetty.security.ServerAuthException;
import org.eclipse.jetty.security.UserIdentity;
import org.eclipse.jetty.security.authentication.BasicAuthenticator;
import org.eclipse.jetty.security.authentication.LoginAuthenticator;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import org.eclipse.jetty.server.Request;
import org.eclipse.jetty.server.Response;
import org.eclipse.jetty.util.Callback;

/**
 * Inspired from {@link BasicAuthenticator} but adapted for proxy auth.
 */
public class ProxyAuthenticator extends LoginAuthenticator {
  /* ------------------------------------------------------------ */
  public ProxyAuthenticator() {
  }

  /* ------------------------------------------------------------ */

  /**
   * @see Authenticator#getAuthenticationType()
   */
  @Override
  public String getAuthenticationType() {
    return Authenticator.BASIC_AUTH;
  }

  /* ------------------------------------------------------------ */

  /**
   * @see Authenticator#validateRequest(Request, Response, Callback)
   */
  @Override
  public AuthenticationState validateRequest(Request req, Response res, Callback callback)  {
    String credentials = req.getHeaders().get(HttpHeader.PROXY_AUTHORIZATION);
    if (credentials != null) {
      int space = credentials.indexOf(' ');
      if (space > 0) {
        String method = credentials.substring(0, space);
        if ("basic".equalsIgnoreCase(method)) {
          credentials = credentials.substring(space + 1);
          credentials = new String(Base64.getDecoder().decode(credentials), StandardCharsets.ISO_8859_1);
          int i = credentials.indexOf(':');
          if (i > 0) {
            String username = credentials.substring(0, i);
            String password = credentials.substring(i + 1);

            return AuthenticationState.login(username, password, req, res);
          }
        }
      }
    }

    res.getHeaders().add(HttpHeader.PROXY_AUTHENTICATE.asString(), "basic realm=\"" + _loginService.getName() + '"');
    res.setStatus(HttpServletResponse.SC_PROXY_AUTHENTICATION_REQUIRED);
    return AuthenticationState.CHALLENGE;
  }
}
