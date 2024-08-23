/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class EnvScannerPropertiesProviderTest
{
    [TestMethod]
    public void ParseValidJson()
    {
        var provider = new EnvScannerPropertiesProvider("{ \"sonar.host.url\": \"http://myhost\"}");
        provider.GetAllProperties().Should().HaveCount(1);
        provider.GetAllProperties().First().Id.Should().Be("sonar.host.url");
        provider.GetAllProperties().First().Value.Should().Be("http://myhost");
    }

    [TestMethod]
    public void TryCreateProvider_WithNullLogger_Throws()
    {
        // Arrange
        Action action = () => EnvScannerPropertiesProvider.TryCreateProvider(null, out var provider);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void ParseInvalidJson()
    {
        var logger = new TestLogger();

        // Make sure the test isn't affected by the hosting environment and
        // does not affect the hosting environment
        // The SonarCloud AzDO extension sets additional properties in an environment variable that
        // would affect the test.
        using var scope = new EnvironmentVariableScope().SetVariable("SONARQUBE_SCANNER_PARAMS", "trash");
        var result = EnvScannerPropertiesProvider.TryCreateProvider(logger, out _);
        result.Should().BeFalse();
        logger.AssertWarningLogged("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS' because 'Error parsing boolean value. Path '', line 1, position 2.'.");
    }

    [TestMethod]
    public void NonExistingEnvVar()
    {
        var provider = new EnvScannerPropertiesProvider(null);
        provider.GetAllProperties().Should().BeEmpty();
    }
}
