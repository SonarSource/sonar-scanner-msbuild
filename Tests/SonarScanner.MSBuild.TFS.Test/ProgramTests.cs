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
    [TestMethod]
    public void Execute_WhenNoArgs_ShouldLogError()
    {
        var logger = new TestLogger();
        var result = Program.Execute(new string[] { }, logger);

        result.Should().Be(1);
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().HaveCount(1);
        logger.Errors.Should().Contain(new string[] { "No argument found. Exiting..." });
    }

    [TestMethod]
    public void Execute_Method_Is_Uknown_Should_Log_Error()
    {
        var logger = new TestLogger();
        var result = Program.Execute(new string[] { "MockMethod" }, logger);

        result.Should().Be(1);
        logger.Errors.Should().HaveCount(1);
        logger.Errors.Should().Contain(new string[] { "Failed to parse or retrieve arguments for command line." });
    }

    [TestMethod]
    public void Execute_ShouldExecute_CoverageConverter_ShouldSucceeed()
    {
        var logger = new TestLogger();
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        var text = @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <AnalysisConfig xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
                        xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://www.sonarsource.com/msbuild/integration/2015/1"">
                        </AnalysisConfig>";

        File.WriteAllText(Path.Combine(tempDir, "temp.xml"), text);
        File.WriteAllText(Path.Combine(tempDir, "sonar-project.properties"), string.Empty);

        using var scope = new EnvironmentVariableScope();

        // Faking TeamBuild
        scope.SetVariable(EnvironmentVariables.IsInTeamFoundationBuild, "true");
        scope.SetVariable(EnvironmentVariables.BuildUriTfs2015, "vsts://test/test");
        scope.SetVariable(EnvironmentVariables.BuildDirectoryTfs2015, tempDir);
        scope.SetVariable(EnvironmentVariables.SourcesDirectoryTfs2015, tempDir);

        var result = Program.Execute(new string[] { "ConvertCoverage", Path.Combine(tempDir, "temp.xml"), Path.Combine(tempDir, "sonar-project.properties") }, logger);

        result.Should().Be(0);
        logger.Errors.Should().HaveCount(0);
        logger.InfoMessages.Should().NotContain("Coverage report conversion completed successfully.");
        logger.InfoMessages.Should().NotContain("Coverage report conversion has failed. Skipping...");
    }

    [TestMethod]
    public void Execute_ShouldExecute_SummaryReportBuilder_ShouldSucceed()
    {
        var logger = new TestLogger();
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        var text = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <AnalysisConfig xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
                         xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://www.sonarsource.com/msbuild/integration/2015/1"">
                        <SonarOutputDir>{tempDir}</SonarOutputDir>
                        <SonarQubeHostUrl>http://localhost/</SonarQubeHostUrl>
                        </AnalysisConfig>";

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

        var result = Program.Execute(["SummaryReportBuilder", Path.Combine(tempDir, "temp.xml"), Path.Combine(tempDir, "sonar-project.properties"), "true"], logger);

        result.Should().Be(0);
    }
}
