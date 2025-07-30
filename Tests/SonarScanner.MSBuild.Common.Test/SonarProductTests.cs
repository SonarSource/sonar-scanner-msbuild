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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class SonarProductTests
{
    [TestMethod]
    [DataRow("8.0", true)]
    [DataRow("8.0.0.18955", true)]
    [DataRow("8.0.1", true)]
    [DataRow("8.0.0.29455", false)]
    [DataRow("6.7", false)]
    [DataRow("7.9", false)]
    [DataRow("9.0", false)]
    [DataRow("10.0", false)]
    public void IsSonarCloud(string version, bool expected) =>
        SonarProduct.IsSonarCloud(new Version(version)).Should().Be(expected);
}
