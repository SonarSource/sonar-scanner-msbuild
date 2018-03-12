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

using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.Common.UnitTests
{
    [TestClass]
    public class AnalysisConfigExtensionsTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void GetConfigValue_WhenConfigIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.GetConfigValue(null, "foo", "bar");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
        }

        [TestMethod]
        public void GetConfigValue_WhenSettingIdIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.GetConfigValue(new AnalysisConfig(), null, "bar");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("settingId");
        }

        [TestMethod]
        public void GetConfigValue_WhenSettingIdIsEmpty_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.GetConfigValue(new AnalysisConfig(), "", "bar");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("settingId");
        }

        [TestMethod]
        public void GetConfigValue_WhenSettingIdIsWhitespace_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.GetConfigValue(new AnalysisConfig(), "   ", "bar");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("settingId");
        }

        [TestMethod]
        public void GetAnalysisSettings_WhenConfigIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.GetAnalysisSettings(null, false);

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
        }

        [TestMethod]
        public void SetSettingsFilePath_WhenConfigIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.SetSettingsFilePath(null, "foo");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
        }

        [TestMethod]
        public void SetSettingsFilePath_WhenFileNameIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.SetSettingsFilePath(new AnalysisConfig(), null);

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("fileName");
        }

        [TestMethod]
        public void SetSettingsFilePath_WhenFileNameIsEmpty_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.SetSettingsFilePath(new AnalysisConfig(), "");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("fileName");
        }

        [TestMethod]
        public void SetSettingsFilePath_WhenFileNameIsWhitespace_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.SetSettingsFilePath(new AnalysisConfig(), "   ");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("fileName");
        }

        [TestMethod]
        public void GetSettingsFilePath_WhenConfigIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.GetSettingsFilePath(null);

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
        }

        [TestMethod]
        public void SetConfigValue_WhenConfigIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.SetConfigValue(null, "foo", "bar");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
        }

        [TestMethod]
        public void SetConfigValue_WhenSettingIdIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.SetConfigValue(new AnalysisConfig(), null, "bar");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("settingId");
        }

        [TestMethod]
        public void SetConfigValue_WhenSettingIdIsEmpty_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.SetConfigValue(new AnalysisConfig(), "", "bar");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("settingId");
        }

        [TestMethod]
        public void SetConfigValue_WhenSettingIdIsWhitespace_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ConfigSettingsExtensions.SetConfigValue(new AnalysisConfig(), "   ", "bar");

            // Act & Assert
            action.ShouldThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("settingId");
        }

        [TestMethod]
        [Description("Checks the extension methods for getting and setting values")]
        public void ConfigExt_GetAndSet()
        {
            // 0. Setup
            var config = new AnalysisConfig();

            string result;

            // 1. Get missing setting -> default returned
            result = config.GetConfigValue("missing", "123");
            Assert.AreEqual("123", result, "Unexpected config value returned");

            // 2. Set and get a new setting
            config.SetConfigValue("id1", "value1");
            Assert.AreEqual("value1", config.GetConfigValue("id1", "XXX"), "Unexpected config value returned");

            // 3. Update an existing setting
            config.SetConfigValue("id1", "value2");
            Assert.AreEqual("value2", config.GetConfigValue("id1", "XXX"), "Unexpected config value returned");
        }

        [TestMethod]
        public void ConfigExt_GetAnalysisSettings_LocalOnly()
        {
            // Check that local settings are always retrieved by GetAnalysisSettings

            // 0. Setup
            var config = new AnalysisConfig
            {
                LocalSettings = new AnalysisProperties()
            };
            config.LocalSettings.Add(new Property() { Id = "local.1", Value = "local.value.1" });
            config.LocalSettings.Add(new Property() { Id = "local.2", Value = "local.value.2" });

            // 1. Local only
            var localProperties = config.GetAnalysisSettings(false);
            localProperties.AssertExpectedPropertyCount(2);
            localProperties.AssertExpectedPropertyValue("local.1", "local.value.1");
            localProperties.AssertExpectedPropertyValue("local.2", "local.value.2");

            // 2. Local and server
            var allProperties = config.GetAnalysisSettings(true);
            allProperties.AssertExpectedPropertyCount(2);
            allProperties.AssertExpectedPropertyValue("local.1", "local.value.1");
            allProperties.AssertExpectedPropertyValue("local.2", "local.value.2");
        }

        [TestMethod]
        public void ConfigExt_GetAnalysisSettings_ServerOnly()
        {
            // Check that local settings are only retrieved by GetAnalysisSettings
            // if includeServerSettings is true

            // 0. Setup
            var config = new AnalysisConfig
            {
                ServerSettings = new AnalysisProperties()
            };
            config.ServerSettings.Add(new Property() { Id = "server.1", Value = "server.value.1" });
            config.ServerSettings.Add(new Property() { Id = "server.2", Value = "server.value.2" });

            // 1. Local only
            var localProperties = config.GetAnalysisSettings(false);
            localProperties.AssertExpectedPropertyCount(0);

            localProperties.AssertPropertyDoesNotExist("server.1");
            localProperties.AssertPropertyDoesNotExist("server.2");

            // 2. Local and server
            var allProperties = config.GetAnalysisSettings(true);
            allProperties.AssertExpectedPropertyCount(2);
            allProperties.AssertExpectedPropertyValue("server.1", "server.value.1");
            allProperties.AssertExpectedPropertyValue("server.2", "server.value.2");
        }

        [TestMethod]
        public void ConfigExt_GetAnalysisSettings_FileSettings()
        {
            // Check that file settings are always retrieved by GetAnalysisSettings
            // and that the file name config property is set and retrieved correctly

            // 0. Setup
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);

            var config = new AnalysisConfig();

            // File settings
            var fileSettings = new AnalysisProperties
            {
                new Property() { Id = "file.1", Value = "file.value.1" },
                new Property() { Id = "file.2", Value = "file.value.2" }
            };
            var settingsFilePath = Path.Combine(testDir, "settings.txt");
            fileSettings.Save(settingsFilePath);

            // 1. Get path when not set -> null
            Assert.IsNull(config.GetSettingsFilePath(), "Expecting the settings file path to be null");

            // 2. Set and get
            config.SetSettingsFilePath(settingsFilePath);
            Assert.AreEqual(settingsFilePath, config.GetSettingsFilePath(), "Unexpected settings file path value returned");

            // 3. Check file properties are retrieved
            var provider = config.GetAnalysisSettings(false);
            provider.AssertExpectedPropertyCount(2);
            provider.AssertExpectedPropertyValue("file.1", "file.value.1");
            provider.AssertExpectedPropertyValue("file.2", "file.value.2");
        }

        [TestMethod]
        public void ConfigExt_GetAnalysisSettings_Precedence()
        {
            // Expected precedence: local -> file -> server

            // 0. Setup
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);

            var config = new AnalysisConfig();

            // File settings
            var fileSettings = new AnalysisProperties
            {
                new Property() { Id = "file.1", Value = "file.value.1" },
                new Property() { Id = "shared.property", Value = "shared value from file - should never be returned" },
                new Property() { Id = "shared.property2", Value = "shared value 2 from file" }
            };
            var settingsFilePath = Path.Combine(testDir, "settings.txt");
            fileSettings.Save(settingsFilePath);
            config.SetSettingsFilePath(settingsFilePath);

            // Local settings
            config.LocalSettings = new AnalysisProperties
            {
                new Property() { Id = "local.1", Value = "local.value.1" },
                new Property() { Id = "local.2", Value = "local.value.2" },
                new Property() { Id = "shared.property", Value = "shared value from local" }
            };

            // Server settings
            config.ServerSettings = new AnalysisProperties
            {
                new Property() { Id = "server.1", Value = "server.value.1" },
                new Property() { Id = "server.2", Value = "server.value.2" },
                new Property() { Id = "shared.property", Value = "shared value from server - should never be returned" },
                new Property() { Id = "shared.property2", Value = "shared value 2 from server - should never be returned" }
            };

            // 1. Precedence - local should win over file
            var provider = config.GetAnalysisSettings(false);
            provider.AssertExpectedPropertyCount(5);
            provider.AssertExpectedPropertyValue("local.1", "local.value.1");
            provider.AssertExpectedPropertyValue("local.2", "local.value.2");
            provider.AssertExpectedPropertyValue("file.1", "file.value.1");
            provider.AssertExpectedPropertyValue("shared.property", "shared value from local");
            provider.AssertExpectedPropertyValue("shared.property2", "shared value 2 from file");

            provider.AssertPropertyDoesNotExist("server.1");
            provider.AssertPropertyDoesNotExist("server.2");

            // 2. Server and non-server
            provider = config.GetAnalysisSettings(true);
            provider.AssertExpectedPropertyCount(7);
            provider.AssertExpectedPropertyValue("local.1", "local.value.1");
            provider.AssertExpectedPropertyValue("local.2", "local.value.2");
            provider.AssertExpectedPropertyValue("file.1", "file.value.1");
            provider.AssertExpectedPropertyValue("shared.property", "shared value from local");
            provider.AssertExpectedPropertyValue("shared.property2", "shared value 2 from file");
            provider.AssertExpectedPropertyValue("server.1", "server.value.1");
            provider.AssertExpectedPropertyValue("server.2", "server.value.2");
        }

        [TestMethod]
        public void ConfigExt_GetAnalysisSettings_NoSettings()
        {
            // 0. Setup
            var config = new AnalysisConfig();

            // 1. No server settings
            var provider = config.GetAnalysisSettings(false);
            Assert.IsNotNull(provider, "Returned provider should not be null");
            provider.AssertExpectedPropertyCount(0);

            // 2. With server settings
            provider = config.GetAnalysisSettings(true);
            Assert.IsNotNull(provider, "Returned provider should not be null");
            provider.AssertExpectedPropertyCount(0);
        }

        #endregion Tests
    }
}
