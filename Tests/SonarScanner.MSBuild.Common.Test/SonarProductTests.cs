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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Common.Test
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
        [DataRow(true, "8.0")]
        [DataRow(true, "8.0.0.18955")]
        [DataRow(true, "8.0.1")]
        [DataRow(false, "8.0.0.29455", DisplayName = "SQ 8.0 build version")]
        [DataRow(false, "6.7")]
        [DataRow(false, "7.9")]
        [DataRow(false, "9.0")]
        [DataRow(false, "10.0")]
        public void IsSonarCloud(bool expected, string version) =>
            SonarProduct.IsSonarCloud(new Version(version)).Should().Be(expected);
    }
}
