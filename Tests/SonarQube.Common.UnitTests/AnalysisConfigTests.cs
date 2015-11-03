//-----------------------------------------------------------------------
// <copyright file="AnalysisConfigTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class AnalysisConfigTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void AnalysisConfig_Serialization_InvalidFileName()
        {
            // 0. Setup
            AnalysisConfig config = new AnalysisConfig();

            // 1a. Missing file name - save
            AssertException.Expects<ArgumentNullException>(() => config.Save(null));
            AssertException.Expects<ArgumentNullException>(() => config.Save(string.Empty));
            AssertException.Expects<ArgumentNullException>(() => config.Save("\r\t "));

            // 1b. Missing file name - load
            AssertException.Expects<ArgumentNullException>(() => ProjectInfo.Load(null));
            AssertException.Expects<ArgumentNullException>(() => ProjectInfo.Load(string.Empty));
            AssertException.Expects<ArgumentNullException>(() => ProjectInfo.Load("\r\t "));
        }

        [TestMethod]
        [Description("Checks AnalysisConfig can be serialized and deserialized")]
        public void AnalysisConfig_Serialization_SaveAndReload()
        {
            // 0. Setup
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            AnalysisConfig originalConfig = new AnalysisConfig();
            originalConfig.SonarConfigDir = @"c:\config";
            originalConfig.SonarOutputDir = @"c:\output";
            originalConfig.SonarProjectKey = @"key.1.2";
            originalConfig.SonarProjectName = @"My project";
            originalConfig.SonarProjectVersion = @"1.0";


            originalConfig.LocalSettings = new AnalysisProperties();
            originalConfig.LocalSettings.Add(new Property() { Id = "local.key", Value = "local.value" });

            originalConfig.ServerSettings = new AnalysisProperties();
            originalConfig.ServerSettings.Add(new Property() { Id = "server.key", Value = "server.value" });

            string fileName = Path.Combine(testFolder, "config1.xml");

            SaveAndReloadConfig(originalConfig, fileName);
        }

        [TestMethod]
        [Description("Checks additional analysis settings can be serialized and deserialized")]
        public void AnalysisConfig_Serialization_AdditionalConfig()
        {
            // 0. Setup
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            AnalysisConfig originalConfig = new AnalysisConfig();

            // 1. Null list
            SaveAndReloadConfig(originalConfig, Path.Combine(testFolder, "AnalysisConfig_NullAdditionalSettings.xml"));

            // 2. Empty list
            originalConfig.AdditionalConfig = new List<ConfigSetting>();
            SaveAndReloadConfig(originalConfig, Path.Combine(testFolder, "AnalysisConfig_EmptyAdditionalSettings.xml"));

            // 3. Non-empty list
            originalConfig.AdditionalConfig.Add(new ConfigSetting() { Id = string.Empty, Value = string.Empty }); // empty item
            originalConfig.AdditionalConfig.Add(new ConfigSetting() { Id = "Id1", Value = "http://www.foo.xxx" });
            originalConfig.AdditionalConfig.Add(new ConfigSetting() { Id = "Id2", Value = "value 2" });
            SaveAndReloadConfig(originalConfig, Path.Combine(testFolder, "AnalysisConfig_NonEmptyList.xml"));
        }

        [TestMethod]
        [Description("Checks the serializer does not take an exclusive read lock")]
        [WorkItem(120)] // Regression test for http://jira.sonarsource.com/browse/SONARMSBRU-120
        public void AnalysisConfig_SharedReadAllowed()
        {
            // 0. Setup
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string filePath = Path.Combine(testFolder, "config.txt");

            AnalysisConfig config = new AnalysisConfig();
            config.Save(filePath);

            using (FileStream lockingStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                AnalysisConfig.Load(filePath);
            }
        }

        #endregion

        #region Helper methods

        private AnalysisConfig SaveAndReloadConfig(AnalysisConfig original, string outputFileName)
        {
            Assert.IsFalse(File.Exists(outputFileName), "Test error: file should not exist at the start of the test. File: {0}", outputFileName);
            original.Save(outputFileName);
            Assert.IsTrue(File.Exists(outputFileName), "Failed to create the output file. File: {0}", outputFileName);
            this.TestContext.AddResultFile(outputFileName);

            AnalysisConfig reloaded = AnalysisConfig.Load(outputFileName);
            Assert.IsNotNull(reloaded, "Reloaded analysis config should not be null");

            AssertExpectedValues(original, reloaded);
            return reloaded;
        }

        private static void AssertExpectedValues(AnalysisConfig expected, AnalysisConfig actual)
        {
            Assert.AreEqual(expected.SonarProjectKey, actual.SonarProjectKey, "Unexpected project key");
            Assert.AreEqual(expected.SonarProjectName, actual.SonarProjectName, "Unexpected project name");
            Assert.AreEqual(expected.SonarProjectVersion, actual.SonarProjectVersion, "Unexpected project version");

            Assert.AreEqual(expected.SonarConfigDir, actual.SonarConfigDir, "Unexpected config directory");
            Assert.AreEqual(expected.SonarOutputDir, actual.SonarOutputDir, "Unexpected output directory");

            CompareAdditionalSettings(expected, actual);
        }

        private static void CompareAdditionalSettings(AnalysisConfig expected, AnalysisConfig actual)
        {
            Assert.IsNotNull(actual.AdditionalConfig, "Not expecting the AdditionalSettings to be null for a reloaded file");

            if (expected.AdditionalConfig == null || expected.AdditionalConfig.Count == 0)
            {
                Assert.AreEqual(0, actual.AdditionalConfig.Count, "Not expecting any additional items. Count: {0}", actual.AdditionalConfig.Count);
                return;
            }

            foreach(ConfigSetting expectedSetting in expected.AdditionalConfig)
            {
                AssertSettingExists(expectedSetting.Id, expectedSetting.Value, actual);
            }
            Assert.AreEqual(expected.AdditionalConfig.Count, actual.AdditionalConfig.Count, "Unexpected number of additional settings");
        }

        private static void AssertSettingExists(string settingId, string expectedValue, AnalysisConfig actual)
        {
            Assert.IsNotNull(actual.AdditionalConfig, "Not expecting the additional settings to be null");

            ConfigSetting actualSetting = actual.AdditionalConfig.FirstOrDefault(s => string.Equals(settingId, s.Id, StringComparison.InvariantCultureIgnoreCase));
            Assert.IsNotNull(actualSetting, "Expected setting not found: {0}", settingId);
            Assert.AreEqual(expectedValue, actualSetting.Value, "Setting does not have the expected value. SettingId: {0}", settingId);
        }


        #endregion
    }
}
