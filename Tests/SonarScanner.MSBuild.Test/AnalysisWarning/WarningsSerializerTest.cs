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
        public void SerializeToFile_InvalidWarningsCollections_Throws()
        {
            Action act = () => WarningsSerializer.Serialize(null, "filePath");
            act.Should().Throw<ArgumentNullException>().WithMessage(@"Value cannot be null.
Parameter name: warnings");
        }

        [TestMethod]
        public void SerializeToFile_InvalidFileDirectory_Throws()
        {
            Action act = () => WarningsSerializer.Serialize(Array.Empty<Warning>(), null);
            act.Should().Throw<ArgumentException>().WithMessage(@"Parameter filePath cannot be null or empty.
Parameter name: filePath");
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
]".Replace("\n", Environment.NewLine);

            var filePath = Path.Combine(TestContext.TestDir, "test.json");
            WarningsSerializer.Serialize(analysisWarnings, filePath);

            File.ReadAllText(filePath).Should().Be(expected);
        }
    }
}
