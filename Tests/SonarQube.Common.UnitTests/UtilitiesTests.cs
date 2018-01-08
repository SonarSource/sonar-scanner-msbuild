/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class UtilitiesTests
    {
        [TestMethod]
        public void VersionDisplayString()
        {
            CheckVersionString("1.2.0.0", "1.2");
            CheckVersionString("1.0.0.0", "1.0");
            CheckVersionString("0.0.0.0", "0.0");
            CheckVersionString("1.2.3.0", "1.2.3");

            CheckVersionString("1.2.0.4", "1.2.0.4");
            CheckVersionString("1.2.3.4", "1.2.3.4");
            CheckVersionString("0.2.3.4", "0.2.3.4");
            CheckVersionString("0.0.3.4", "0.0.3.4");
        }

        private static void CheckVersionString(string version, string expectedDisplayString)
        {
            var actualVersion = new Version(version);
            var actualVersionString = actualVersion.ToDisplayString();

            Assert.AreEqual(expectedDisplayString, actualVersionString);
        }
    }
}
