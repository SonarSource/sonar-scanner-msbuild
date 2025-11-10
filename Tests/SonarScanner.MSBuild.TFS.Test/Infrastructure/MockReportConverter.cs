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

namespace SonarScanner.MSBuild.TFS.Test.Infrastructure;

internal class MockReportConverter : ICoverageReportConverter
{
    private int convertCallCount;

    public bool ShouldFailConversion { get; set; } = false;

    public void AssertExpectedNumberOfConversions(int expected) =>
        convertCallCount.Should().Be(expected, "ConvertToXml called an unexpected number of times");

    public void AssertConvertCalledAtLeastOnce() =>
        convertCallCount.Should().BePositive("ConvertToXml called less than once.");

    public void AssertConvertNotCalled() =>
        convertCallCount.Should().Be(0, "Not expecting ConvertToXml to have been called");

    bool ICoverageReportConverter.ConvertToXml(string inputFilePath, string outputFilePath)
    {
        convertCallCount++;

        return !ShouldFailConversion;
    }
}
