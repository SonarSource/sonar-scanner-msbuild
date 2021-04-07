/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
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

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Common.UnitTests
{
    [TestClass]
    public class SonarProductTests
    {
        [DataTestMethod]
        [DataRow(null, "SonarQube")]
        [DataRow("https://localhost:9000", "SonarQube")]
        [DataRow("https://next.sonarqube.com", "SonarQube")]
        [DataRow("sonarcloud.io", "SonarCloud")]
        [DataRow("https://sonarcloud.io", "SonarCloud")]
        [DataRow("https://SonarCloud.io", "SonarCloud")]
        [DataRow("https://SONARCLOUD.IO.custom.dns.proxy", "SonarCloud")]
        [DataRow("https://sonarcloud.custom.dns.proxy", "SonarQube")]
        public void GetSonarProductToLog(string host, string expectedName) =>
            SonarProduct.GetSonarProductToLog(host).Should().Be(expectedName);

        [DataTestMethod]
        [DataRow(true, "https://sonarcloud.io", "8.0")]
        [DataRow(true, "https://SonarCloud.io", "8.0")]
        [DataRow(true, "https://SONARCLOUD.IO.custom.dns.proxy", "8.0")]
        [DataRow(true, null, "8.0")]
        [DataRow(true, null, "8.0.0.18955")]
        [DataRow(true, null, "8.0.1")]
        [DataRow(true, "https://sonarcloud.io", "8.0.0.29455", DisplayName = "SC with same build as SQ 8.0")]
        [DataRow(false, "https://something.io", "8.0.0.29455", DisplayName = "SQ 8.0 build version")]
        [DataRow(false, "https://localhost:9000", "6.7")]
        [DataRow(false, "https://localhost:9000", "7.9")]
        [DataRow(false, null, "7.9")]
        [DataRow(false, null, "9.0")]
        [DataRow(false, null, "10.0")]
        [DataRow(false, "https://sonarcloud.io", "7.0")]    // SC is defined as "Version 8.0"
        [DataRow(false, "https://sonarcloud.io", "9.0")]    // SC is defined as "Version 8.0"
        public void IsSonarCloud(bool expected, string host, string version) =>
            SonarProduct.IsSonarCloud(host, new Version(version)).Should().Be(expected);
    }
}
