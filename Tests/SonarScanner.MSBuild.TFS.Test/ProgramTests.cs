/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

namespace SonarScanner.MSBuild.TFS.Classic.Tests;

[TestClass]
public class ProgramTests
{
    private readonly TestRuntime runtime = new TestRuntime();

    [TestMethod]
    public void Execute_WhenNoArgs_ShouldLogError()
    {
        var result = Program.Execute([], runtime);

        result.Should().Be(1);
        runtime.Logger.Warnings.Should().BeEmpty();
        runtime.Logger.Errors.Should().BeEquivalentTo("No argument found. Exiting...");
    }

    [TestMethod]
    public void Execute_ExceptionThrown_LogsError()
    {
        var loggerRuntime = new MockedLoggerRuntime();
        loggerRuntime.Logger.When(x => x.LogError("No argument found. Exiting...")).Do(x => { throw new Exception("Mock Exception"); });
        var result = Program.Execute([], loggerRuntime);

        result.Should().Be(1);
        loggerRuntime.Logger.Received().LogError("An exception occurred while executing the process: Mock Exception");
    }

    [TestMethod]
    public void Execute_Method_Is_Uknown_Should_Log_Error()
    {
        var result = Program.Execute(["MockMethod"], runtime);

        result.Should().Be(1);
        runtime.Logger.Errors.Should().BeEquivalentTo("Failed to parse or retrieve arguments for command line.");
    }

    [TestMethod]
    public void Execute_ShouldExecute_CoverageConverter_ShouldSucceeed()
    {
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        var text = """
            <?xml version="1.0" encoding="utf-8"?>
            <AnalysisConfig xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns="http://www.sonarsource.com/msbuild/integration/2015/1">
            </AnalysisConfig>
            """;
        File.WriteAllText(Path.Combine(tempDir, "temp.xml"), text);
        File.WriteAllText(Path.Combine(tempDir, "sonar-project.properties"), string.Empty);
        using var scope = new EnvironmentVariableScope();
        // Faking TeamBuild
        scope.SetVariable(EnvironmentVariables.IsInTeamFoundationBuild, "true");
        scope.SetVariable(EnvironmentVariables.BuildUriTfs2015, "vsts://test/test");
        scope.SetVariable(EnvironmentVariables.BuildDirectoryTfs2015, tempDir);
        scope.SetVariable(EnvironmentVariables.SourcesDirectoryTfs2015, tempDir);
        var result = Program.Execute(["ConvertCoverage", Path.Combine(tempDir, "temp.xml"), Path.Combine(tempDir, "sonar-project.properties")], runtime);

        result.Should().Be(0);
        runtime.Logger.Errors.Should().BeEmpty();
        runtime.Logger.InfoMessages.Should().NotContain("Coverage report conversion completed successfully.");
        runtime.Logger.InfoMessages.Should().NotContain("Coverage report conversion has failed. Skipping...");
    }

    [TestMethod]
    public void Execute_ShouldExecute_SummaryReportBuilder_ShouldSucceed()
    {
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        var text = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <AnalysisConfig xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns="http://www.sonarsource.com/msbuild/integration/2015/1">
                <SonarOutputDir>{tempDir}</SonarOutputDir>
                <SonarQubeHostUrl>http://localhost/</SonarQubeHostUrl>
            </AnalysisConfig>
            """;
        File.WriteAllText(Path.Combine(tempDir, "temp.xml"), text);
        File.WriteAllText(Path.Combine(tempDir, "sonar-project.properties"), string.Empty);
        // Faking TeamBuild
        Environment.SetEnvironmentVariable("TF_Build", "true");
        Environment.SetEnvironmentVariable("BUILD_BUILDURI", "vsts://test/test");
        Environment.SetEnvironmentVariable("AGENT_BUILDDIRECTORY", tempDir);
        Environment.SetEnvironmentVariable("BUILD_SOURCESDIRECTORY", tempDir);
        using var scope = new EnvironmentVariableScope();
        // Faking TeamBuild
        scope.SetVariable(EnvironmentVariables.IsInTeamFoundationBuild, "true");
        scope.SetVariable(EnvironmentVariables.BuildUriTfs2015, "vsts://test/test");
        scope.SetVariable(EnvironmentVariables.BuildDirectoryTfs2015, tempDir);
        scope.SetVariable(EnvironmentVariables.SourcesDirectoryTfs2015, tempDir);
        var result = Program.Execute(["SummaryReportBuilder", Path.Combine(tempDir, "temp.xml"), Path.Combine(tempDir, "sonar-project.properties"), "true"], runtime);

        result.Should().Be(0);
    }

    private sealed class MockedLoggerRuntime : IRuntime
    {
        public ILogger Logger { get; } = Substitute.For<ILogger>();
        public OperatingSystemProvider OperatingSystem => throw new NotImplementedException();
        public IDirectoryWrapper Directory => throw new NotImplementedException();
        public IFileWrapper File => throw new NotImplementedException();

        public void LogDebug(string message, params object[] args) =>
            Logger.LogDebug(message, args);

        public void LogInfo(string message, params object[] args) =>
            Logger.LogInfo(message, args);

        public void LogWarning(string message, params object[] args) =>
            Logger.LogWarning(message, args);

        public void LogError(string message, params object[] args) =>
            Logger.LogError(message, args);
    }
}
