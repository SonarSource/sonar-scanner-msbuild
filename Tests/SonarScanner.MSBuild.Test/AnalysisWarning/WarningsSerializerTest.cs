/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.AnalysisWarning;

namespace SonarScanner.MSBuild.Test.AnalysisWarning
{
    [TestClass]
    public class WarningsSerializerTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void SerializeToFile_InvalidWarningsCollections_Throws() =>
            ((Action)(() => WarningsSerializer.Serialize(null, "filePath"))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("warnings");

        [TestMethod]
        public void SerializeToFile_InvalidFileDirectory_Throws() =>
            ((Action)(() => WarningsSerializer.Serialize(Array.Empty<Warning>(), null))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("filePath");

        [TestMethod]
        public void SerializeToFile_EmptyFileDirectory_Throws() =>
            ((Action)(() => WarningsSerializer.Serialize(Array.Empty<Warning>(), string.Empty))).Should().Throw<ArgumentException>().WithMessage("Empty path name is not legal.");

        [TestMethod]
        public void SerializeToFile_EmptyWarningsCollections_FileCreatedWithNoContent()
        {
            var filePath = Path.Combine(TestContext.TestDir, "test.json");
            WarningsSerializer.Serialize(Array.Empty<Warning>(), filePath);
            File.Exists(filePath).Should().Be(true);
            File.ReadAllText(filePath).Should().Be("[]");
        }

        [TestMethod]
        public void SerializeToFile_NonExistentPath_Throws()
        {
            var analysisWarnings = new[] { new Warning("A message") };
            var filePath = Path.Combine(TestContext.TestDir, "NonexistentDirectory", "test.json");
            ((Action)(() => WarningsSerializer.Serialize(analysisWarnings, filePath))).Should().Throw<DirectoryNotFoundException>();
        }

        [TestMethod]
        public void SerializeToFile_JsonIsSerializedCorrectly()
        {
            var analysisWarnings = new[] { new Warning("A message"), new Warning("A second message") };
            var expected = @"[
  {
    ""text"": ""A message""
  },
  {
    ""text"": ""A second message""
  }
]";

            var filePath = Path.Combine(TestContext.TestDir, "test.json");
            WarningsSerializer.Serialize(analysisWarnings, filePath);
            File.ReadAllText(filePath).Should().Be(expected);
        }
    }
}
