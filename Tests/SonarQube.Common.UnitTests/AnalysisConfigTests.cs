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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            var config = new AnalysisConfig();

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
            var testFolder = TestUtils.CreateTestSpecificFolder(TestContext);

            var originalConfig = new AnalysisConfig
            {
                SonarConfigDir = @"c:\config",
                SonarOutputDir = @"c:\output",
                SonarProjectKey = @"key.1.2",
                SonarProjectName = @"My project",
                SonarProjectVersion = @"1.0",

                LocalSettings = new AnalysisProperties()
            };
            originalConfig.LocalSettings.Add(new Property() { Id = "local.key", Value = "local.value" });

            originalConfig.ServerSettings = new AnalysisProperties
            {
                new Property() { Id = "server.key", Value = "server.value" }
            };

            var settings = new AnalyzerSettings
            {
                RuleSetFilePath = "ruleset path",

                AdditionalFilePaths = new List<string>()
            };
            settings.AdditionalFilePaths.Add("additional path1");
            settings.AdditionalFilePaths.Add("additional path2");

            settings.AnalyzerAssemblyPaths = new List<string>
            {
                "analyzer path1",
                "analyzer path2"
            };

            originalConfig.AnalyzersSettings = new List<AnalyzerSettings>
            {
                settings
            };

            var fileName = Path.Combine(testFolder, "config1.xml");

            SaveAndReloadConfig(originalConfig, fileName);
        }

        [TestMethod]
        [Description("Checks AnalysisConfig can be serialized and deserialized with missing values and empty collections")]
        public void AnalysisConfig_Serialization_SaveAndReload_EmptySettings()
        {
            // Arrange
            var testFolder = TestUtils.CreateTestSpecificFolder(TestContext);

            var originalConfig = new AnalysisConfig();
            var fileName = Path.Combine(testFolder, "empty_config.xml");

            // Act and assert
            SaveAndReloadConfig(originalConfig, fileName);
        }

        [TestMethod]
        [Description("Checks additional analysis settings can be serialized and deserialized")]
        public void AnalysisConfig_Serialization_AdditionalConfig()
        {
            // 0. Setup
            var testFolder = TestUtils.CreateTestSpecificFolder(TestContext);

            var originalConfig = new AnalysisConfig();

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
            var testFolder = TestUtils.CreateTestSpecificFolder(TestContext);
            var filePath = Path.Combine(testFolder, "config.txt");

            var config = new AnalysisConfig();
            config.Save(filePath);

            using (var lockingStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                AnalysisConfig.Load(filePath);
            }
        }

        [TestMethod]
        [Description("Checks that the XML uses the expected element and attribute names, and that unrecognised elements are silently ignored")]
        public void AnalysisConfig_ExpectedXmlFormat()
        {
            // Arrange
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<AnalysisConfig xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""http://www.sonarsource.com/msbuild/integration/2015/1"">
  <SonarConfigDir>c:\config</SonarConfigDir>
  <SonarOutputDir>c:\output</SonarOutputDir>
  <SonarProjectKey>key.1.2</SonarProjectKey>
  <SonarProjectVersion>1.0</SonarProjectVersion>
  <SonarProjectName>My project</SonarProjectName>
  <ServerSettings>
    <Property Name=""server.key"">server.value</Property>
  </ServerSettings>

  <!-- Unexpected additional elements should be silently ignored -->
  <UnexpectedElement1 />

  <LocalSettings>
    <Property Name=""local.key"">local.value</Property>
  </LocalSettings>
  <AnalyzersSettings>
    <AnalyzerSettings>
      <RuleSetFilePath>d:\ruleset path.ruleset</RuleSetFilePath>
      <AnalyzerAssemblyPaths>
        <Path>c:\analyzer1.dll</Path>
      </AnalyzerAssemblyPaths>
      <AdditionalFilePaths>

        <MoreUnexpectedData><Foo /></MoreUnexpectedData>

        <Path>c:\additional1.txt</Path>
      </AdditionalFilePaths>
    </AnalyzerSettings>
  </AnalyzersSettings>
</AnalysisConfig>";

            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var fullPath = TestUtils.CreateTextFile(testDir, "input.txt", xml);

            // Act
            var actual = AnalysisConfig.Load(fullPath);

            // Assert
            var expected = new AnalysisConfig
            {
                SonarConfigDir = "c:\\config",
                SonarOutputDir = "c:\\output",
                SonarProjectKey = "key.1.2",
                SonarProjectVersion = "1.0",
                SonarProjectName = "My project",
                ServerSettings = new AnalysisProperties()
            };
            expected.ServerSettings.Add(new Property() { Id = "server.key", Value = "server.value" });
            expected.LocalSettings = new AnalysisProperties
            {
                new Property() { Id = "local.key", Value = "local.value" }
            };

            var settings = new AnalyzerSettings();

            settings = new AnalyzerSettings
            {
                RuleSetFilePath = "d:\\ruleset path.ruleset",
                AdditionalFilePaths = new List<string>()
            };
            settings.AdditionalFilePaths.Add("c:\\additional1.txt");
            settings.AnalyzerAssemblyPaths = new List<string>
            {
                "c:\\analyzer1.dll"
            };

            expected.AnalyzersSettings = new List<AnalyzerSettings>
            {
                settings
            };

            AssertExpectedValues(expected, actual);
        }

        #endregion Tests

        #region Helper methods

        private AnalysisConfig SaveAndReloadConfig(AnalysisConfig original, string outputFileName)
        {
            Assert.IsFalse(File.Exists(outputFileName), "Test error: file should not exist at the start of the test. File: {0}", outputFileName);
            original.Save(outputFileName);
            Assert.IsTrue(File.Exists(outputFileName), "Failed to create the output file. File: {0}", outputFileName);
            TestContext.AddResultFile(outputFileName);

            var reloaded = AnalysisConfig.Load(outputFileName);
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

            CompareAnalyzerSettings(expected.AnalyzersSettings, actual.AnalyzersSettings);
        }

        private static void CompareAdditionalSettings(AnalysisConfig expected, AnalysisConfig actual)
        {
            // The XmlSerializer should create an empty list
            Assert.IsNotNull(actual.AdditionalConfig, "Not expecting the AdditionalSettings to be null for a reloaded file");

            if (expected.AdditionalConfig == null || expected.AdditionalConfig.Count == 0)
            {
                Assert.AreEqual(0, actual.AdditionalConfig.Count, "Not expecting any additional items. Count: {0}", actual.AdditionalConfig.Count);
                return;
            }

            foreach(var expectedSetting in expected.AdditionalConfig)
            {
                AssertSettingExists(expectedSetting.Id, expectedSetting.Value, actual);
            }
            Assert.AreEqual(expected.AdditionalConfig.Count, actual.AdditionalConfig.Count, "Unexpected number of additional settings");
        }

        private static void AssertSettingExists(string settingId, string expectedValue, AnalysisConfig actual)
        {
            Assert.IsNotNull(actual.AdditionalConfig, "Not expecting the additional settings to be null");

            var actualSetting = actual.AdditionalConfig.FirstOrDefault(s => string.Equals(settingId, s.Id, StringComparison.InvariantCultureIgnoreCase));
            Assert.IsNotNull(actualSetting, "Expected setting not found: {0}", settingId);
            Assert.AreEqual(expectedValue, actualSetting.Value, "Setting does not have the expected value. SettingId: {0}", settingId);
        }

        private static void CompareAnalyzerSettings(IList<AnalyzerSettings> expectedList, IList<AnalyzerSettings> actualList)
        {
            Assert.IsNotNull(actualList, "Not expecting the AnalyzersSettings to be null for a reloaded file");

            if (expectedList == null)
            {
                Assert.IsTrue(actualList.Count == 0, "Expecting the reloaded analyzers settings to be empty");
                return;
            }

            Assert.IsNotNull(actualList, "Not expecting the actual analyzers settings to be null for a reloaded file");

            Assert.AreEqual(expectedList.Count, actualList.Count, "Expecting number of analyzer settings to be the same");

            for(var i = 0; i < actualList.Count; i++)
            {
                var actual = actualList[i];
                var expected = expectedList[i];

                Assert.AreEqual(expected.RuleSetFilePath, actual.RuleSetFilePath, "Unexpected Ruleset value");

                CollectionAssert.AreEqual(expected.AnalyzerAssemblyPaths, actual.AnalyzerAssemblyPaths, "Analyzer assembly paths do not match");
                CollectionAssert.AreEqual(expected.AdditionalFilePaths, actual.AdditionalFilePaths, "Additional file paths do not match");
            }
        }

        #endregion Helper methods
    }
}
