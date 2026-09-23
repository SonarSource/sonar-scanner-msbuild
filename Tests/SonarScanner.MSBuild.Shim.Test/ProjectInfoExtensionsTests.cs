/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class ProjectInfoExtensionsTests
{
    private readonly TestLogger logger = new();

    [TestMethod]
    [DataRow(null)]
    [DataRow("FOO")]
    public void FixEncoding_WithNullEncoding_NullGlobalEncoding_NotSupportedProject(string projectLanguage)
    {
        var sut = new ProjectInfo
        {
            ProjectLanguage = projectLanguage,
            Encoding = null
        };

        sut.FixEncoding(null, logger);
        sut.Encoding.Should().BeNull();
        logger.Should().HaveNoInfos();
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp)]
    [DataRow(ProjectLanguages.VisualBasic)]
    public void FixEncoding_WithNullEncoding_NullGlobalEncoding_SupportedProject(string projectLanguage)
    {
        var sut = new ProjectInfo
        {
            ProjectLanguage = projectLanguage,
            Encoding = null
        };

        sut.FixEncoding(null, logger);
        sut.Encoding.Should().Be(Encoding.UTF8.WebName);
        logger.Should().HaveNoInfos();
    }

    [TestMethod]
    public void FixEncoding_WithNullEncoding_GlobalEncoding()
    {
        var sut = new ProjectInfo { Encoding = null };

        sut.FixEncoding("FOO", logger);
        sut.Encoding.Should().Be("FOO");
        logger.Should().HaveNoInfos();
    }

    [TestMethod]
    public void FixEncoding_WithEncoding_GlobalEncoding()
    {
        var sut = new ProjectInfo { Encoding = "FOO" };

        sut.FixEncoding("BAR", logger);
        sut.Encoding.Should().Be("FOO");
        logger.Should().HaveInfos("""Property "sonar.sourceEncoding" is defined, but will be ignored during analysis.""");
    }

    [TestMethod]
    public void FixEncoding_WithEncoding_NullGlobalEncoding()
    {
        var sut = new ProjectInfo { Encoding = "FOO" };

        sut.FixEncoding(null, logger);
        sut.Encoding.Should().Be("FOO");
        logger.Should().HaveNoInfos();
    }
}
