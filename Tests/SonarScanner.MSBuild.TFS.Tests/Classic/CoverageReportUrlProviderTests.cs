/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System;
using FluentAssertions;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.TestManagement.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.TFS.Classic.XamlBuild;
using TestUtilities;

namespace SonarScanner.MSBuild.TFS.Tests
{
    [TestClass]
    public class CoverageReportUrlProviderTests
    {
        [TestMethod]
        public void Ctor_Argument_Check()
        {
            Action action = () => new CoverageReportUrlProvider(null);
            action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void GetCodeCoverageReportUrls_Arguments_Check()
        {
            var provider = new CoverageReportUrlProvider(new TestLogger());

            Action action = () => provider.GetCodeCoverageReportUrls(tfsUri: null, buildUri: "buildUri");
            action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("tfsUri");

            action = () => provider.GetCodeCoverageReportUrls(tfsUri: "tfsUri", buildUri: null);
            action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("buildUri");
        }

        [TestMethod]
        public void GetCoverageUri_ReturnsExepctedUri()
        {
            // Arrange
            var buildDetailMock = new Mock<IBuildDetail>();
            buildDetailMock.SetupGet(x => x.BuildNumber).Returns("1234");
            buildDetailMock.SetupGet(x => x.TeamProject).Returns("team-project");
            buildDetailMock.SetupGet(x => x.Uri).Returns(new Uri("http://foo.com"));
            var buildServerMock = new Mock<IBuildServer>();
            var tfsCollection = new TfsTeamProjectCollection(new Uri("http://bar.com"));
            buildServerMock.SetupGet(x => x.TeamProjectCollection).Returns(tfsCollection);
            buildDetailMock.SetupGet(x => x.BuildServer).Returns(buildServerMock.Object);

            var buildCoverageMock = new Mock<IBuildCoverage>();
            var configMock = new Mock<IBuildConfiguration>();
            configMock.SetupGet(x => x.BuildFlavor).Returns("flavor");
            configMock.SetupGet(x => x.BuildPlatform).Returns("platform");
            configMock.SetupGet(x => x.Id).Returns(1);
            buildCoverageMock.SetupGet(x => x.Configuration).Returns(configMock.Object);

            // Act
            var result = CoverageReportUrlProvider.GetCoverageUri(buildDetailMock.Object, buildCoverageMock.Object);

            // Assert
            result.Should().Be("http://bar.com//team-project/_api/_build/ItemContent?buildUri=http%3A%2F%2Ffoo.com%2F&path=%2FBuildCoverage%2F1234.flavor.platform.1.coverage");
        }
    }
}
