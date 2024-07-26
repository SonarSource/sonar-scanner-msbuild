/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

namespace SonarScanner.MSBuild.PostProcessor.Test;

[TestClass]
public class AnalysisWarningProcessorTests
{
    private readonly IFileWrapper fileWrapper = Substitute.For<IFileWrapper>();
    private readonly TestLogger logger = new();

    [TestMethod]
    public void AnalysisWarningProcessor_Constructor()
    {
        ((Action)(() => AnalysisWarningProcessor.Process(null, null, null, null))).Should()
            .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("warnings");

        ((Action)(() => AnalysisWarningProcessor.Process([], null, null, null))).Should()
            .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("outputPath");

        ((Action)(() => AnalysisWarningProcessor.Process([], string.Empty, null, null))).Should()
            .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("fileWrapper");

        ((Action)(() => AnalysisWarningProcessor.Process([], string.Empty, Substitute.For<IFileWrapper>(), null))).Should()
            .ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void AnalysisWarningProcessor_Process_EmptyWarnings()
    {
        AnalysisWarningProcessor.Process([], string.Empty, fileWrapper, logger);

        logger.AssertNoWarningsLogged();
        fileWrapper.Received(0).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void AnalysisWarningProcessor_Process_MultipleWarnings()
    {
        AnalysisWarningProcessor.Process(["exploding", "whale"], string.Empty, fileWrapper, logger);

        logger.Warnings.Should().BeEquivalentTo("exploding", "whale");
        fileWrapper.Received(0).WriteAllText(string.Empty, """
            [
              {
                    "Text": "exploding"
              },
              {
                    "Text": "whale"
              }
            ]
            """);
    }
}
