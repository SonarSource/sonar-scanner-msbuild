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

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.Bootstrapper.Tests
{
    [TestClass]
    public class BootstrapperSettingsTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void BootSettings_InvalidArguments()
        {
            IList<string> validArgs = null;

            Action act = () => new BootstrapperSettings(AnalysisPhase.PreProcessing, validArgs, LoggerVerbosity.Debug, null);
            act.Should().ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        public void BootSettings_Properties()
        {
            // Check the properties values and that relative paths are turned into absolute paths

            // 0. Setup
            var logger = new TestLogger();

            using (var envScope = new EnvironmentVariableScope())
            {
                envScope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, @"c:\temp");

                // 1. Default value -> relative to download dir
                new BootstrapperSettings(AnalysisPhase.PreProcessing, null, LoggerVerbosity.Debug, logger);
            }
        }

        #endregion Tests
    }
}
