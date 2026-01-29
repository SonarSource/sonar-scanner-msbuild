/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class RoslynV1SarifFixerTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void SarifFixer_ConstructorThrows() =>
        FluentActions.Invoking(() => new RoslynV1SarifFixer(null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("runtime");

    [TestMethod]
    public void SarifFixer_FileDoesNotExist_LogsMessage()
    {
        var runtime = new TestRuntime();
        new RoslynV1SarifFixer(runtime).LoadAndFixFile("Some/NonexistantPath", RoslynV1SarifFixer.CSharpLanguage).Should().BeNull();
        runtime.Logger.InfoMessages.Should().ContainSingle().Which.Should().Contain("No Code Analysis ErrorLog file found");
    }

    /// <summary>
    /// There should be no change to input if it is already valid, as attempting to fix valid SARIF may cause over-escaping.
    /// This should be the case even if the output came from VS 2015 RTM.
    /// </summary>
    [TestMethod]
    public void SarifFixer_ShouldNotChange_Valid()
    {
        var runtime = new TestRuntime();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        const string inputSarif = """
            {
              "version": "0.1",
              "toolInfo": {
                "toolName": "Microsoft (R) Visual C# Compiler",
                "productVersion": "1.0.0",
                "fileVersion": "1.0.0"
              },
              "issues": [
                {
                  "ruleId": "DD001",
                  "locations": [
                    {
                      "analysisTarget": [
                        {
                          "uri": "C:\\agent\\_work\\2\\s\\MyTestProj\\Program.cs",
                        }
                      ]
                    }
                  ],
                  "shortMessage": "Test shortMessage. It features \"quoted text\".",
                  "properties": {
                    "severity": "Info",
                    "helpLink": "https://github.com/SonarSource/sonar-msbuild-runner",
                  }
                }
              ]
            }
            """;
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, inputSarif.ToEnvironmentLineEndings());
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;
        var returnedSarifPath = new RoslynV1SarifFixer(runtime).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage);

        // Already valid -> no change to file, same file path returned
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().Be(testSarifPath);
        runtime.Telemetry.Messages.Should().ContainEquivalentOf(
            new KeyValuePair<string, string>("dotnetenterprise.s4net.endstep.Sarif.V0_1_0_0", "True"));
    }

    [TestMethod]
    public void SarifFixer_ShouldNotChange_Unfixable()
    {
        var runtime = new TestRuntime();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        const string sarifInput = """
            {
              "version": "0.1",
              "toolInfo": {
                "toolName": "Microsoft (R) Visual C# Compiler",
                "productVersion": "1.0.0",
                "fileVersion": "1.0.0"
              },
              "issues": [
                {
            }}}}}}}}}}}}}}}}}}}}}}}}}

                  "ruleId": "DD001",
                  "locations": [
                    {
                      "analysisTarget": [
                        {
                          "uri": "C:\\agent\\_work\\2\\s\\MyTestProj\\Program.cs",
                        }
                      ]
                    }
                  ],
                  "shortMessage": "Test shortMessage. It features \"quoted text\".",
                  "properties": {
                    "severity": "Info",
                    "helpLink": "https://github.com/SonarSource/sonar-msbuild-runner",
                  }
                }
              ]
            }
            """;
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, sarifInput);
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;
        var returnedSarifPath = new RoslynV1SarifFixer(runtime).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage);

        // Not fixable -> no change to file, null return
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().BeNull();
        runtime.Telemetry.Messages.Should().BeEmpty();
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
        var runtime = new TestRuntime();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        const string inputSarif = """
            {
              "version": "0.1",
              "toolInfo": {
                "toolName": "Microsoft (R) Visual C# Compiler",
                "productVersion": "1.0.0",
                "fileVersion": "1.0.0"
              },
              "issues": [
                {
                  "ruleId": "DD001",
                  "shortMessage": "Test shortMessage.
            It features "quoted text".",
                  "properties": {
                    "severity": "Info",
                    "helpLink": "https://github.com/SonarSource/sonar-msbuild-runner",
                  }
                }
              ]
            }
            """;
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, inputSarif.ToEnvironmentLineEndings());
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;
        var returnedSarifPath = new RoslynV1SarifFixer(runtime).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage);

        // Not fixable -> no change to file, null return
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().BeNull();
        runtime.Telemetry.Messages.Should().BeEmpty();
    }

    [TestMethod]
    public void SarifFixer_ShouldChange_EscapeBackslashes()
    {
        var runtime = new TestRuntime();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        const string inputSarif = """
            {
              "version": "0.1",
              "toolInfo": {
                "toolName": "Microsoft (R) Visual C# Compiler",
                "productVersion": "1.0.0",
                "fileVersion": "1.0.0"
              },
              "issues": [
                {
                  "ruleId": "DD001",
                  "locations": [
                    {
                      "analysisTarget": [
                        {
                          "uri": "C:\agent\_work\2\s\MyTestProj\Program.cs",
                        }
                      ]
                    }
                  ],
                }
              ]
            }
            """;
        const string expectedSarif = """
            {
              "version": "0.1",
              "toolInfo": {
                "toolName": "Microsoft (R) Visual C# Compiler",
                "productVersion": "1.0.0",
                "fileVersion": "1.0.0"
              },
              "issues": [
                {
                  "ruleId": "DD001",
                  "locations": [
                    {
                      "analysisTarget": [
                        {
                          "uri": "C:\\agent\\_work\\2\\s\\MyTestProj\\Program.cs",
                        }
                      ]
                    }
                  ],
                }
              ]
            }
            """;
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, inputSarif.ToEnvironmentLineEndings());
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;
        var returnedSarifPath = new RoslynV1SarifFixer(runtime).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage);

        // Fixable -> no change to file, file path in return value, file contents as expected
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().NotBeNull();
        File.ReadAllText(returnedSarifPath).Should().Be(expectedSarif.ToEnvironmentLineEndings());
        runtime.Telemetry.Messages.Should().ContainEquivalentOf(
            new KeyValuePair<string, string>("dotnetenterprise.s4net.endstep.Sarif.V0_1_0_0", "True"));
    }

    [TestMethod]
    public void SarifFixer_ShouldChange_EscapeQuotes()
    {
        var runtime = new TestRuntime();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        const string inputSarif = """
            {
              "version": "0.1",
              "toolInfo": {
                "toolName": "Microsoft (R) Visual C# Compiler",
                "productVersion": "1.0.0",
                "fileVersion": "1.0.0"
              },
              "issues": [
                {
                  "ruleId": "DD001",
                  "shortMessage": "Test shortMessage. It features "quoted text".",
                  "properties": {
                    "severity": "Info",
                    "helpLink": "https://github.com/SonarSource/sonar-msbuild-runner",
                  }
                }
              ]
            }
            """;
        const string expectedSarif = """
            {
              "version": "0.1",
              "toolInfo": {
                "toolName": "Microsoft (R) Visual C# Compiler",
                "productVersion": "1.0.0",
                "fileVersion": "1.0.0"
              },
              "issues": [
                {
                  "ruleId": "DD001",
                  "shortMessage": "Test shortMessage. It features \"quoted text\".",
                  "properties": {
                    "severity": "Info",
                    "helpLink": "https://github.com/SonarSource/sonar-msbuild-runner",
                  }
                }
              ]
            }
            """;
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, inputSarif.ToEnvironmentLineEndings());
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;
        var returnedSarifPath = new RoslynV1SarifFixer(runtime).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage);

        // Fixable -> no change to file, file path in return value, file contents as expected
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().NotBeNull();
        File.ReadAllText(returnedSarifPath).Should().Be(expectedSarif.ToEnvironmentLineEndings());
        runtime.Telemetry.Messages.Should().ContainEquivalentOf(
            new KeyValuePair<string, string>("dotnetenterprise.s4net.endstep.Sarif.V0_1_0_0", "True"));
    }

    [TestMethod]
    public void SarifFixer_ShouldChange_EscapeCharsInAllAffectedFields()
    {
        var runtime = new TestRuntime();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        const string inputSarif = """
            {
              "version": "0.1",
              "toolInfo": {
                "toolName": "Microsoft (R) Visual C# Compiler",
                "productVersion": "1.0.0",
                "fileVersion": "1.0.0"
              },
              "issues": [
                {
                  "ruleId": "DD001",
                  "locations": [
                    {
                      "analysisTarget": [
                        {
                          "uri": "C:\agent\_work\2\s\MyTestProj\Program.cs",
                        }
                      ]
                    }
                  ],
                  "shortMessage": "Test shortMessage. It features "quoted text" and has \slashes.",
                  "fullMessage": "Test fullMessage. It features "quoted text" and has \slashes.",
                  "properties": {
                    "severity": "Info",
                    "title": "Test title. It features "quoted text" and has \slashes.",
                    "helpLink": "https://github.com/SonarSource/sonar-msbuild-runner",
                  }
                }
              ]
            }
            """;
        const string expectedSarif = """
            {
              "version": "0.1",
              "toolInfo": {
                "toolName": "Microsoft (R) Visual C# Compiler",
                "productVersion": "1.0.0",
                "fileVersion": "1.0.0"
              },
              "issues": [
                {
                  "ruleId": "DD001",
                  "locations": [
                    {
                      "analysisTarget": [
                        {
                          "uri": "C:\\agent\\_work\\2\\s\\MyTestProj\\Program.cs",
                        }
                      ]
                    }
                  ],
                  "shortMessage": "Test shortMessage. It features \"quoted text\" and has \\slashes.",
                  "fullMessage": "Test fullMessage. It features \"quoted text\" and has \\slashes.",
                  "properties": {
                    "severity": "Info",
                    "title": "Test title. It features \"quoted text\" and has \\slashes.",
                    "helpLink": "https://github.com/SonarSource/sonar-msbuild-runner",
                  }
                }
              ]
            }
            """;
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, inputSarif.ToEnvironmentLineEndings());
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;
        var returnedSarifPath = new RoslynV1SarifFixer(runtime).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.CSharpLanguage);

        // Fixable -> no change to file, file path in return value, file contents as expected
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().NotBeNull();
        File.ReadAllText(returnedSarifPath).Should().Be(expectedSarif.ToEnvironmentLineEndings());
        runtime.Telemetry.Messages.Should().ContainEquivalentOf(
            new KeyValuePair<string, string>("dotnetenterprise.s4net.endstep.Sarif.V0_1_0_0", "True"));
    }

    [TestMethod]
    public void SarifFixer_VBNet()
    {
        var runtime = new TestRuntime();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        const string inputSarif = """
            {
              "version": "0.1",
              "toolInfo": {
                "toolName": "Microsoft (R) Visual Basic Compiler",
                "productVersion": "1.0.0",
                "fileVersion": "1.0.0"
              },
              "issues": [
                {
                  "ruleId": "DD001",
                  "locations": [
                    {
                      "analysisTarget": [
                        {
                          "uri": "C:\agent\_work\2\s\MyTestProj\Program.cs",
                        }
                      ]
                    }
                  ],
                }
              ]
            }
            """;
        const string expectedSarif = """
            {
              "version": "0.1",
              "toolInfo": {
                "toolName": "Microsoft (R) Visual Basic Compiler",
                "productVersion": "1.0.0",
                "fileVersion": "1.0.0"
              },
              "issues": [
                {
                  "ruleId": "DD001",
                  "locations": [
                    {
                      "analysisTarget": [
                        {
                          "uri": "C:\\agent\\_work\\2\\s\\MyTestProj\\Program.cs",
                        }
                      ]
                    }
                  ],
                }
              ]
            }
            """;
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, inputSarif.ToEnvironmentLineEndings());
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;
        var returnedSarifPath = new RoslynV1SarifFixer(runtime).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.VBNetLanguage);

        // Fixable -> no change to file, file path in return value, file contents as expected
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().NotBeNull();
        File.ReadAllText(returnedSarifPath).Should().Be(expectedSarif.ToEnvironmentLineEndings());
        runtime.Telemetry.Messages.Should().ContainEquivalentOf(
            new KeyValuePair<string, string>("dotnetenterprise.s4net.endstep.Sarif.V0_1_0_0", "True"));
    }

    /// <summary>
    /// To avoid FPs, the tool name declared in the file is compared with the language. If it doesn't match, do nothing.
    /// </summary>
    [TestMethod]
    public void SarifFixer_ShouldNotFixInvalid()
    {
        var runtime = new TestRuntime();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        const string inputSarif = """
            {
              "version": "0.1",
              "toolInfo": {
                "toolName": "Microsoft (R) Visual C# Compiler",
                "productVersion": "1.0.0",
                "fileVersion": "1.0.0"
              },
              "issues": [
                {
                  "ruleId": "DD001",
                  "locations": [
                    {
                      "analysisTarget": [
                        {
                          "uri": "C:\agent\_work\2\s\MyTestProj\Program.cs",
                        }
                      ]
                    }
                  ],
                }
              ]
            }
            """;
        var testSarifPath = Path.Combine(testDir, "testSarif.json");
        File.WriteAllText(testSarifPath, inputSarif.ToEnvironmentLineEndings());
        var returnedSarifPath = new RoslynV1SarifFixer(runtime).LoadAndFixFile(testSarifPath, RoslynV1SarifFixer.VBNetLanguage);

        returnedSarifPath.Should().BeNull();
        runtime.Telemetry.Messages.Should().BeEmpty();
    }

    private static void AssertFileUnchanged(string filePath, DateTime originalWriteTime) =>
        new FileInfo(filePath).LastWriteTime.Should().Be(originalWriteTime);
}
