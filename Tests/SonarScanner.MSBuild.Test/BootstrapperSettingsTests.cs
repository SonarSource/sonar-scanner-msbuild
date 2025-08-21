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
    public void BootSettings_InvalidArguments()
    {
        Action act = () => new BootstrapperSettings(AnalysisPhase.PreProcessing, null, LoggerVerbosity.Debug, null);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    public void BootSettings_Properties_RelativePathsConvertToAbsolute()
    {
        using var envScope = new EnvironmentVariableScope().SetVariable(EnvironmentVariables.BuildDirectoryLegacy, $@"c:{Path.DirectorySeparatorChar}temp");
        var sut = new BootstrapperSettings(AnalysisPhase.PreProcessing, null, LoggerVerbosity.Debug, new TestLogger());
        sut.TempDirectory.Should().Be($@"c:{Path.DirectorySeparatorChar}temp{Path.DirectorySeparatorChar}.sonarqube");
    }
}
