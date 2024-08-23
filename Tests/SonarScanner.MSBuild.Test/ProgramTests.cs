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

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.Test;

[TestClass]
public class ProgramTests
{
    [TestMethod]
    public async Task Execute_WhenIsHelp_ReturnsTrue()
    {
        var logger = new TestLogger();

        var result = await Program.Execute(["/h", "/blah", "/xxx"], logger);

        result.Should().Be(0);
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
        logger.InfoMessages.Should().HaveCount(3);
        logger.InfoMessages[0].Should().Contain("SonarScanner for MSBuild");
        logger.InfoMessages[1].Should().Contain("Using the .NET Framework version of the Scanner for MSBuild");
        logger.InfoMessages[2].Should().Contain("Usage");
    }

    [TestMethod]
    public void Execute_WhenInvalidDuplicateBeginArgument_ReturnsFalse()
    {
        var logger = new TestLogger();
        var result = Program.Execute(["begin", "begin"], logger).Result;

        // Assert
        result.Should().Be(1);
        logger.Errors.Should().ContainSingle();
    }
}
