//-----------------------------------------------------------------------
// <copyright file="AnalysisConfigTests.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;

namespace Sonar.Common.UnitTests
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

            Guid projectGuid = Guid.NewGuid();

            AnalysisConfig originalConfig = new AnalysisConfig();
            originalConfig.SonarConfigDir = @"c:\config";
            originalConfig.SonarOutputDir = @"c:\output";
            originalConfig.SonarProjectKey = @"key.1.2";
            originalConfig.SonarProjectName = @"My project";
            originalConfig.SonarProjectVersion = @"1.0";

            string fileName = Path.Combine(testFolder, "config1.xml");

            SaveAndReloadConfig(originalConfig, fileName);
        }

        [TestMethod]
        [Description("Checks additional analysis settings can be serialized and deserialized")]
        public void ProjectInfo_Serialization_AdditionalSettings()
        {
            // 0. Setup
            string testFolder = TestUtils.CreateTestSpecificFolder(this.TestContext);

            AnalysisConfig originalConfig = new AnalysisConfig();

            // 1. Null list
            SaveAndReloadConfig(originalConfig, Path.Combine(testFolder, "AnalysisConfig_NullAdditionalSettings.xml"));

            // 2. Empty list
            originalConfig.AdditionalSettings = new List<AnalysisSetting>();
            SaveAndReloadConfig(originalConfig, Path.Combine(testFolder, "AnalysisConfig_EmptyAdditionalSettings.xml"));

            // 3. Non-empty list
            originalConfig.AdditionalSettings.Add(new AnalysisSetting() { Id = string.Empty, Value = string.Empty }); // empty item
            originalConfig.AdditionalSettings.Add(new AnalysisSetting() { Id = "Id1", Value = "http://www.foo.xxx" });
            originalConfig.AdditionalSettings.Add(new AnalysisSetting() { Id = "Id2", Value = "value 2" });
            SaveAndReloadConfig(originalConfig, Path.Combine(testFolder, "AnalysisConfig_NonEmptyList.xml"));
        }


        [TestMethod]
        [Description("Checks the extension methods for getting and setting values")]
        public void ProjectInfo_ExtensionMethods_GetAndSet()
        {
            // 0. Setup
            AnalysisConfig config = new AnalysisConfig();

            AnalysisSetting setting;
            string result;

            // 1. Get/TryGet missing setting
            result = config.GetSetting("missing", "123");

            Assert.IsFalse(config.TryGetSetting("missing", out setting), "Setting should not have been found");
            Assert.AreEqual("123", config.GetSetting("missing", "123"), "Expecting the default setting to be returned");

            // 2. Set and get a previously new setting
            config.SetValue("id1", "value1");
            Assert.IsTrue(config.TryGetSetting("id1", out setting), "Setting should have been found");
            Assert.AreEqual("value1", setting.Value, "Unexpected value returned for setting");
            Assert.AreEqual("value1", config.GetSetting("id1", "123"), "Unexpected value returned for setting");

            // 3. Update and refetch the setting
            config.SetValue("id1", "updated value");
            Assert.IsTrue(config.TryGetSetting("id1", out setting), "Setting should have been found");
            Assert.AreEqual("updated value", setting.Value, "Unexpected value returned for setting");
            Assert.AreEqual("updated value", config.GetSetting("id1", "123"), "Unexpected value returned for setting");        
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
            Assert.IsNotNull(actual.AdditionalSettings, "Not expecting the AdditionalSettings to be null for a reloaded file");

            if (expected.AdditionalSettings == null || expected.AdditionalSettings.Count == 0)
            {
                Assert.AreEqual(0, actual.AdditionalSettings.Count, "Not expecting any additional items. Count: {0}", actual.AdditionalSettings.Count);
                return;
            }

            foreach(AnalysisSetting expectedSetting in expected.AdditionalSettings)
            {
                AssertSettingExists(expectedSetting.Id, expectedSetting.Value, actual);
            }
            Assert.AreEqual(expected.AdditionalSettings.Count, actual.AdditionalSettings.Count, "Unexpected number of additional settings");
        }

        private static void AssertSettingExists(string settingId, string expectedValue, AnalysisConfig actual)
        {
            Assert.IsNotNull(actual.AdditionalSettings, "Not expecting the additional settings to be null");

            AnalysisSetting actualSetting = actual.AdditionalSettings.FirstOrDefault(s => string.Equals(settingId, s.Id, StringComparison.InvariantCultureIgnoreCase));
            Assert.IsNotNull(actualSetting, "Expected setting not found: {0}", settingId);
            Assert.AreEqual(expectedValue, actualSetting.Value, "Setting does not have the expected value. SettingId: {0}", settingId);
        }


        #endregion
    }
}
