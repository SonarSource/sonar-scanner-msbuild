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
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Shim;
using TestUtilities;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class RoslynV1SarifFixerTests
{
    public TestContext TestContext { get; set; }

    #region Tests

    /// <summary>
    /// There should be no change to input if it is already valid, as attempting to fix valid SARIF may cause over-escaping.
    /// This should be the case even if the output came from VS 2015 RTM.
    /// </summary>
    [TestMethod]
    public void SarifFixer_ShouldNotChange_Valid()
    {
        // Arrange
        var logger = new TestLogger();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var testSarifString = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""locations"": [
        {
          ""analysisTarget"": [
            {
              ""uri"": ""C:\\agent\\_work\\2\\s\\MyTestProj\\Program.cs"",
}
          ]
        }
      ],
      ""shortMessage"": ""Test shortMessage. It features \""quoted text\""."",
      ""properties"": {
        ""severity"": ""Info"",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}";
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, testSarifString);
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        // Act
        var returnedSarifPath = new RoslynV1SarifFixer(logger).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage);

        // Assert
        // already valid -> no change to file, same file path returned
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().Be(testSarifPath);
    }

    [TestMethod]
    public void SarifFixer_ShouldNotChange_Unfixable()
    {
        // Arrange
        var logger = new TestLogger();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var testSarifString = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
}}}}}}}}}}}}}}}}}}}}}}}}}

      ""ruleId"": ""DD001"",
      ""locations"": [
        {
          ""analysisTarget"": [
            {
              ""uri"": ""C:\\agent\\_work\\2\\s\\MyTestProj\\Program.cs"",
}
          ]
        }
      ],
      ""shortMessage"": ""Test shortMessage. It features \""quoted text\""."",
      ""properties"": {
        ""severity"": ""Info"",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}";
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, testSarifString);
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        // Act
        var returnedSarifPath = new RoslynV1SarifFixer(logger).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage);

        // Assert
        // unfixable -> no change to file, null return
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().BeNull();
    }

    /// <summary>
    /// The current solution cannot fix values spanning multiple fields. As such it should not attempt to.
    ///
    /// Example invalid:
    /// "fullMessage": "message
    /// \test\ ["_"]",
    /// </summary>
    [TestMethod]
    public void SarifFixer_ShouldNotChange_MultipleLineValues()
    {
        // Arrange
        var logger = new TestLogger();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var testSarifString = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""shortMessage"": ""Test shortMessage.
It features ""quoted text""."",
      ""properties"": {
        ""severity"": ""Info"",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}";
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, testSarifString);
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        // Act
        var returnedSarifPath = new RoslynV1SarifFixer(logger).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage);

        // Assert
        // unfixable -> no change to file, null return
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().BeNull();
    }

    [TestMethod]
    public void SarifFixer_ShouldChange_EscapeBackslashes()
    {
        // Arrange
        var logger = new TestLogger();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var testSarifString = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""locations"": [
        {
          ""analysisTarget"": [
            {
              ""uri"": ""C:\agent\_work\2\s\MyTestProj\Program.cs"",
}
          ]
        }
      ],
    }
  ]
}";
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, testSarifString);
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        // Act
        var returnedSarifPath = new RoslynV1SarifFixer(logger).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage);

        // Assert
        // fixable -> no change to file, file path in return value, file contents as expected
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().NotBeNull();

        var returnedSarifString = File.ReadAllText(returnedSarifPath);
        returnedSarifString.Should().Be(@"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""locations"": [
        {
          ""analysisTarget"": [
            {
              ""uri"": ""C:\\agent\\_work\\2\\s\\MyTestProj\\Program.cs"",
}
          ]
        }
      ],
    }
  ]
}");
    }

    [TestMethod]
    public void SarifFixer_ShouldChange_EscapeQuotes()
    {
        // Arrange
        var logger = new TestLogger();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var testSarifString = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""shortMessage"": ""Test shortMessage. It features ""quoted text""."",
      ""properties"": {
        ""severity"": ""Info"",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}";
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, testSarifString);
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        // Act
        var returnedSarifPath = new RoslynV1SarifFixer(logger).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage);

        // Assert
        // fixable -> no change to file, file path in return value, file contents as expected
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().NotBeNull();

        var returnedSarifString = File.ReadAllText(returnedSarifPath);
        returnedSarifString.Should().Be(@"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""shortMessage"": ""Test shortMessage. It features \""quoted text\""."",
      ""properties"": {
        ""severity"": ""Info"",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}");
    }

    [TestMethod]
    public void SarifFixer_ShouldChange_EscapeCharsInAllAffectedFields()
    {
        // Arrange
        var logger = new TestLogger();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var testSarifString = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""locations"": [
        {
          ""analysisTarget"": [
            {
              ""uri"": ""C:\agent\_work\2\s\MyTestProj\Program.cs"",
}
          ]
        }
      ],
      ""shortMessage"": ""Test shortMessage. It features ""quoted text"" and has \slashes."",
      ""fullMessage"": ""Test fullMessage. It features ""quoted text"" and has \slashes."",
      ""properties"": {
        ""severity"": ""Info"",
        ""title"": ""Test title. It features ""quoted text"" and has \slashes."",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}";
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, testSarifString);
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        // Act
        var returnedSarifPath = new RoslynV1SarifFixer(logger).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage);

        // Assert
        // fixable -> no change to file, file path in return value, file contents as expected
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().NotBeNull();

        var returnedSarifString = File.ReadAllText(returnedSarifPath);
        returnedSarifString.Should().Be(@"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""locations"": [
        {
          ""analysisTarget"": [
            {
              ""uri"": ""C:\\agent\\_work\\2\\s\\MyTestProj\\Program.cs"",
}
          ]
        }
      ],
      ""shortMessage"": ""Test shortMessage. It features \""quoted text\"" and has \\slashes."",
      ""fullMessage"": ""Test fullMessage. It features \""quoted text\"" and has \\slashes."",
      ""properties"": {
        ""severity"": ""Info"",
        ""title"": ""Test title. It features \""quoted text\"" and has \\slashes."",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}");
    }

    [TestMethod]
    public void SarifFixer_VBNet()
    {
        // Arrange
        var logger = new TestLogger();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var testSarifString = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual Basic Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""locations"": [
        {
          ""analysisTarget"": [
            {
              ""uri"": ""C:\agent\_work\2\s\MyTestProj\Program.cs"",
}
          ]
        }
      ],
    }
  ]
}";
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, testSarifString);
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        // Act
        var returnedSarifPath = new RoslynV1SarifFixer(logger).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.VBNetLanguage);

        // Assert
        // fixable -> no change to file, file path in return value, file contents as expected
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().NotBeNull();

        var returnedSarifString = File.ReadAllText(returnedSarifPath);
        returnedSarifString.Should().Be(@"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual Basic Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""locations"": [
        {
          ""analysisTarget"": [
            {
              ""uri"": ""C:\\agent\\_work\\2\\s\\MyTestProj\\Program.cs"",
}
          ]
        }
      ],
    }
  ]
}");
    }

    /// <summary>
    /// To avoid FPs, the tool name declared in the file is compared with the language. If it doesn't match, do nothing.
    /// </summary>
    [TestMethod]
    public void SarifFixer_ShouldNotFixInvalid()
    {
        // Arrange
        var logger = new TestLogger();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var testSarifString = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  },
  ""issues"": [
    {
      ""ruleId"": ""DD001"",
      ""locations"": [
        {
          ""analysisTarget"": [
            {
              ""uri"": ""C:\agent\_work\2\s\MyTestProj\Program.cs"",
}
          ]
        }
      ],
    }
  ]
}";
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, testSarifString);

        // Act
        var returnedSarifPath = new RoslynV1SarifFixer(logger).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.VBNetLanguage);
        returnedSarifPath.Should().BeNull();
    }

    #endregion Tests

    #region Private Methods

    private void AssertFileUnchanged(string filePath, DateTime originalWriteTime)
    {
        new FileInfo(filePath).LastWriteTime.Should().Be(originalWriteTime);
    }

    #endregion Private Methods
}
