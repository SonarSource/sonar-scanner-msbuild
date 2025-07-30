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
using System.Text;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class ProjectInfoExtensionsTests
{
    private static bool isLogActionInvoked;
    private readonly Action logActionMock = () => isLogActionInvoked = true;

    [TestInitialize]
    public void InitializeTests() => isLogActionInvoked = false;

    [TestMethod]
    [DataRow(null)]
    [DataRow("FOO")]
    public void FixEncoding_WithNullEncoding_NullGlobalEncoding_NotSupportedProject_DoesNothing(string projectLanguage)
    {
        ProjectInfo sut = new()
        {
            ProjectLanguage = projectLanguage,
            Encoding = null
        };

        sut.FixEncoding(null, logActionMock);

        sut.Encoding.Should().BeNull();
        isLogActionInvoked.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(ProjectLanguages.CSharp)]
    [DataRow(ProjectLanguages.VisualBasic)]
    public void FixEncoding_WithNullEncoding_NullGlobalEncoding_SupportedProject_SetsUtf8WebName(string projectLanguage)
    {
        ProjectInfo sut = new()
        {
            ProjectLanguage = projectLanguage,
            Encoding = null
        };

        sut.FixEncoding(null, logActionMock);

        sut.Encoding.Should().Be(Encoding.UTF8.WebName);
        isLogActionInvoked.Should().BeFalse();
    }

    [TestMethod]
    public void FixEncoding_WithNullEncoding_GlobalEncoding_SetsGlobalEncoding()
    {
        ProjectInfo sut = new()
        {
            Encoding = null
        };

        sut.FixEncoding("FOO", logActionMock);

        sut.Encoding.Should().Be("FOO");
        isLogActionInvoked.Should().BeFalse();
    }

    [TestMethod]
    public void FixEncoding_WithEncoding_GlobalEncoding_DoesNotChangeEncodingAndCallsLogAction()
    {
        ProjectInfo sut = new()
        {
            Encoding = "FOO"
        };

        sut.FixEncoding("BAR", logActionMock);

        sut.Encoding.Should().Be("FOO");
        isLogActionInvoked.Should().BeTrue();
    }

    [TestMethod]
    public void FixEncoding_WithEncoding_NullGlobalEncoding_DoesNothing()
    {
        ProjectInfo sut = new()
        {
            Encoding = "FOO"
        };

        sut.FixEncoding(null, logActionMock);

        sut.Encoding.Should().Be("FOO");
        isLogActionInvoked.Should().BeFalse();
    }
}
