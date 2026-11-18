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

using TestUtilities;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class ScannerJavaOptsTests
{
    [TestMethod]
    public void TryGetFromEnvironment_NothingSet_ReturnsFalse()
    {
        using var scope = new EnvironmentVariableScope()
            .SetVariable(EnvironmentVariables.SonarScannerJavaOptsVariableName, null)
            .SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, null);

        ScannerJavaOpts.TryGetFromEnvironment(out var name, out var value).Should().BeFalse();
        name.Should().BeNull();
        value.Should().BeNull();
    }

    [TestMethod]
    public void TryGetFromEnvironment_OnlyScannerJavaOpts_ReturnsIt()
    {
        using var scope = new EnvironmentVariableScope()
            .SetVariable(EnvironmentVariables.SonarScannerJavaOptsVariableName, "-XX:-UseBASE64Intrinsics")
            .SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, null);

        ScannerJavaOpts.TryGetFromEnvironment(out var name, out var value).Should().BeTrue();
        name.Should().Be("SONAR_SCANNER_JAVA_OPTS");
        value.Should().Be("-XX:-UseBASE64Intrinsics");
    }

    [TestMethod]
    public void TryGetFromEnvironment_OnlyScannerOpts_ReturnsIt()
    {
        using var scope = new EnvironmentVariableScope()
            .SetVariable(EnvironmentVariables.SonarScannerJavaOptsVariableName, null)
            .SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, "-XX:-UseBASE64Intrinsics");

        ScannerJavaOpts.TryGetFromEnvironment(out var name, out var value).Should().BeTrue();
        name.Should().Be("SONAR_SCANNER_OPTS");
        value.Should().Be("-XX:-UseBASE64Intrinsics");
    }

    [TestMethod]
    public void TryGetFromEnvironment_BothSet_ScannerJavaOptsWins()
    {
        using var scope = new EnvironmentVariableScope()
            .SetVariable(EnvironmentVariables.SonarScannerJavaOptsVariableName, "-Xmx1g")
            .SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, "-Xmx2g");

        ScannerJavaOpts.TryGetFromEnvironment(out var name, out var value).Should().BeTrue();
        name.Should().Be("SONAR_SCANNER_JAVA_OPTS");
        value.Should().Be("-Xmx1g");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("  ")]
    public void TryGetFromEnvironment_ScannerJavaOptsEmptyOrWhitespace_FallsBackToScannerOpts(string scannerJavaOpts)
    {
        using var scope = new EnvironmentVariableScope()
            .SetVariable(EnvironmentVariables.SonarScannerJavaOptsVariableName, scannerJavaOpts)
            .SetVariable(EnvironmentVariables.SonarScannerOptsVariableName, "-Xmx2g");

        ScannerJavaOpts.TryGetFromEnvironment(out var name, out var value).Should().BeTrue();
        name.Should().Be("SONAR_SCANNER_OPTS");
        value.Should().Be("-Xmx2g");
    }
}
