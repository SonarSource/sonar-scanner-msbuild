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

using FluentAssertions;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.TFS;

namespace SonarScanner.MSBuild.PostProcessor.Tests;

internal class MockSummaryReportBuilder : ISummaryReportBuilder
{
    private bool methodCalled;

    #region ISummaryReportBuilder interface

    public void GenerateReports(IBuildSettings settings, AnalysisConfig config, bool ranToCompletion, string fullPropertiesFilePath, ILogger logger)
    {
        methodCalled.Should().BeFalse("Generate reports has already been called");

        methodCalled = true;
    }

    #endregion ISummaryReportBuilder interface

    #region Checks

    public void AssertExecuted()
    {
        methodCalled.Should().BeTrue("Expecting ISummaryReportBuilder.GenerateReports to have been called");
    }

    public void AssertNotExecuted()
    {
        methodCalled.Should().BeFalse("Not expecting ISummaryReportBuilder.GenerateReports to have been called");
    }

    #endregion Checks
}
