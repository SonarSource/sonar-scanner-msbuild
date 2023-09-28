/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
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
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class JavaFilePropertyVersionTests
    {
        [TestMethod]
        public async Task GetVersionAsync_FileExist_ShouldReturnJavaVersion17()
        {
            // TODO: We should not rely on installed Java and provide a way to "mock"
            // Idea: Create a small program that print the expected version for the test
            var sut = new JavaFilePropertyVersion(new TestLogger());

            var result = await sut.GetVersionAsync();

            result.Should().NotBeNull();
            result.Major.Should().Be(17);
        }

        [TestMethod]
        public async Task GetVersionAsync_FileNotFound_ShouldReturnNullAndLogWarning()
        {
            using var scope = new EnvironmentVariableScope();
            scope.SetVariable("PATH", string.Empty);
            var logger = new TestLogger();
            var sut = new JavaFilePropertyVersion(logger);

            var result = await sut.GetVersionAsync();

            result.Should().BeNull();
            logger.AssertWarningLogged("Unable to locate 'java'. Please make sure java is installed and in the PATH");
        }

        [TestMethod]
        public async Task GetVersionAsync_UnableToParseVersion_ShouldReturnNullAndLogWarning()
        {
            using var scope = new EnvironmentVariableScope();
            scope.SetVariable("PATH", Path.Combine(Directory.GetCurrentDirectory(), "TestResources"));
            var logger = new TestLogger();
            var sut = new JavaFilePropertyVersion(logger);

            var result = await sut.GetVersionAsync();

            result.Should().BeNull();
            logger.AssertWarningLogged("Unable to get current Java version. Reason: '': Invalid version format");
        }
    }
}
