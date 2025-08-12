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

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.Common.TFS;
using SonarScanner.MSBuild.TFS;
using SonarScanner.MSBuild.TFS.Tests.Infrastructure;
using TestUtilities;

namespace SonarScanner.MSBuild.PostProcessor.Tests;

[TestClass]
public class CoverageReportProcessorTests
{
    private ILegacyTeamBuildFactory legacyFactory;
    private ICoverageReportConverter reportConverter;
    private CoverageReportProcessor processor;

    [TestInitialize]
    public void TestInitialize()
    {
        reportConverter = Substitute.For<ICoverageReportConverter>();
        legacyFactory = Substitute.For<ILegacyTeamBuildFactory>();
        processor = new CoverageReportProcessor(legacyFactory, reportConverter, new TestLogger());
    }

    public void Ctor_WhenLegacyFactoryIsNull_Throws()
    {
        // Arrange
        Action act = () => new CoverageReportProcessor(null, reportConverter, new TestLogger());

        // Act & Assert
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("legactTeamBuildFactory");
    }

    public void Ctor_WhenConverterIsNull_Throws()
    {
        // Arrange
        Action act = () => new CoverageReportProcessor(legacyFactory, null, new TestLogger());

        // Act & Assert
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("coverageReportConverter");
    }

    public void Ctor_WhenLoggerIsNull_Throws()
    {
        // Arrange
        Action act = () => new CoverageReportProcessor(legacyFactory, reportConverter, null);

        // Act & Assert
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void Initialize_Checks_Arguments_For_Null()
    {
        // Arrange
        var analysisConfig = new AnalysisConfig();
        Action act = () => processor.Initialize(analysisConfig, null, string.Empty);

        // Act & Assert
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("settings");
    }

    [TestMethod]
    public void ProcessCoverageReports_LegacyTeamBuild_SkipCoverageIsFalse_WhenProcess_CallsLegacyFactoryThenCallsReturnedProcessor()
    {
        // Arrange
        var analysisConfig = new AnalysisConfig { LocalSettings = new AnalysisProperties() };
        var settingsMock = new MockBuildSettings { BuildEnvironment = BuildEnvironment.LegacyTeamBuild };
        var logger = new TestLogger();

        // Set up the factory to return a processor that returns success
        var processor = Substitute.For<ICoverageReportProcessor>();
        processor.Initialize(Arg.Any<AnalysisConfig>(), Arg.Any<IBuildSettings>(), Arg.Any<string>()).Returns(true);
        processor.ProcessCoverageReports(logger).Returns(true);
        legacyFactory.BuildTfsLegacyCoverageReportProcessor().Returns(processor);

        using (var scope = new EnvironmentVariableScope())
        {
            scope.SetVariable(BuildSettings.EnvironmentVariables.SkipLegacyCodeCoverage, "false");

            var testSubject = new CoverageReportProcessor(legacyFactory, reportConverter, logger);
            testSubject.Initialize(analysisConfig, settingsMock, string.Empty);

            // Act
            var result = testSubject.ProcessCoverageReports(logger);

            // Assert
            result.Should().BeTrue();
            legacyFactory.Received(1).BuildTfsLegacyCoverageReportProcessor();
            processor.Received(1).Initialize(Arg.Any<AnalysisConfig>(), Arg.Any<IBuildSettings>(), Arg.Any<string>());
            processor.Received(1).ProcessCoverageReports(logger);
        }
    }

    [TestMethod]
    public void ProcessCoverageReports_LegacyTeamBuild_SkipCoverageIsTrue_WhenProcess_CallsLegacyFactoryThenCallsReturnedProcessor()
    {
        // Arrange
        var analysisConfig = new AnalysisConfig { LocalSettings = [] };
        var settingsMock = new MockBuildSettings { BuildEnvironment = BuildEnvironment.LegacyTeamBuild };
        var logger = new TestLogger();

        using (var scope = new EnvironmentVariableScope())
        {
            scope.SetVariable(BuildSettings.EnvironmentVariables.SkipLegacyCodeCoverage, "true");

            var testSubject = new CoverageReportProcessor(legacyFactory, reportConverter, logger);
            testSubject.Initialize(analysisConfig, settingsMock, string.Empty);

            // Act
            var result = false;
            using (new AssertIgnoreScope())
            {
                result = testSubject.ProcessCoverageReports(logger);
            }

            // Assert
            result.Should().BeTrue();
            legacyFactory.DidNotReceive().BuildTfsLegacyCoverageReportProcessor();
        }
    }

    [TestMethod]
    public void ProcessCoverageReports_Standalone_WhenProcess_ReturnsTrue()
    {
        // Arrange
        var analysisConfig = new AnalysisConfig { LocalSettings = [] };
        var settings = new MockBuildSettings { BuildEnvironment = BuildEnvironment.NotTeamBuild };
        var logger = new TestLogger();

        var testSubject = new CoverageReportProcessor(legacyFactory, reportConverter, logger);
        testSubject.Initialize(analysisConfig, settings, string.Empty);

        // Act
        var result = false;
        using (new AssertIgnoreScope())
        {
            result = testSubject.ProcessCoverageReports(logger);
        }

        // Assert
        result.Should().BeTrue(); // false would cause the remaining processing to stop
        legacyFactory.DidNotReceive().BuildTfsLegacyCoverageReportProcessor();
    }
}
