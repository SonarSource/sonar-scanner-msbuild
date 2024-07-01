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
using static SonarScanner.MSBuild.Common.OperatingSystemProvider;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class OperatingSystemProviderTests
{
    [TestMethod]
    public void GetFolderPath_WithUserProfile()
    {
        var sut = new OperatingSystemProvider(FileWrapper.Instance);
        sut.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.None)
            .Should().Be(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.None));
    }

    [TestMethod]
    public void DirectoryExists_WithCurrentDirectory()
    {
        var sut = new OperatingSystemProvider(FileWrapper.Instance);
        sut.DirectoryExists(Environment.CurrentDirectory).Should().BeTrue();
    }

    [DataTestMethod]
    [DataRow("/etc/os-release", "alpine", true)]
    [DataRow("/etc/os-release", "ubuntu", false)]
    [DataRow("/etc/os-release", "\"rocky\"", false)] // https://github.com/which-distro/os-release/blob/main/rocky/8.6
    [DataRow("/usr/lib/os-release", "alpine", true)]
    [DataRow("/usr/lib/os-release", "ubuntu", false)]
    [DataRow("/usr/lib/os-release", "\"rhel\"", false)] // https://github.com/chef/os_release/blob/main/redhat_8
    [DataRow("/etc/other-file", "alpine", false)]
    public void IsAlpine_ChecksFileContent(string path, string id, bool expectedValue)
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(path).Returns(_ => true);
        fileWrapper.ReadAllText(path).Returns(_ => $"""
                                                    NAME="Alpine Linux"
                                                    ID={id}
                                                    VERSION_ID=3.17.0
                                                    PRETTY_NAME="Alpine Linux v3.17"
                                                    HOME_URL="https://alpinelinux.org/"
                                                    BUG_REPORT_URL="https://gitlab.alpinelinux.org/alpine/aports/-/issues"
                                                    """);
        var sut = new OperatingSystemProvider(fileWrapper);
        sut.IsAlpine().Should().Be(expectedValue);
    }
}
