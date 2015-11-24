//-----------------------------------------------------------------------
// <copyright file="RoslynV1SarifFixerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class RoslynV1SarifFixerTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void SarifCompilerVersionCheck_IsFromRoslynV1()
        {
            // Arrange
            string testSarif = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.0.0"",
    ""fileVersion"": ""1.0.0""
  }
        }";

            // Act
            bool valid = RoslynV1SarifFixer.IsSarifFromRoslynV1(testSarif);

            // Assert
            Assert.IsTrue(valid, "Expecting the compiler version check to return true for 'is Roslyn 1.0'");
        }

        [TestMethod]
        public void SarifCompilerVersionCheck_IsNotFromRoslynV1()
        {
            // Arrange
            string testSarif = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Microsoft (R) Visual C# Compiler"",
    ""productVersion"": ""1.1.0"",
    ""fileVersion"": ""1.0.0""
  }
        }";

            // Act
            bool valid = RoslynV1SarifFixer.IsSarifFromRoslynV1(testSarif);

            // Assert
            Assert.IsFalse(valid, "Expecting the compiler version check to return false for 'not Roslyn 1.0'");
        }

        /// <summary>
        /// Tests whether the compiler version check can handle improper escaping, as the JSON parse will throw an exception.
        /// </summary>
        [TestMethod]
        public void SarifCompilerVersionCheck_HasImproperEscaping()
        {
            // Arrange
            string testSarif = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""Foo\Bar"",
    ""productVersion"": ""1.1.0"",
    ""fileVersion"": ""1.0.0""
  }
        }";

            // Act
            bool valid = RoslynV1SarifFixer.IsSarifFromRoslynV1(testSarif);

            // Assert
            Assert.IsFalse(valid, "Expecting the compiler version check to return false for 'not Roslyn 1.0'");
        }

        [TestMethod]
        public void SarifCompilerVersionCheck_FailureNoToolInfo()
        {
            // Arrange
            string testSarif = @"{ }";

            // Act
            bool valid = RoslynV1SarifFixer.IsSarifFromRoslynV1(testSarif);

            // Assert
            Assert.IsFalse(valid, "Expecting the compiler version check to return false for 'not Roslyn 1.0'");
        }

        [TestMethod]
        public void SarifCompilerVersionCheck_FailureInvalidToolInfo()
        {
            // Arrange
            string testSarif = @"{
  ""version"": ""0.1"",
  ""toolInfo"": {
                ""toolName"": ""foo""
  }
        }";

            // Act
            bool valid = RoslynV1SarifFixer.IsSarifFromRoslynV1(testSarif);

            // Assert
            Assert.IsFalse(valid, "Expecting the compiler version check to return false for 'not Roslyn 1.0'");
        }

        /// <summary>
        /// There should be no change to input if it is already valid, as attempting to fix valid SARIF may cause over-escaping.
        /// This should be the case even if the output came from VS 2015 RTM.
        /// </summary>
        [TestMethod]
        public void SarifFixer_ShouldNotChange_Valid()
        {
            // Arrange
            string testSarif = @"{
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

            // Act
            string fixedSarif;
            bool returnStringIsValid = RoslynV1SarifFixer.FixRoslynV1Sarif(testSarif, out fixedSarif);

            // Assert
            Assert.IsTrue(returnStringIsValid);
            Assert.AreEqual(testSarif, fixedSarif);
        }

        [TestMethod]
        public void SarifFixer_ShouldNotChange_Unfixable()
        {
            // Arrange
            string testSarif = @"{
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

            // Act
            string fixedSarif;
            bool returnStringIsValid = RoslynV1SarifFixer.FixRoslynV1Sarif(testSarif, out fixedSarif);

            // Assert
            Assert.IsFalse(returnStringIsValid);
            Assert.AreEqual(testSarif, fixedSarif);
        }

        /// <summary>
        /// The current solution cannot fix values spanning multiple fields. As such it should not attempt to.
        /// 
        /// Example invalid:
        /// "fullMessage": "message 
        /// \test\ ["_"]",
        /// </summary>
        [TestMethod]
        public void SarifFixer_ShouldNotChange_SpansMultipleLines()
        {
            // Arrange
            string testSarif = @"{
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

            // Act
            string fixedSarif;
            bool returnStringIsValid = RoslynV1SarifFixer.FixRoslynV1Sarif(testSarif, out fixedSarif);

            // Assert
            Assert.IsFalse(returnStringIsValid);
            Assert.AreEqual(testSarif, fixedSarif);
        }

        [TestMethod]
        public void SarifFixer_ShouldChange_EscapeBackslashes()
        {
            // Arrange
            string testSarif = @"{
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

            // Act
            string fixedSarif;
            bool returnStringIsValid = RoslynV1SarifFixer.FixRoslynV1Sarif(testSarif, out fixedSarif);

            // Assert
            Assert.IsTrue(returnStringIsValid);
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
}", fixedSarif);
        }

        [TestMethod]
        public void SarifFixer_ShouldChange_EscapeQuotes()
        {
            // Arrange
            string testSarif = @"{
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

            // Act
            string fixedSarif;
            bool changeApplied = RoslynV1SarifFixer.FixRoslynV1Sarif(testSarif, out fixedSarif);

            // Assert
            Assert.IsTrue(changeApplied);
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
}", fixedSarif);
        }

        [TestMethod]
        public void SarifFixer_ShouldChange_EscapeCharsInAllAffectedFields()
        {
            // Arrange
            string testSarif = @"{
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

            // Act
            string fixedSarif;
            bool changeApplied = RoslynV1SarifFixer.FixRoslynV1Sarif(testSarif, out fixedSarif);

            // Assert
            Assert.IsTrue(changeApplied);
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
}", fixedSarif);
        }
        
        [TestMethod]
        public void IsJsonValid_True()
        {
            // Arrange
            string testSarif = @"{
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

            // Act
            bool isValid = RoslynV1SarifFixer.IsValidJson(testSarif);

            // Assert
            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public void IsJsonValid_FalseHasUnescapedQuotes()
        {
            // Arrange
            string testSarif = @"{
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
      ""shortMessage"": ""Test shortMessage. It features ""quoted text""."",
      ""properties"": {
        ""severity"": ""Info"",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}";

            // Act
            bool isValid = RoslynV1SarifFixer.IsValidJson(testSarif);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void IsJsonValid_FalseHasUnescapedSlashes()
        {
            // Arrange
            string testSarif = @"{
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
      ""shortMessage"": ""Test shortMessage. It features \""quoted text\""."",
      ""properties"": {
        ""severity"": ""Info"",
        ""helpLink"": ""https://github.com/SonarSource/sonar-msbuild-runner"",
      }
    }
  ]
}";

            // Act
            bool isValid = RoslynV1SarifFixer.IsValidJson(testSarif);

            // Assert
            Assert.IsFalse(isValid);
        }

        #endregion


    }
}
