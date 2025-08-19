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

using SonarScanner.MSBuild.Common.TFS;
using SonarScanner.MSBuild.TFS.Tests.Infrastructure;

namespace SonarScanner.MSBuild.TFS.Test;

[TestClass]
public class CoverageReportProcessorTests
{
    private ILegacyTeamBuildFactory legacyFactory;
    private CoverageReportProcessor processor;

    [TestInitialize]
    public void TestInitialize()
    {
        legacyFactory = Substitute.For<ILegacyTeamBuildFactory>();
        processor = new CoverageReportProcessor(legacyFactory);
    }

    [TestMethod]
    public void Ctor_WhenLegacyFactoryIsNull_Throws()
    {
        Action act = () => _ = new CoverageReportProcessor(null);
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("legacyTeamBuildFactory");
    }

    [TestMethod]
    public void Initialize_Checks_Arguments_For_Null()
    {
        Action act = () => processor.Initialize(new AnalysisConfig(), null, string.Empty);
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("settings");
    }

    [TestMethod]
    public void ProcessCoverageReports_LegacyTeamBuild_SkipCoverageIsFalse_WhenProcess_CallsLegacyFactoryThenCallsReturnedProcessor()
    {
        var analysisConfig = new AnalysisConfig { LocalSettings = [] };
        var settingsMock = new MockBuildSettings { BuildEnvironment = BuildEnvironment.LegacyTeamBuild };
        var logger = new TestLogger();

        // Set up the factory to return a processor that returns success
        var processorSub = Substitute.For<ICoverageReportProcessor>();
        processorSub.Initialize(Arg.Any<AnalysisConfig>(), Arg.Any<IBuildSettings>(), Arg.Any<string>()).Returns(true);
        processorSub.ProcessCoverageReports(logger).Returns(true);
        legacyFactory.BuildTfsLegacyCoverageReportProcessor().Returns(processorSub);

        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(BuildSettings.EnvironmentVariables.SkipLegacyCodeCoverage, "false");

        var testSubject = new CoverageReportProcessor(legacyFactory);
        testSubject.Initialize(analysisConfig, settingsMock, string.Empty);

        var result = testSubject.ProcessCoverageReports(logger);

        result.Should().BeTrue();
        legacyFactory.Received(1).BuildTfsLegacyCoverageReportProcessor();
        processorSub.Received(1).Initialize(Arg.Any<AnalysisConfig>(), Arg.Any<IBuildSettings>(), Arg.Any<string>());
        processorSub.Received(1).ProcessCoverageReports(logger);
    }

    [TestMethod]
    public void ProcessCoverageReports_LegacyTeamBuild_SkipCoverageIsTrue_WhenProcess_CallsLegacyFactoryThenCallsReturnedProcessor()
    {
        var analysisConfig = new AnalysisConfig { LocalSettings = [] };
        var settingsMock = new MockBuildSettings { BuildEnvironment = BuildEnvironment.LegacyTeamBuild };
        var logger = new TestLogger();

        using var scope = new EnvironmentVariableScope();
        scope.SetVariable(BuildSettings.EnvironmentVariables.SkipLegacyCodeCoverage, "true");

        var testSubject = new CoverageReportProcessor(legacyFactory);
        testSubject.Initialize(analysisConfig, settingsMock, string.Empty);

        var result = false;
        using (new AssertIgnoreScope())
        {
            result = testSubject.ProcessCoverageReports(logger);
        }

        result.Should().BeTrue();
        legacyFactory.DidNotReceive().BuildTfsLegacyCoverageReportProcessor();
    }

    [TestMethod]
    public void ProcessCoverageReports_Standalone_WhenProcess_ReturnsTrue()
    {
        var analysisConfig = new AnalysisConfig { LocalSettings = [] };
        var settings = new MockBuildSettings { BuildEnvironment = BuildEnvironment.NotTeamBuild };
        var logger = new TestLogger();

        var testSubject = new CoverageReportProcessor(legacyFactory);
        testSubject.Initialize(analysisConfig, settings, string.Empty);

        var result = false;
        using (new AssertIgnoreScope())
        {
            result = testSubject.ProcessCoverageReports(logger);
        }

        result.Should().BeTrue(); // false would cause the remaining processing to stop
        legacyFactory.DidNotReceive().BuildTfsLegacyCoverageReportProcessor();
    }
}
