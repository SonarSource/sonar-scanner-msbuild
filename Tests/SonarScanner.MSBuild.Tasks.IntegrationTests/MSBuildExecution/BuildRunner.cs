/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
 * mailto:info AT sonarsource DOT com
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// Utility classes that handles executing msbuild.exe and return a log
    /// describing the build
    /// </summary>
    public static class BuildRunner
    {
        public static BuildLog BuildTargets(TestContext testContext, string projectFile, params string[] targets)
        {
            return BuildTargets(testContext, projectFile, buildShouldSucceed: true, targets: targets);
        }

        public static BuildLog BuildTargets(TestContext testContext, string projectFile, bool buildShouldSucceed, params string[] targets)
        {
            // TODO: support 14.0?
            const string msBuildVersion = "15.0";

            // Expecting a path like "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\msbuild.exe"
            var exePath = MSBuildLocator.GetMSBuildPath(msBuildVersion, testContext);
            exePath.Should().NotBeNull($"Test setup failure - failed to locate MSBuild.exe for version {msBuildVersion}");
            File.Exists(exePath).Should().BeTrue($"expecting the returned msbuild.exe file to exist. File path: {exePath}");
            Path.GetFileName(exePath).Should().Be("msbuild.exe");
            Path.GetDirectoryName(exePath).Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar })
                .Should().Contain(msBuildVersion);

            // The build is being run in a separate process so we can't directly
            // capture the property values or which tasks/targets were executed.
            // Instead, we add a custom logger that will record that information
            // in a structured form that we can check later.
            var logPath = projectFile + ".log";
            var msbuildArgs = new List<string>();
            var loggerType = typeof(SimpleXmlLogger);
            msbuildArgs.Add($"/logger:{loggerType.FullName},{loggerType.Assembly.Location};{logPath}");
            msbuildArgs.Add(projectFile);

            // Ask MSBuild to create a detailed binary log (not used by the tests,
            // but it simplifies manual investigation of failures)
            var projectDir = Path.GetDirectoryName(projectFile);
            var binaryLogPath = Path.Combine(projectDir, "buildlog.binlog");
            msbuildArgs.Add("/bl:" + binaryLogPath);
            System.Console.WriteLine("Project Directory: " + projectDir);

            // Specify the targets to be executed, if any
            if (targets?.Length > 0)
            {
                msbuildArgs.Add($"/t:" + string.Join(";", targets.ToArray()));
            }

            // Run the build
            var args = new ProcessRunnerArguments(exePath, false)
            {
                CmdLineArgs = msbuildArgs
            };
            var runner = new ProcessRunner(new ConsoleLogger(true));
            var success = runner.Execute(args);

            File.Exists(logPath).Should().BeTrue();
            testContext.AddResultFile(logPath);

            success.Should().Be(buildShouldSucceed);

            return BuildLog.Load(logPath);
        }
    }
}
