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

namespace SonarScanner.MSBuild.Test;

[TestClass]
public class BootstrapperSettingsTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void InvalidArguments() =>
        FluentActions.Invoking(() => new BootstrapperSettings(AnalysisPhase.PreProcessing, null, LoggerVerbosity.Debug, null)).Should().ThrowExactly<ArgumentNullException>();

    [TestMethod]
    public void Properties_RelativePathsConvertToAbsolute()
    {
        using var envScope = new EnvironmentVariableScope().SetVariable(EnvironmentVariables.BuildDirectoryLegacy, $@"c:{Path.DirectorySeparatorChar}temp");
        var sut = new BootstrapperSettings(AnalysisPhase.PreProcessing, null, LoggerVerbosity.Debug, new TestRuntime());
        sut.TempDirectory.Should().Be($@"c:{Path.DirectorySeparatorChar}temp{Path.DirectorySeparatorChar}.sonarqube");
    }

    [TestMethod]
    public void Properties_ScannerBinaryDirPath()
    {
        var sut = new BootstrapperSettings(AnalysisPhase.PreProcessing, null, LoggerVerbosity.Debug, new TestRuntime());
        string extension;
#if NETFRAMEWORK
        extension = "exe";
#else
        extension = "dll";
#endif
        $"{sut.ScannerBinaryDirPath}{Path.DirectorySeparatorChar}SonarScanner.MSBuild.{extension}".Should().Be(typeof(BootstrapperSettings).Assembly.Location);
    }
}
