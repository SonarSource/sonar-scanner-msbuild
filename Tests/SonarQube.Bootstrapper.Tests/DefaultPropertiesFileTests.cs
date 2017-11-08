/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
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
 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class DefaultPropertiesFileTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void DefaultProperties_AreValid()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string propertiesFile = TestUtils.EnsureDefaultPropertiesFileExists(testDir, this.TestContext);

            // Act - will error if the file is badly-formed
            AnalysisProperties defaultProps = AnalysisProperties.Load(propertiesFile);


            Assert.AreEqual(0, defaultProps.Count, "Unexpected number of properties defined in the default properties file");
        }

        #endregion

        #region Checks

        private static void AssertPropertyHasValue(string key, string expectedValue, AnalysisProperties properties)
        {
            bool found = Property.TryGetProperty(key, properties, out Property match);

            Assert.IsTrue(found, "Expected property was not found. Key: {0}", key);
            Assert.AreEqual(expectedValue, match.Value, "Property does not have the expected value. Key: {0}", key);
        }

        #endregion

    }
}
