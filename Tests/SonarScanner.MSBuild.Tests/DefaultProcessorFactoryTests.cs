/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild;
using SonarScanner.MSBuild.PostProcessor;
using SonarScanner.MSBuild.PreProcessor;
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class DefaultProcessorFactoryTests
    {
        [TestMethod]
        public void Ctor_WhenLegacyTeamBuildFactoryIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => new DefaultProcessorFactory(new TestLogger(), null, new NullCoverageReportConverter());

            // Act & Assert
            action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("legacyTeamBuildFactory");
        }

        [TestMethod]
        public void Ctor_WhenCoverageReportConverterIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => new DefaultProcessorFactory(new TestLogger(), new NotSupportedLegacyTeamBuildFactory(), null);

            // Act & Assert
            action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("coverageReportConverter");
        }

        [TestMethod]
        public void CreatePreProcessor_Returns_New_Instance()
        {
            var factory = new DefaultProcessorFactory(
                new TestLogger(),
                new NotSupportedLegacyTeamBuildFactory(),
                new NullCoverageReportConverter());

            factory.CreatePreProcessor().Should().BeOfType<TeamBuildPreProcessor>();
        }

        [TestMethod]
        public void CreatePostProcessor_Returns_New_Instance()
        {
            var factory = new DefaultProcessorFactory(
                new TestLogger(),
                new NotSupportedLegacyTeamBuildFactory(),
                new NullCoverageReportConverter());

            factory.CreatePostProcessor().Should().BeOfType<MSBuildPostProcessor>();
        }
    }
}
