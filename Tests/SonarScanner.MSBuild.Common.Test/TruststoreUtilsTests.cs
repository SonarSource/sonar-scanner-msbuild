/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto: info AT sonarsource DOT com
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

using TestUtilities.Certificates;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class TruststoreUtilsTests
{
    [TestMethod]
    public void TruststoreDefaultPassword_TruststorePathNull()
    {
        var logger = new TestLogger();

        var result = TruststoreUtils.TruststoreDefaultPassword(null, logger);

        result.Should().Be("changeit");
        logger.DebugMessages.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("doesnotexist.p12")]
    [DataRow("folder/doesnotexist.p12")]
    public void TruststoreDefaultPassword_TruststoreDoesNotExists(string path)
    {
        var logger = new TestLogger();

        var result = TruststoreUtils.TruststoreDefaultPassword(path, logger);

        result.Should().Be("changeit");
        logger.DebugMessages.Should().ContainMatch($"Could not import the truststore '{path}' with the default password at index 0. Reason: *");
        logger.DebugMessages.Should().ContainMatch($"Could not import the truststore '{path}' with the default password at index 1. Reason: *");
    }

    [TestMethod]
    public void TruststoreDefaultPassword_IncorrectPassword()
    {
        var logger = new TestLogger();
        using var truststoreFile = new TempFile("p12");
        CertificateBuilder.CreateWebServerCertificate().ToPfx(truststoreFile.FileName, "itchange");

        var result = TruststoreUtils.TruststoreDefaultPassword(truststoreFile.FileName, logger);

        result.Should().Be("changeit");
        logger.DebugMessages.Should().ContainMatch($"Could not import the truststore '{truststoreFile.FileName}' with the default password at index 0. Reason: *");
        logger.DebugMessages.Should().ContainMatch($"Could not import the truststore '{truststoreFile.FileName}' with the default password at index 1. Reason: *");
    }

    [TestMethod]
    [DataRow("changeit", 0)]
    [DataRow("sonar", 1)]
    public void TruststoreDefaultPassword_CorrectPassword(string password, int expectedMessagesCount)
    {
        var logger = new TestLogger();
        using var truststoreFile = new TempFile("p12");
        CertificateBuilder.CreateWebServerCertificate().ToPfx(truststoreFile.FileName, password);

        var result = TruststoreUtils.TruststoreDefaultPassword(truststoreFile.FileName, logger);

        result.Should().Be(password);
        logger.DebugMessages.Should().HaveCount(expectedMessagesCount);
        for (var i = 0; i < expectedMessagesCount; i++)
        {
            logger.DebugMessages.Should().ContainMatch($"Could not import the truststore '{truststoreFile.FileName}' with the default password at index {i}. Reason: *");
        }
    }
}
