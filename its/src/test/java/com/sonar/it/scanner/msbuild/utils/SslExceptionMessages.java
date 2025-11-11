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

public class SslExceptionMessages {
  public static String sslConnectionFailed() {
    return "System.Net.Http.HttpRequestException: The SSL connection could not be established, see inner exception.";
  }

  public static String untrustedRoot() {
    return "System.Security.Authentication.AuthenticationException: The remote certificate is invalid because of errors in the certificate chain: UntrustedRoot";
  }

  public static String certificateRejected() {
    return "System.Security.Authentication.AuthenticationException: The remote certificate was rejected by the provided RemoteCertificateValidationCallback.";
  }

  public static String importFail(String path) {
    return switch (OSPlatform.current()) {
      case Windows ->
        "Failed to import the sonar.scanner.truststorePath file " + path + ": The specified network password is not correct.";
      case Linux, MacOS ->
        "Failed to import the sonar.scanner.truststorePath file " + path + ": The certificate data cannot be read with the provided password, the password may be incorrect.";
    };
  }

  public static String incorrectPassword() {
    return switch (OSPlatform.current()) {
      case Windows ->
        "System.Security.Cryptography.CryptographicException: The specified network password is not correct.";
      case Linux, MacOS ->
        "System.Security.Cryptography.CryptographicException: The certificate data cannot be read with the provided password, the password may be incorrect.";
    };
  }
}
