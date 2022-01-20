/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2021 SonarSource SA
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

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Tasks.NetFramework.UnitTest
{
    [TestClass]
    public class WriteProjectInfoFileTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void GetProjectGuid_InvalidPath_DoesNotThrow()
        {
            var expectedGuid = "{10F2915F-4AB3-4269-BC2B-4F72C6DE87C8}";
            var notExpectedGuid = "{10F2915F-4AB3-4269-BC2B-4F72C6DE87C8}";
            var notValidPath = @"D:\a\1\s\src\https://dnndev.me:44305";
            var fullProjectPath = @"C:\NetStdApp\NetStdApp.csproj";
            var testSubject = new WriteProjectInfoFile
            {
                ProjectGuid = null,
                FullProjectPath = fullProjectPath,
                SolutionConfigurationContents = @"<SolutionConfiguration>
<ProjectConfiguration Project=""" + notExpectedGuid + @""" AbsolutePath=""" + notValidPath + @""" BuildProjectInSolution=""True"">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project=""" + expectedGuid + @""" AbsolutePath=""" + fullProjectPath + @""" BuildProjectInSolution=""True"">Debug|AnyCPU</ProjectConfiguration>
</SolutionConfiguration>"
            };

            // Act
            var actual = testSubject.GetProjectGuid();

            // Assert
            actual.Should().Be(expectedGuid);
        }
    }
}
