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
using static SonarScanner.MSBuild.Common.EnvironmentBasedPlatformHelper;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class EnvironmentBasedPlatformHelperTests
{
    [TestMethod]
    public void GetFolderPath_WithUserProfile()
    {
        var userProfile = Instance.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.None);
        userProfile.Should().NotBeNull();
        if (Instance.IsWindows())
        {
            userProfile.Should().Contain("");
        }
    }

    [TestMethod]
    public void DirectoryExists_WithCurrentDirectory()
    {
        Instance.DirectoryExists(Environment.CurrentDirectory).Should().BeTrue();
    }

    [TestMethod]
    public void IsWindowsAndMacOSX_AreMutuallyExclusive()
    {
        (Instance.IsWindows() && Instance.IsMacOSX()).Should().BeFalse();
    }
}
