/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
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

using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Tasks;
using SonarScanner.MSBuild.Tasks.UnitTests;
using TestUtilities;

namespace SonarScanner.Integration.Tasks.UnitTests
{
    [TestClass]
    public class MoveDirectoryTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void MoveDirectory_InvalidSourceDirectoryPath_ReturnsFalse(string sourceDirectory)
        {
            // Arrange
            var sut = new MoveDirectory();
            var dummyEngine = new DummyBuildEngine();
            sut.BuildEngine = dummyEngine;
            sut.SourceDirectory = sourceDirectory;
            sut.DestinationDirectory = @"C:\SomeRandomPath\";

            // Act & Assert
            sut.Execute().Should().BeFalse();
            dummyEngine.AssertSingleErrorExists("The source directory is invalid.");
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void MoveDirectory_InvalidDestinationDirectoryPath_ReturnsFalse(string destinationDirectory)
        {
            // Arrange
            var sut = new MoveDirectory();
            var dummyEngine = new DummyBuildEngine();
            sut.BuildEngine = dummyEngine;
            sut.SourceDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            sut.DestinationDirectory = destinationDirectory;

            // Act & Assert
            sut.Execute().Should().BeFalse();
            dummyEngine.AssertSingleErrorExists("The destination directory is invalid.");
        }

        [TestMethod]
        public void MoveDirectory_ValidPaths_DirectoryMoved()
        {
            // Arrange
            var sut = new MoveDirectory();
            var root = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            var sourceDirectory = Path.Combine(root, "Source");
            var destinationDirectory = Path.Combine(root, "Destination");
            Directory.CreateDirectory(sourceDirectory);
            Directory.CreateDirectory(Path.Combine(sourceDirectory, "SubDirectory"));
            File.WriteAllText(Path.Combine(sourceDirectory, "RandomFile1.txt"), string.Empty);
            File.WriteAllText(Path.Combine(sourceDirectory, @".\SubDirectory\RandomFile2.txt"), string.Empty);
            var dummyEngine = new DummyBuildEngine();
            sut.DestinationDirectory = destinationDirectory;
            sut.SourceDirectory = sourceDirectory;
            sut.BuildEngine = dummyEngine;

            // Act & Assert
            sut.Execute().Should().BeTrue();
            dummyEngine.AssertNoErrors();
            File.Exists(Path.Combine(destinationDirectory, "RandomFile1.txt")).Should().BeTrue();
            File.Exists(Path.Combine(destinationDirectory, @".\SubDirectory\RandomFile2.txt")).Should().BeTrue();
        }
    }
}
