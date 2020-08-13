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

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild;
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class ProgramTests
    {
        [TestMethod]
        public void Execute_WhenIsHelp_ReturnsTrue()
        {
            var logger = new TestLogger();
            var result = Program.Execute(new string[] { "/h", "/blah", "/xxx" }, logger).Result;

            // Assert
            result.Should().Be(0);
            logger.Warnings.Should().BeEmpty();
            logger.Errors.Should().BeEmpty();
            logger.InfoMessages.Count.Should().BeGreaterThan(4); // expecting multiple lines of help output
        }

        [TestMethod]
        public void Execute_WhenInvalidDuplicateBeginArgument_ReturnsFalse()
        {
            var logger = new TestLogger();
            var result = Program.Execute(new string[] { "begin", "begin" }, logger).Result;

            // Assert
            result.Should().Be(1);
            logger.Errors.Should().HaveCount(1);
        }
    }
}
