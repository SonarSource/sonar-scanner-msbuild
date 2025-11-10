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

using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Test;

[TestClass]
public class DefaultPropertiesFileTests
{
    public TestContext TestContext { get; set; }

    #region Tests

    [TestMethod]
    public void Load_WhenUsingDefaultFile_ReturnsEmptyList()
    {
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var propertiesFile = TestUtils.EnsureDefaultPropertiesFileExists(testDir, TestContext);

        // Act - will error if the file is badly-formed
        var defaultProps = AnalysisProperties.Load(propertiesFile);

        defaultProps.Should().BeEmpty("Unexpected number of properties defined in the default properties file");
    }

    [TestMethod]
    public void Load_WhenUsingProperties_ReturnsExpectedProperties()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var propertiesFile = TestUtils.EnsureDefaultPropertiesFileExists(testDir, TestContext);
        File.WriteAllText(propertiesFile, @"<SonarQubeAnalysisProperties  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""http://www.sonarsource.com/msbuild/integration/2015/1"">
    <Property Name=""sonar.host.url"">http://localhost:9000</Property>
    <Property Name=""sonar.token"">token</Property>
    <Property Name=""sonar.login"">SomeLogin</Property>
    <Property Name=""sonar.password"">SomePassword</Property>
</SonarQubeAnalysisProperties>");

        var properties = AnalysisProperties.Load(propertiesFile);

        properties.Should().HaveCount(4, "Unexpected number of properties defined in the default properties file");
        properties[0].Id.Should().Be("sonar.host.url");
        properties[1].Id.Should().Be("sonar.token");
        properties[2].Id.Should().Be("sonar.login");
        properties[3].Id.Should().Be("sonar.password");
        properties[0].Value.Should().Be("http://localhost:9000");
        properties[1].Value.Should().Be("token");
        properties[2].Value.Should().Be("SomeLogin");
        properties[3].Value.Should().Be("SomePassword");
    }

    [TestMethod]
    public void Load_WhenUsingInvalidName_ThrowsXmlException()
    {
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var propertiesFile = TestUtils.EnsureDefaultPropertiesFileExists(testDir, TestContext);
        File.WriteAllText(propertiesFile, @"<SonarQubeAnalysisProperties  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""http://www.sonarsource.com/msbuild/integration/2015/1"">
    <Property name=""sonar.verbose"">true</Property>
</SonarQubeAnalysisProperties>");

        // Act - will error if the file is badly-formed
        try
        {
            AnalysisProperties.Load(propertiesFile);

            Assert.Fail("Expecting XmlException to be thrown.");
        }
        catch (System.Xml.XmlException e)
        {
            e.Message.Should().Be("At least one property name is missing. Please check that the settings file is valid.");
        }
    }

    #endregion Tests
}
