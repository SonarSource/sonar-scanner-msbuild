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
using System.Collections.Generic;
using FluentAssertions;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.PostProcessor.Test;

internal class MockSonarScanner : ISonarScanner
{
    private bool methodCalled;
    private readonly ILogger logger;

    #region Test Helpers

    public string ErrorToLog { get; set; }

    public bool ValueToReturn { get; set; }

    public IEnumerable<string> SuppliedCommandLineArgs { get; set; }

    #endregion Test Helpers

    public MockSonarScanner(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region ISonarScanner interface

    public bool Execute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, string fullPropertiesFilePath)
    {
        methodCalled.Should().BeFalse("Scanner should only be called once");
        methodCalled = true;
        SuppliedCommandLineArgs = userCmdLineArguments;
        if (ErrorToLog != null)
        {
            logger.LogError(ErrorToLog);
        }

        return ValueToReturn;
    }

    #endregion ISonarScanner interface

    #region Checks

    public void AssertExecuted()
    {
        methodCalled.Should().BeTrue("Expecting the sonar-scanner to have been called");
    }

    public void AssertNotExecuted()
    {
        methodCalled.Should().BeFalse("Not expecting the sonar-scanner to have been called");
    }

    #endregion Checks
}
