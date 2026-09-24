/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

using static SonarScanner.MSBuild.Shim.ScannerEngineInputGenerator;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class RoslynV1SarifFixerTests
{
    private const string ValidSarif = """
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

    private readonly TestRuntime runtime = new();

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_Null() =>
        FluentActions.Invoking(() => new RoslynV1SarifFixer(null)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("runtime");

    [TestMethod]
    public void FixReports_FileDoesNotExist()
    {
        FixReport("Some/NonexistantPath", ReportFilePathsKeyCS).Should().BeNull();
        runtime.Logger.InfoMessages.Should().ContainSingle().Which.Should().Contain("No Code Analysis ErrorLog file found");
    }

    /// <summary>
    /// There should be no change to input if it is already valid, as attempting to fix valid SARIF may cause over-escaping.
    /// This should be the case even if the output came from VS 2015 RTM.
    /// </summary>
    [TestMethod]
    public void FixReport_Valid()
    {
        var testSarifPath = CreateSarifFile("testSarif.json", ValidSarif);
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        var returnedSarifPath = FixReport(testSarifPath, ReportFilePathsKeyCS);
        // Already valid -> no change to file, same file path returned
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().Be(testSarifPath);
        runtime.Telemetry.Messages.Should().ContainEquivalentOf(new KeyValuePair<string, string>("dotnetenterprise.s4net.endstep.Sarif.v0_1_0_0.Valid", "True"));
    }

    [TestMethod]
    public void FixReport_Unfixable()
    {
        var sarifInput = """
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
        var testSarifPath = CreateSarifFile("testSarif.json", sarifInput);
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        var returnedSarifPath = FixReport(testSarifPath, ReportFilePathsKeyCS);
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
    public void FixReport_MultipleLineValues()
    {
        var inputSarif = """
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
        var testSarifPath = CreateSarifFile("testSarif.json", inputSarif);
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        var returnedSarifPath = FixReport(testSarifPath, ReportFilePathsKeyCS);
        // Not fixable -> no change to file, null return
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().BeNull();
        runtime.Telemetry.Messages.Should().BeEmpty();
    }

    [TestMethod]
    public void FixReport_EscapeBackslashes()
    {
        var expectedSarif = """
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
        var testSarifPath = CreateSarifFile("testSarif.json", RoslynV1Sarif("Visual C#"));
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        var returnedSarifPath = FixReport(testSarifPath, ReportFilePathsKeyCS);
        // Fixable -> no change to file, file path in return value, file contents as expected
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().NotBeNull();
        File.ReadAllText(returnedSarifPath).Should().Be(expectedSarif.ToEnvironmentLineEndings());
        runtime.Telemetry.Messages.Should().ContainEquivalentOf(
            new KeyValuePair<string, string>("dotnetenterprise.s4net.endstep.Sarif.v0_1_0_0.Fixed", "True"));
    }

    [TestMethod]
    public void FixReport_EscapeQuotes()
    {
        var inputSarif = """
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
        var expectedSarif = """
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
        var testSarifPath = CreateSarifFile("testSarif.json", inputSarif);
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        var returnedSarifPath = FixReport(testSarifPath, ReportFilePathsKeyCS);
        // Fixable -> no change to file, file path in return value, file contents as expected
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().NotBeNull();
        File.ReadAllText(returnedSarifPath).Should().Be(expectedSarif.ToEnvironmentLineEndings());
        runtime.Telemetry.Messages.Should().ContainEquivalentOf(
            new KeyValuePair<string, string>("dotnetenterprise.s4net.endstep.Sarif.v0_1_0_0.Fixed", "True"));
    }

    [TestMethod]
    public void FixReport_EscapeCharsInAllAffectedFields()
    {
        var inputSarif = """
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
        var expectedSarif = """
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
        var testSarifPath = CreateSarifFile("testSarif.json", inputSarif);
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        var returnedSarifPath = FixReport(testSarifPath, ReportFilePathsKeyCS);
        // Fixable -> no change to file, file path in return value, file contents as expected
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().NotBeNull();
        File.ReadAllText(returnedSarifPath).Should().Be(expectedSarif.ToEnvironmentLineEndings());
        runtime.Telemetry.Messages.Should().ContainEquivalentOf(
            new KeyValuePair<string, string>("dotnetenterprise.s4net.endstep.Sarif.v0_1_0_0.Fixed", "True"));
    }

    [TestMethod]
    public void FixReport_VBNet()
    {
        var expectedSarif = """
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
        var testSarifPath = CreateSarifFile("testSarif.json", RoslynV1Sarif("Visual Basic"));
        var originalWriteTime = new FileInfo(testSarifPath).LastWriteTime;

        var returnedSarifPath = FixReport(testSarifPath, ReportFilePathsKeyVB);
        // Fixable -> no change to file, file path in return value, file contents as expected
        AssertFileUnchanged(testSarifPath, originalWriteTime);
        returnedSarifPath.Should().NotBeNull();
        File.ReadAllText(returnedSarifPath).Should().Be(expectedSarif.ToEnvironmentLineEndings());
        runtime.Telemetry.Messages.Should().ContainEquivalentOf(
            new KeyValuePair<string, string>("dotnetenterprise.s4net.endstep.Sarif.v0_1_0_0.Fixed", "True"));
    }

    /// <summary>
    /// To avoid FPs, the tool name declared in the file is compared with the language. If it doesn't match, do nothing.
    /// </summary>
    [TestMethod]
    public void FixReport_Invalid()
    {
        var testSarifPath = CreateSarifFile("testSarif.json", RoslynV1Sarif("Visual C#"));

        var returnedSarifPath = FixReport(testSarifPath, ReportFilePathsKeyVB);
        returnedSarifPath.Should().BeNull();
        runtime.Telemetry.Messages.Should().BeEmpty();
    }

    [TestMethod]
    public void FixReports_MultipleReportPaths()
    {
        var validPath = CreateSarifFile("valid.json", ValidSarif);
        var roslynV1Path = CreateSarifFile("roslynV1.json", RoslynV1Sarif("Visual C#"));
        var missingPath = Path.Combine(Path.GetDirectoryName(validPath), "missing.json");
        var project = CreateProject(ReportFilePathsKeyCS, $"{validPath}|{missingPath}|{roslynV1Path}");

        new RoslynV1SarifFixer(new TestRuntime()).FixReports([project]);
        project.AnalysisSettings.Should().ContainSingle().Which.Should().BeEquivalentTo(new Property(ReportFilePathsKeyCS, $"{validPath}|{roslynV1Path.Replace(".json", "_fixed.json")}"));
    }

    [TestMethod]
    public void FixReports_MultipleProjects()
    {
        var validPath = CreateSarifFile("valid.json", ValidSarif);
        var roslynV1Path = CreateSarifFile("roslynV1.json", RoslynV1Sarif("Visual Basic"));
        var missingPath = Path.Combine(Path.GetDirectoryName(validPath), "missing.json");
        var project1 = CreateProject(ReportFilePathsKeyCS, validPath);
        var project2 = CreateProject(ReportFilePathsKeyVB, roslynV1Path);
        var project3 = CreateProject(ReportFilePathsKeyCS, missingPath);

        new RoslynV1SarifFixer(new TestRuntime()).FixReports([project1, project2, project3]);
        project1.AnalysisSettings.Should().ContainSingle().Which.Should().BeEquivalentTo(new Property(ReportFilePathsKeyCS, validPath));
        project2.AnalysisSettings.Should().ContainSingle().Which.Should().BeEquivalentTo(new Property(ReportFilePathsKeyVB, roslynV1Path.Replace(".json", "_fixed.json")));
        project3.AnalysisSettings.Should().BeEmpty();
    }

    private string FixReport(string sarifPath, string reportPathsKey)
    {
        var project = CreateProject(reportPathsKey, sarifPath);
        new RoslynV1SarifFixer(runtime).FixReports([project]);
        return project.AnalysisSettings.SingleOrDefault()?.Value;
    }

    private string CreateSarifFile(string fileName, string content) =>
        TestUtils.CreateFile(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext), fileName, content.ToEnvironmentLineEndings());

    private static string RoslynV1Sarif(string compiler) =>
        $$"""
        {
          "version": "0.1",
          "toolInfo": {
            "toolName": "Microsoft (R) {{compiler}} Compiler",
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

    private static ProjectInfo CreateProject(string key, string value) =>
        new() { AnalysisSettings = [new Property(key, value)] };

    private static void AssertFileUnchanged(string filePath, DateTime originalWriteTime) =>
        new FileInfo(filePath).LastWriteTime.Should().Be(originalWriteTime);
}
