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
using SonarScanner.MSBuild.TFS.Classic.XamlBuild;
using TestUtilities;

namespace SonarScanner.MSBuild.TFS.Tests;

[TestClass]
public class LegacyTeamBuildFactoryTests
{
    [TestMethod]
    public void Ctor_Argument_Check()
    {
        Action action = () => new LegacyTeamBuildFactory(null);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void BuildLegacyBuildSummaryLogger_Arguments_Check()
    {
        var factory = new LegacyTeamBuildFactory(new TestLogger());
        factory.BuildLegacyBuildSummaryLogger(tfsUri: "tfsUri", buildUri: "buildUri").Should().BeOfType<LegacyBuildSummaryLogger>();
        factory.BuildTfsLegacyCoverageReportProcessor().Should().BeOfType<TfsLegacyCoverageReportProcessor>();
    }
}
