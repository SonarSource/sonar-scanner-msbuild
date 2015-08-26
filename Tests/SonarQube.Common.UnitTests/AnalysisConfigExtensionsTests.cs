//-----------------------------------------------------------------------
// <copyright file="AnalysisConfigExtensionsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using TestUtilities;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class AnalysisConfigExtensionsTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [Description("Checks the extension methods for getting and setting values")]
        public void ConfigExt_GetAndSet()
        {
            // 0. Setup
            AnalysisConfig config = new AnalysisConfig();

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
            AnalysisConfig config = new AnalysisConfig();

            config.LocalSettings = new AnalysisProperties();
            config.LocalSettings.Add(new Property() { Id = "local.1", Value = "local.value.1" });
            config.LocalSettings.Add(new Property() { Id = "local.2", Value = "local.value.2" });

            // 1. Local only
            IAnalysisPropertyProvider localProperties = config.GetAnalysisSettings(false);
            localProperties.AssertExpectedPropertyCount(2);
            localProperties.AssertExpectedPropertyValue("local.1", "local.value.1");
            localProperties.AssertExpectedPropertyValue("local.2", "local.value.2");

            // 2. Local and server
            IAnalysisPropertyProvider allProperties = config.GetAnalysisSettings(true);
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
            AnalysisConfig config = new AnalysisConfig();

            config.ServerSettings = new AnalysisProperties();
            config.ServerSettings.Add(new Property() { Id = "server.1", Value = "server.value.1" });
            config.ServerSettings.Add(new Property() { Id = "server.2", Value = "server.value.2" });

            // 1. Local only
            IAnalysisPropertyProvider localProperties = config.GetAnalysisSettings(false);
            localProperties.AssertExpectedPropertyCount(0);

            localProperties.AssertPropertyDoesNotExist("server.1");
            localProperties.AssertPropertyDoesNotExist("server.2");

            // 2. Local and server
            IAnalysisPropertyProvider allProperties = config.GetAnalysisSettings(true);
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
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            AnalysisConfig config = new AnalysisConfig();

            // File settings
            AnalysisProperties fileSettings = new AnalysisProperties();
            fileSettings.Add(new Property() { Id = "file.1", Value = "file.value.1" });
            fileSettings.Add(new Property() { Id = "file.2", Value = "file.value.2" });
            string settingsFilePath = Path.Combine(testDir, "settings.txt");
            fileSettings.Save(settingsFilePath);

            // 1. Get path when not set -> null
            Assert.IsNull(config.GetSettingsFilePath(), "Expecting the settings file path to be null");

            // 2. Set and get
            config.SetSettingsFilePath(settingsFilePath);
            Assert.AreEqual(settingsFilePath, config.GetSettingsFilePath(), "Unexpected settings file path value returned");

            // 3. Check file properties are retrieved
            IAnalysisPropertyProvider provider = config.GetAnalysisSettings(false);
            provider.AssertExpectedPropertyCount(2);
            provider.AssertExpectedPropertyValue("file.1", "file.value.1");
            provider.AssertExpectedPropertyValue("file.2", "file.value.2");
        }

        [TestMethod]
        public void ConfigExt_GetAnalysisSettings_Precedence()
        {
            // Expected precedence: local -> file -> server

            // 0. Setup
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            AnalysisConfig config = new AnalysisConfig();

            // File settings
            AnalysisProperties fileSettings = new AnalysisProperties();
            fileSettings.Add(new Property() { Id = "file.1", Value = "file.value.1" });
            fileSettings.Add(new Property() { Id = "shared.property", Value = "shared value from file - should never be returned" });
            fileSettings.Add(new Property() { Id = "shared.property2", Value = "shared value 2 from file" });
            string settingsFilePath = Path.Combine(testDir, "settings.txt");
            fileSettings.Save(settingsFilePath);
            config.SetSettingsFilePath(settingsFilePath);

            // Local settings
            config.LocalSettings = new AnalysisProperties();
            config.LocalSettings.Add(new Property() { Id = "local.1", Value = "local.value.1" });
            config.LocalSettings.Add(new Property() { Id = "local.2", Value = "local.value.2" });
            config.LocalSettings.Add(new Property() { Id = "shared.property", Value = "shared value from local" });

            // Server settings
            config.ServerSettings = new AnalysisProperties();
            config.ServerSettings.Add(new Property() { Id = "server.1", Value = "server.value.1" });
            config.ServerSettings.Add(new Property() { Id = "server.2", Value = "server.value.2" });
            config.ServerSettings.Add(new Property() { Id = "shared.property", Value = "shared value from server - should never be returned" });
            config.ServerSettings.Add(new Property() { Id = "shared.property2", Value = "shared value 2 from server - should never be returned" });


            // 1. Precedence - local should win over file
            IAnalysisPropertyProvider provider = config.GetAnalysisSettings(false);
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
            AnalysisConfig config = new AnalysisConfig();

            // 1. No server settings
            IAnalysisPropertyProvider provider = config.GetAnalysisSettings(false);
            Assert.IsNotNull(provider, "Returned provider should not be null");
            provider.AssertExpectedPropertyCount(0);

            // 2. With server settings
            provider = config.GetAnalysisSettings(true);
            Assert.IsNotNull(provider, "Returned provider should not be null");
            provider.AssertExpectedPropertyCount(0);
        }

        #endregion
    }
}
