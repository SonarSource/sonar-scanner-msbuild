//-----------------------------------------------------------------------
// <copyright file="AnalysisConfigExtensionsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class AnalysisConfigExtensionsTests
    {
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
        public void ConfigExt_GetAnalysisSettings()
        {
            // 0. Setup
            AnalysisConfig config = new AnalysisConfig();

            config.LocalSettings = new AnalysisProperties();
            config.LocalSettings.Add(new Property() { Id = "local.1", Value = "local.value.1" });
            config.LocalSettings.Add(new Property() { Id = "local.2", Value = "local.value.2" });
            config.LocalSettings.Add(new Property() { Id = "shared.property", Value = "shared value from local" });

            config.ServerSettings = new AnalysisProperties();
            config.ServerSettings.Add(new Property() { Id = "server.1", Value = "server.value.1" });
            config.ServerSettings.Add(new Property() { Id = "server.2", Value = "server.value.2" });
            config.ServerSettings.Add(new Property() { Id = "shared.property", Value = "shared value from server - should never be returned" });

            // 1. Local only
            IAnalysisPropertyProvider localProperties = config.GetAnalysisSettings(false);
            localProperties.AssertExpectedPropertyCount(3);
            localProperties.AssertExpectedPropertyValue("local.1", "local.value.1");
            localProperties.AssertExpectedPropertyValue("local.2", "local.value.2");
            localProperties.AssertExpectedPropertyValue("shared.property", "shared value from local");

            localProperties.AssertPropertyDoesNotExist("server.1");
            localProperties.AssertPropertyDoesNotExist("server.2");

            // 2. Server too
            IAnalysisPropertyProvider allProperties = config.GetAnalysisSettings(true);
            allProperties.AssertExpectedPropertyCount(5);
            allProperties.AssertExpectedPropertyValue("local.1", "local.value.1");
            allProperties.AssertExpectedPropertyValue("local.2", "local.value.2");
            allProperties.AssertExpectedPropertyValue("shared.property", "shared value from local");
            allProperties.AssertExpectedPropertyValue("server.1", "server.value.1");
            allProperties.AssertExpectedPropertyValue("server.2", "server.value.2");
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
