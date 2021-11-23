/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2021 SonarSource SA
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

using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Test
{
    [TestClass]
    public class RollForwardTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void MustRollForward()
        {
            var runtimeconfigPath = Path.Combine(TestContext.DeploymentDirectory, "SonarScanner.MSBuild.runtimeconfig.json");
            Assert.IsTrue(File.Exists(runtimeconfigPath), $"[TEST ERROR] The runtimeconfig file could not be found: {runtimeconfigPath}");

            var json = JsonDocument.Parse(File.ReadAllText(runtimeconfigPath));
            var runtimeOptions = json.RootElement.GetProperty("runtimeOptions");
            Assert.IsNotNull(runtimeOptions, "runtimeOptions should not be null");

            var rollForward = runtimeOptions.GetProperty("rollForward");
            Assert.IsNotNull(rollForward, "runtimOptions should have the rollForward property");
            Assert.AreEqual("LatestMajor", rollForward.GetString());

            var tfm = runtimeOptions.GetProperty("tfm");
            Assert.IsNotNull(tfm, "runtimOptions should have the tfm property");
            Assert.AreEqual("net5.0", tfm.GetString());
        }
    }
}
