//-----------------------------------------------------------------------
// <copyright file="RoslynV1SarifFixerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarRunner.Shim;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.Shim.Tests
{
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
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
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
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, logger);

            // Assert
            // already valid -> no change to file, same file path returned
            AssertFileUnchanged(testSarifPath, originalWriteTime);
            Assert.AreEqual(testSarifPath, returnedSarifPath);
        }

        [TestMethod]
        public void SarifFixer_ShouldNotChange_Unfixable()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
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
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, logger);

            // Assert
            // unfixable -> no change to file, null return
            AssertFileUnchanged(testSarifPath, originalWriteTime);
            Assert.IsNull(returnedSarifPath);
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
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
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
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, logger);

            // Assert
            // unfixable -> no change to file, null return
            AssertFileUnchanged(testSarifPath, originalWriteTime);
            Assert.IsNull(returnedSarifPath);
        }

        [TestMethod]
        public void SarifFixer_ShouldChange_EscapeBackslashes()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
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
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, logger);

            // Assert
            // fixable -> no change to file, file path in return value, file contents as expected
            AssertFileUnchanged(testSarifPath, originalWriteTime);
            Assert.IsNotNull(returnedSarifPath);

            string returnedSarifString = File.ReadAllText(returnedSarifPath);
            Assert.AreEqual(@"{
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
}", returnedSarifString);
        }

        [TestMethod]
        public void SarifFixer_ShouldChange_EscapeQuotes()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
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
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, logger);

            // Assert
            // fixable -> no change to file, file path in return value, file contents as expected
            AssertFileUnchanged(testSarifPath, originalWriteTime);
            Assert.IsNotNull(returnedSarifPath);

            string returnedSarifString = File.ReadAllText(returnedSarifPath);
            Assert.AreEqual(@"{
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
}", returnedSarifString);
        }

        [TestMethod]
        public void SarifFixer_ShouldChange_EscapeCharsInAllAffectedFields()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string testSarifString = @"{
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
            string testSarifPath = Path.Combine(testDir, "testSarif.json");
            File.WriteAllText(testSarifPath, testSarifString);
            DateTime originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

            // Act
            string returnedSarifPath = new RoslynV1SarifFixer().LoadAndFixFile(testSarifPath, logger);

            // Assert
            // fixable -> no change to file, file path in return value, file contents as expected
            AssertFileUnchanged(testSarifPath, originalWriteTime);
            Assert.IsNotNull(returnedSarifPath);

            string returnedSarifString = File.ReadAllText(returnedSarifPath);
            Assert.AreEqual(@"{
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
}", returnedSarifString);
        }

        #endregion

        #region Private Methods

        private void AssertFileUnchanged(string filePath, DateTime originalWriteTime)
        {
            Assert.AreEqual(originalWriteTime, new FileInfo(filePath).LastWriteTime);
        }

        #endregion
    }
}
