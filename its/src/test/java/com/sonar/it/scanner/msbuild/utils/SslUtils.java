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

package com.sonar.it.scanner.msbuild.utils;

import java.io.FileOutputStream;
import java.math.BigInteger;
import java.nio.file.Path;
import java.security.GeneralSecurityException;
import java.security.KeyPair;
import java.security.KeyPairGenerator;
import java.security.KeyStore;
import java.security.SecureRandom;
import java.security.Security;
import java.security.cert.Certificate;
import java.security.cert.X509Certificate;
import java.util.Date;
import javax.security.auth.x500.X500Principal;
import org.bouncycastle.asn1.ASN1Encodable;
import org.bouncycastle.asn1.DERSequence;
import org.bouncycastle.asn1.x509.BasicConstraints;
import org.bouncycastle.asn1.x509.ExtendedKeyUsage;
import org.bouncycastle.asn1.x509.Extension;
import org.bouncycastle.asn1.x509.GeneralName;
import org.bouncycastle.asn1.x509.KeyPurposeId;
import org.bouncycastle.asn1.x509.KeyUsage;
import org.bouncycastle.cert.X509v3CertificateBuilder;
import org.bouncycastle.cert.jcajce.JcaX509CertificateConverter;
import org.bouncycastle.cert.jcajce.JcaX509v3CertificateBuilder;
import org.bouncycastle.jce.provider.BouncyCastleProvider;
import org.bouncycastle.operator.ContentSigner;
import org.bouncycastle.operator.jcajce.JcaContentSignerBuilder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class SslUtils {
  static final Logger LOG = LoggerFactory.getLogger(SslUtils.class);

  public static String generateKeyStore(Path outputPath, String host, String password) {
    try {
      LOG.info("Generating keystore for host {}", host);
      var keyPair = generateKeyPair();
      var cert = generateSelfSignedCertificate(host, keyPair);
      KeyStore keyStore = KeyStore.getInstance("PKCS12");
      keyStore.load(null, null);

      Certificate[] chain = {cert};
      keyStore.setKeyEntry("key", keyPair.getPrivate(), password.toCharArray(), chain);

      try (FileOutputStream fos = new FileOutputStream(outputPath.toFile())) {
        keyStore.store(fos, password.toCharArray());
      }
      return outputPath.toString();
    } catch (Exception e) {
      throw new RuntimeException(e);
    }
  }

  public static X509Certificate generateSelfSignedCertificate(String host, KeyPair keyPair) {
    Security.addProvider(new BouncyCastleProvider());

    X500Principal subject = new X500Principal("CN=" + host);

    long notBefore = System.currentTimeMillis();
    long notAfter = notBefore + (1000L * 3600L * 24 * 365);

    ASN1Encodable[] encodableAltNames = new ASN1Encodable[]{new GeneralName(GeneralName.dNSName, host)};
    KeyPurposeId[] purposes = new KeyPurposeId[]{KeyPurposeId.id_kp_serverAuth, KeyPurposeId.id_kp_clientAuth};

    X509v3CertificateBuilder certBuilder = new JcaX509v3CertificateBuilder(subject, BigInteger.ONE, new Date(notBefore), new Date(notAfter), subject, keyPair.getPublic());

    try {
      certBuilder.addExtension(Extension.basicConstraints, true, new BasicConstraints(false));
      certBuilder.addExtension(Extension.keyUsage, true, new KeyUsage(KeyUsage.digitalSignature + KeyUsage.keyEncipherment));
      certBuilder.addExtension(Extension.extendedKeyUsage, false, new ExtendedKeyUsage(purposes));
      certBuilder.addExtension(Extension.subjectAlternativeName, false, new DERSequence(encodableAltNames));

      final ContentSigner signer = new JcaContentSignerBuilder(("SHA256withRSA")).build(keyPair.getPrivate());

      return new JcaX509CertificateConverter().getCertificate(certBuilder.build(signer));

    } catch (Exception e) {
      throw new AssertionError(e.getMessage());
    }
  }

  private static KeyPair generateKeyPair() {
    try {
      KeyPairGenerator keyPairGenerator = KeyPairGenerator.getInstance("RSA");
      keyPairGenerator.initialize(2048, new SecureRandom());
      return keyPairGenerator.generateKeyPair();
    } catch (GeneralSecurityException var2) {
      throw new AssertionError(var2);
    }
  }
}
