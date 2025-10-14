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
public class StringExtensionsTests
{
    private static IEnumerable<object[]> SensitivePropertyKeys =>
        SonarProperties.SensitivePropertyKeys.Select(x => new object[] { x });

    [TestMethod]
    public void ReplaceCaseInsensitive()
    {
        "abcdef".ReplaceCaseInsensitive("abc", "xyz").Should().Be("xyzdef");
        "ABCdef".ReplaceCaseInsensitive("abc", "xyz").Should().Be("xyzdef");
        "A*BCdef".ReplaceCaseInsensitive("a*bc", "xyz").Should().Be("xyzdef");
        "abcdef".ReplaceCaseInsensitive("abc", "x*yz").Should().Be("x*yzdef");
        "abcdef".ReplaceCaseInsensitive("abc", "$").Should().Be("$def");
        "ab$$$def".ReplaceCaseInsensitive("$", "x").Should().Be("abxxxdef");
        "aabcbcdef".ReplaceCaseInsensitive("abc", "x").Should().Be("axbcdef");
    }

    [TestMethod]
    public void RedactSensitiveData_NoSensitiveData() =>
        "Some string with no sensitive data".RedactSensitiveData().Should().Be("Some string with no sensitive data");

    [TestMethod]
    [DynamicData(nameof(SensitivePropertyKeys))]
    public void RedactSensitiveData_SensitiveData(string sensitiveKey) =>
        @$"Setting environment variable 'SONAR_SCANNER_OPTS'. Value: -D{sensitiveKey}=""changeit""".RedactSensitiveData()
            .Should().Be("Setting environment variable 'SONAR_SCANNER_OPTS'. Value: -D<sensitive data removed>");

    [TestMethod]
    [CombinatorialData]
    public void RedactSensitiveData_MixedSensitiveData(
        [CombinatorialValues(SonarProperties.SonarToken, SonarProperties.SonarPassword)] string sensitiveKey1,
        [CombinatorialValues(SonarProperties.SonarToken, SonarProperties.SonarPassword)] string sensitiveKey2) =>
        @$"Setting environment variable 'SONAR_SCANNER_OPTS'. Value: -D{sensitiveKey1}=""changeit"" -D{sensitiveKey2}=""changeit""".RedactSensitiveData()
            .Should().Be("Setting environment variable 'SONAR_SCANNER_OPTS'. Value: -D<sensitive data removed>");
}
