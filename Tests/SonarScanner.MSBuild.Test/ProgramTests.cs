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

namespace SonarScanner.MSBuild.Test;

[TestClass]
public class ProgramTests
{
    [TestMethod]
    public async Task Main_NoArg_ReturnsSuccessCode() =>
        (await Program.Main([])).Should().Be(0, "because the Usage information should be displayed");

    [TestMethod]
    public async Task Main_InvalidPhaseArg_ReturnsErrorCode() =>
        (await Program.Main(["MIDDLE"])).Should().Be(1, "because no valid phase argument was passed");

    [TestMethod]
    public async Task Execute_WhenIsHelp_ReturnsTrue()
    {
        var runtime = new TestRuntime();
        var logger = runtime.Logger;

        var result = await Program.Execute(["/h", "/blah", "/xxx"], runtime);

        result.Should().Be(0);
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
        logger.InfoMessages.Should().HaveCount(3);
        logger.InfoMessages[0].Should().Contain("SonarScanner for .NET");
#if NETFRAMEWORK
        logger.InfoMessages[1].Should().Contain("Using the .NET Framework version of the Scanner for .NET");
#else
        logger.InfoMessages[1].Should().Contain("Using the .NET Core version of the Scanner for .NET");
#endif
        logger.InfoMessages[2].Should().Contain("Usage");
    }

    [TestMethod]
    public void Execute_WhenInvalidDuplicateBeginArgument_ReturnsFalse()
    {
        var runtime = new TestRuntime();
        var result = Program.Execute(["begin", "begin"], runtime).Result;

        result.Should().Be(1);
        runtime.Logger.Errors.Should().ContainSingle();
    }
}
