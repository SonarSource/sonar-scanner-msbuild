/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class DefaultPropertiesFileTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void Load_WhenUsingDefaultFile_ReturnsEmptyList()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var propertiesFile = TestUtils.EnsureDefaultPropertiesFileExists(testDir, TestContext);

            // Act - will error if the file is badly-formed
            var defaultProps = AnalysisProperties.Load(propertiesFile);

            Assert.AreEqual(0, defaultProps.Count, "Unexpected number of properties defined in the default properties file");
        }

        [TestMethod]
        public void Load_WhenUsingProperties_ReturnsExpectedProperties()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var propertiesFile = TestUtils.EnsureDefaultPropertiesFileExists(testDir, TestContext);
            File.WriteAllText(propertiesFile, @"<SonarQubeAnalysisProperties  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""http://www.sonarsource.com/msbuild/integration/2015/1"">
    <Property Name=""sonar.host.url"">http://localhost:9000</Property>
    <Property Name=""sonar.login"">SomeLogin</Property>
    <Property Name=""sonar.password"">SomePassword</Property>
</SonarQubeAnalysisProperties>");

            // Act - will error if the file is badly-formed
            var properties = AnalysisProperties.Load(propertiesFile);

            Assert.AreEqual(3, properties.Count, "Unexpected number of properties defined in the default properties file");
            Assert.AreEqual("sonar.host.url", properties[0].Id);
            Assert.AreEqual("sonar.login", properties[1].Id);
            Assert.AreEqual("sonar.password", properties[2].Id);
            Assert.AreEqual("http://localhost:9000", properties[0].Value);
            Assert.AreEqual("SomeLogin", properties[1].Value);
            Assert.AreEqual("SomePassword", properties[2].Value);
        }

        [TestMethod]
        public void Load_WhenUsingInvalidName_ThrowsXmlException()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var propertiesFile = TestUtils.EnsureDefaultPropertiesFileExists(testDir, TestContext);
            File.WriteAllText(propertiesFile, @"<SonarQubeAnalysisProperties  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""http://www.sonarsource.com/msbuild/integration/2015/1"">
    <Property name=""sonar.verbose"">true</Property>
</SonarQubeAnalysisProperties>");

            // Act - will error if the file is badly-formed
            try
            {
                var properties = AnalysisProperties.Load(propertiesFile);

                Assert.Fail("Expecting XmlException to be thrown.");
            }
            catch (System.Xml.XmlException e)
            {
                Assert.AreEqual("At least one property name is missing. Please check that the settings file is valid.",
                    e.Message);
            }
        }

        #endregion Tests
    }
}
