/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Common.UnitTests
{
    [TestClass]
    public class MsBuildPathSettingsTests
    {
        [TestMethod]
        public void GetImportBeforePaths_WhenLocalAppDataIsEmptyOrNull_Throws()
        {
            // Arrange
            var testSubject1 = new MsBuildPathSettings(
                (x, y) => x == Environment.SpecialFolder.LocalApplicationData ? null : "foo", () => true);
            var testSubject2 = new MsBuildPathSettings(
                (x, y) => x == Environment.SpecialFolder.LocalApplicationData ? "" : "foo", () => true);

            // Act
            Action action1 = () => testSubject1.GetImportBeforePaths().ToList();
            Action action2 = () => testSubject2.GetImportBeforePaths().ToList();

            // Assert
            action1.Should().ThrowExactly<IOException>().WithMessage("The local application data folder doesn't exist and it was not possible to create it.");
            action2.Should().ThrowExactly<IOException>().WithMessage("The local application data folder doesn't exist and it was not possible to create it.");
        }

        [TestMethod]
        public void GetImportBeforePaths_WhenLocalAppDataIsSetAndOsIsWindows_ReturnsExceptedPaths()
        {
            // Arrange
            var testSubject = new MsBuildPathSettings(
                (x, y) => x == Environment.SpecialFolder.LocalApplicationData ? "C:\\AppDataPath" : "", () => true);

            // Act
            var result = testSubject.GetImportBeforePaths().ToList();

            // Assert
            result.Should().HaveCount(6);
            result.Should().ContainInOrder(
                "C:\\AppDataPath\\Microsoft\\MSBuild\\4.0\\Microsoft.Common.targets\\ImportBefore",
                "C:\\AppDataPath\\Microsoft\\MSBuild\\10.0\\Microsoft.Common.targets\\ImportBefore",
                "C:\\AppDataPath\\Microsoft\\MSBuild\\11.0\\Microsoft.Common.targets\\ImportBefore",
                "C:\\AppDataPath\\Microsoft\\MSBuild\\12.0\\Microsoft.Common.targets\\ImportBefore",
                "C:\\AppDataPath\\Microsoft\\MSBuild\\14.0\\Microsoft.Common.targets\\ImportBefore",
                "C:\\AppDataPath\\Microsoft\\MSBuild\\15.0\\Microsoft.Common.targets\\ImportBefore");
        }

        [TestMethod]
        public void GetImportBeforePaths_WhenLocalAppDataIsSetAndOsNotWindowsAndUserProfileNotEmpty_ReturnsExceptedPaths()
        {
            // Arrange
            var testSubject = new MsBuildPathSettings(GetSpecialFolder, () => false);

            // Act
            var result = testSubject.GetImportBeforePaths().ToList();

            // Assert
            result.Should().HaveCount(7);
            result.Should().ContainInOrder(
                "C:\\AppDataPath\\Microsoft\\MSBuild\\4.0\\Microsoft.Common.targets\\ImportBefore",
                "C:\\AppDataPath\\Microsoft\\MSBuild\\10.0\\Microsoft.Common.targets\\ImportBefore",
                "C:\\AppDataPath\\Microsoft\\MSBuild\\11.0\\Microsoft.Common.targets\\ImportBefore",
                "C:\\AppDataPath\\Microsoft\\MSBuild\\12.0\\Microsoft.Common.targets\\ImportBefore",
                "C:\\AppDataPath\\Microsoft\\MSBuild\\14.0\\Microsoft.Common.targets\\ImportBefore",
                "C:\\AppDataPath\\Microsoft\\MSBuild\\15.0\\Microsoft.Common.targets\\ImportBefore",
                "C:\\UserProfilePath\\Microsoft\\MSBuild\\15.0\\Microsoft.Common.targets\\ImportBefore");

            string GetSpecialFolder(Environment.SpecialFolder folder, bool forceCreate)
            {
                if (folder == Environment.SpecialFolder.LocalApplicationData)
                {
                    return "C:\\AppDataPath";
                }
                else if (folder == Environment.SpecialFolder.UserProfile)
                {
                    return "C:\\UserProfilePath";
                }
                else
                {
                    return "";
                }
            }
        }

        [TestMethod]
        public void GetImportBeforePaths_WhenLocalAppDataIsSetAndOsNotWindowsAndUserProfileIsEmpty_Throws()
        {
            // Arrange
            var testSubject = new MsBuildPathSettings(GetSpecialFolder, () => false);

            // Act
            Func<IEnumerable<string>> func = () => testSubject.GetImportBeforePaths().ToList();

            // Assert
            func.Should().ThrowExactly<IOException>().WithMessage("The user profile folder doesn't exist and it was not possible to create it.");

            string GetSpecialFolder(Environment.SpecialFolder folder, bool forceCreate)
            {
                if (folder == Environment.SpecialFolder.LocalApplicationData)
                {
                    return "C:\\AppDataPath";
                }
                else
                {
                    return "";
                }
            }
        }

        [TestMethod]
        public void GetGlobalTargetsPaths_WhenProgramFilesIsEmptyOrNull_Throws()
        {
            // Arrange
            var testSubject1 = new MsBuildPathSettings((x, y) => x == Environment.SpecialFolder.ProgramFiles ? null : "foo", () => true);
            var testSubject2 = new MsBuildPathSettings((x, y) => x == Environment.SpecialFolder.ProgramFiles ? "" : "foo", () => true);

            // Act
            Func<IEnumerable<string>> action1 = () => testSubject1.GetGlobalTargetsPaths().ToList();
            Func<IEnumerable<string>> action2 = () => testSubject2.GetGlobalTargetsPaths().ToList();

            // Assert
            action1.Should().ThrowExactly<IOException>().WithMessage("The program files folder doesn't exist and it was not possible to create it.");
            action2.Should().ThrowExactly<IOException>().WithMessage("The program files folder doesn't exist and it was not possible to create it.");
        }

        [TestMethod]
        public void GetGlobalTargetsPaths_WhenProgramFilesNotEmpty_ReturnsExpectedPaths()
        {
            // Arrange
            var testSubject = new MsBuildPathSettings((x, y) => x == Environment.SpecialFolder.ProgramFiles ? "C:\\Program" : "", () => true);

            // Act
            var result = testSubject.GetGlobalTargetsPaths().ToList();

            // Assert
            result.Should().HaveCount(1);
            result.Should().ContainInOrder(
                "C:\\Program\\MSBuild\\14.0\\Microsoft.Common.Targets\\ImportBefore");
        }
    }
}
