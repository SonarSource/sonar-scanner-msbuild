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

using FluentAssertions;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;

namespace SonarScanner.MSBuild.TFS.Tests
{
    internal class MockCodeCoverageProcessor : ICoverageReportProcessor
    {
        private bool processCoverageMethodCalled;
        private bool initalisedCalled;

        #region Test helpers

        public bool ProcessValueToReturn { get; set; }
        public bool InitialiseValueToReturn { get; set; }

        #endregion Test helpers

        #region ICoverageReportProcessor interface

        public bool Initialise(AnalysisConfig config, ITeamBuildSettings settings, string propertiesFilePath)
        {
            initalisedCalled.Should().BeFalse("Expecting Initialize to be called only once");
            initalisedCalled = true;
            return InitialiseValueToReturn;
        }

        public bool ProcessCoverageReports()
        {
            processCoverageMethodCalled.Should().BeFalse("Expecting ProcessCoverageReports to be called only once");
            initalisedCalled.Should().BeTrue("Expecting Initialize to be called first");
            processCoverageMethodCalled = true;
            return ProcessValueToReturn;
        }

        #endregion ICoverageReportProcessor interface

        #region Checks

        public void AssertExecuteCalled()
        {
            processCoverageMethodCalled.Should().BeTrue("Expecting the sonar-scanner to have been called");
        }

        public void AssertExecuteNotCalled()
        {
            processCoverageMethodCalled.Should().BeFalse("Not expecting the sonar-scanner to have been called");
        }

        public void AssertInitializedCalled()
        {
            initalisedCalled.Should().BeTrue("Expecting the sonar-scanner to have been called");
        }

        public void AssertInitialisedNotCalled()
        {
            initalisedCalled.Should().BeFalse("Not expecting the sonar-scanner to have been called");
        }

        #endregion Checks
    }
}
