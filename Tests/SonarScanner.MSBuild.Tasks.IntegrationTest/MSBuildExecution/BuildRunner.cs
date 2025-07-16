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

namespace SonarScanner.MSBuild.Tasks.IntegrationTest;

/// <summary>
/// Utility classes that handles executing msbuild.exe and return a log describing the build.
/// </summary>
public static class BuildRunner
{
    public static BuildLog BuildTargets(TestContext testContext, string projectFile, params string[] targets) =>
        BuildTargets(testContext, projectFile, buildShouldSucceed: true, targets: targets);

    public static BuildLog BuildTargets(TestContext testContext, string projectFile, bool buildShouldSucceed, params string[] targets)
    {
        // Expecting a path like "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\msbuild.exe"
        var exePath = MSBuildLocator.GetMSBuildPath(testContext);
        exePath.Should().NotBeNull("Test setup failure - failed to locate MSBuild.exe");
        File.Exists(exePath).Should().BeTrue($"expecting the returned msbuild.exe file to exist. File path: {exePath}");
        var executable = Path.GetFileName(exePath);
        executable.Should().MatchRegex(@"(msbuild\.exe|dotnet)");

        // We generate binary log because the build is being run in a separate process so we can't directly capture the property values or which tasks/targets were executed.
        var projectDir = Path.GetDirectoryName(projectFile);
        var binaryLogPath = Path.Combine(projectDir, Path.ChangeExtension(Path.GetRandomFileName(), ".binlog"));
        List<string> msbuildArgs =
        [
            .. executable == "dotnet" ? (string[])["msbuild"] : [],
            projectFile,
            "/bl:" + binaryLogPath
        ];
        Console.WriteLine("Project Directory: " + Path.GetDirectoryName(projectFile));

        msbuildArgs.Add($"/t:{(targets is null || targets.Length is 0 ? $"{TargetConstants.Restore};{TargetConstants.DefaultBuild}" : string.Join(";", targets))}");

        // Run the build
        var args = new ProcessRunnerArguments(exePath, false)
        {
            CmdLineArgs = msbuildArgs
        };
        var runner = new ProcessRunner(new ConsoleLogger(true));
        var success = runner.Execute(args);

        File.Exists(binaryLogPath).Should().BeTrue();
        testContext.AddResultFile(binaryLogPath);

        success.Succeeded.Should().Be(buildShouldSucceed);

        return new BuildLog(binaryLogPath);
    }
}
