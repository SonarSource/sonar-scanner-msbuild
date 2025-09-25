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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class RuntimeTests
{
    [TestMethod]
    public void Constructor_OperatingSystemNull_Throws() =>
        FluentActions.Invoking(() => new Runtime(null, null, null, null, null, null))
            .Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("operatingSystem");

    [TestMethod]
    public void Constructor_DirectoryWrapperNull_Throws() =>
        FluentActions.Invoking(() => new Runtime(OperatingSystemMock(), null, null, null, null, null))
            .Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("directoryWrapper");

    [TestMethod]
    public void Constructor_FileWrapperNull_Throws() =>
        FluentActions.Invoking(() => new Runtime(OperatingSystemMock(), Substitute.For<IDirectoryWrapper>(), null, null, null, null))
            .Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("fileWrapper");

    [TestMethod]
    public void Constructor_LoggerNull_Throws() =>
        FluentActions.Invoking(() => new Runtime(OperatingSystemMock(), Substitute.For<IDirectoryWrapper>(), Substitute.For<IFileWrapper>(), null, null, null))
            .Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("logger");

    [TestMethod]
    public void Constructor_TelemetryNull_Throws() =>
        FluentActions.Invoking(() => new Runtime(OperatingSystemMock(), Substitute.For<IDirectoryWrapper>(), Substitute.For<IFileWrapper>(), Substitute.For<ILogger>(), null, null))
            .Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("telemetry");

    [TestMethod]
    public void Constructor_AnalysisWarningsNull_Throws() =>
        FluentActions
            .Invoking(() => new Runtime(OperatingSystemMock(), Substitute.For<IDirectoryWrapper>(), Substitute.For<IFileWrapper>(), Substitute.For<ILogger>(), Substitute.For<ITelemetry>(), null))
            .Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("analysisWarnings");

    [TestMethod]
    public void Constructor_SetsProperties()
    {
        var operatingSystem = OperatingSystemMock();
        var directory = Substitute.For<IDirectoryWrapper>();
        var file = Substitute.For<IFileWrapper>();
        var logger = Substitute.For<ILogger>();
        var telemetry = Substitute.For<ITelemetry>();
        var analysisWarnings = Substitute.For<AnalysisWarnings>(null, null);

        var sut = new Runtime(operatingSystem, directory, file, logger, telemetry, analysisWarnings);

        sut.OperatingSystem.Should().Be(operatingSystem);
        sut.Directory.Should().Be(directory);
        sut.File.Should().Be(file);
        sut.Logger.Should().Be(logger);
        sut.Telemetry.Should().Be(telemetry);
        sut.AnalysisWarnings.Should().Be(analysisWarnings);
    }

    private static OperatingSystemProvider OperatingSystemMock() =>
        Substitute.For<OperatingSystemProvider>(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>());
}
