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

using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.PostProcessor.Test;

internal class MockSonarScanner : ISonarScanner
{
    private readonly ILogger logger;
    private bool methodCalled;

    public string ErrorToLog { get; set; }
    public bool ValueToReturn { get; set; } = true;
    public IEnumerable<string> SuppliedCommandLineArgs { get; set; }

    public MockSonarScanner(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool Execute(AnalysisConfig config, IAnalysisPropertyProvider userCmdLineArguments, string propertiesFilePath)
    {
        methodCalled.Should().BeFalse("the scanner should only be called once.");
        methodCalled = true;
        SuppliedCommandLineArgs = userCmdLineArguments.GetAllProperties().Select(x => x.AsSonarScannerArg());
        if (ErrorToLog is not null)
        {
            logger.LogError(ErrorToLog);
        }
        return ValueToReturn;
    }

    public void AssertExecuted()
    {
        methodCalled.Should().BeTrue("the scanner should have been called.");
    }

    public void AssertNotExecuted()
    {
        methodCalled.Should().BeFalse("the scanner should not have been called.");
    }
}
