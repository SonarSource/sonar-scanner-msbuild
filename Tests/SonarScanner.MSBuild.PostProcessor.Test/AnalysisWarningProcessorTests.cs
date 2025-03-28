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
using TestUtilities;

using static FluentAssertions.FluentActions;

namespace SonarScanner.MSBuild.PostProcessor.Test;

[TestClass]
public class AnalysisWarningProcessorTests
{
    private readonly IFileWrapper fileWrapper = Substitute.For<IFileWrapper>();
    private readonly TestLogger logger = new();

    [TestMethod]
    public void AnalysisWarningProcessor_Constructor()
    {
        Invoking(() => AnalysisWarningProcessor.Process(null, null, null, null)).Should()
            .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("warnings");

        Invoking(() => AnalysisWarningProcessor.Process([], null, null, null)).Should()
            .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("outputPath");

        Invoking(() => AnalysisWarningProcessor.Process([], string.Empty, null, null)).Should()
            .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("fileWrapper");

        Invoking(() => AnalysisWarningProcessor.Process([], string.Empty, Substitute.For<IFileWrapper>(), null)).Should()
            .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void AnalysisWarningProcessor_Process_EmptyWarnings()
    {
        AnalysisWarningProcessor.Process([], string.Empty, fileWrapper, logger);

        logger.AssertNoWarningsLogged();
        fileWrapper.DidNotReceiveWithAnyArgs().WriteAllText(default, default);
    }

    [TestCategory("NoUnixNeedsReview")]
    [TestMethod]
    public void AnalysisWarningProcessor_Process_MultipleWarnings()
    {
        AnalysisWarningProcessor.Process(["exploding", "whale"], string.Empty, fileWrapper, logger);

        logger.Warnings.Should().BeEquivalentTo("exploding", "whale");
        fileWrapper.Received(1).WriteAllText(string.Empty, """
            [
              {
                "text": "exploding"
              },
              {
                "text": "whale"
              }
            ]
            """);
    }
}
