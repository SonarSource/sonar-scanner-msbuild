/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.Integration.Interfaces;

namespace SonarQube.TeamBuild.PostProcessor.Tests
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

        public bool Initialise(AnalysisConfig context, ITeamBuildSettings settings, ILogger logger)
        {
            Assert.IsFalse(initalisedCalled, "Expecting Initialise to be called only once");
            initalisedCalled = true;
            return InitialiseValueToReturn;
        }

        public bool ProcessCoverageReports()
        {
            Assert.IsFalse(processCoverageMethodCalled, "Expecting ProcessCoverageReports to be called only once");
            Assert.IsTrue(initalisedCalled, "Expecting Initialise to be called first");
            processCoverageMethodCalled = true;
            return ProcessValueToReturn;
        }

        #endregion ICoverageReportProcessor interface

        #region Checks

        public void AssertExecuteCalled()
        {
            Assert.IsTrue(processCoverageMethodCalled, "Expecting the sonar-scanner to have been called");
        }

        public void AssertExecuteNotCalled()
        {
            Assert.IsFalse(processCoverageMethodCalled, "Not expecting the sonar-scanner to have been called");
        }

        public void AssertInitializedCalled()
        {
            Assert.IsTrue(initalisedCalled, "Expecting the sonar-scanner to have been called");
        }

        public void AssertInitialisedNotCalled()
        {
            Assert.IsFalse(initalisedCalled, "Not expecting the sonar-scanner to have been called");
        }

        #endregion Checks
    }
}
