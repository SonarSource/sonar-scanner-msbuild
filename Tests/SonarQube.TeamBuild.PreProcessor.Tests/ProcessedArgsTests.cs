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
using System;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class ProcessedArgsTests
    {
        private ProcessedArgs args;

        [TestInitialize]
        public void TestInitialize()
        {
            // 0. Setup
            ListPropertiesProvider cmdLineProps = new ListPropertiesProvider();
            cmdLineProps.AddProperty("cmd.key.1", "cmd value 1");
            cmdLineProps.AddProperty("shared.key.1", "shared cmd value");

            ListPropertiesProvider fileProps = new ListPropertiesProvider();
            fileProps.AddProperty("file.key.1", "file value 1");
            fileProps.AddProperty("shared.key.1", "shared file value");
            fileProps.AddProperty("shared.key.2", "shared file value");

            ListPropertiesProvider envProps = new ListPropertiesProvider();
            envProps.AddProperty("env.key.1", "env value 1");
            envProps.AddProperty("shared.key.1", "shared env value");
            envProps.AddProperty("shared.key.2", "shared env value");

            args = new ProcessedArgs("key", "branch", "ver", null, true, cmdLineProps, fileProps, envProps);
        }

        #region Tests

        [TestMethod]
        public void ProcArgs_Organization()
        {
            Assert.IsNull(args.Organization);
            args = new ProcessedArgs("key", "branch", "ver", "organization", true, new ListPropertiesProvider(), new ListPropertiesProvider(), new ListPropertiesProvider());
            Assert.AreEqual("organization", args.Organization);
        }

        [TestMethod]
        public void ProcArgs_GetSetting()
        {
            // 1. Throws on missing value
            AssertException.Expects<InvalidOperationException>(() => args.GetSetting("missing.property"));

            // 2. Returns existing values
            Assert.AreEqual("cmd value 1", args.GetSetting("cmd.key.1"));
            Assert.AreEqual("file value 1", args.GetSetting("file.key.1"));
            Assert.AreEqual("env value 1", args.GetSetting("env.key.1"));

            // 3. Precedence - command line properties should win
            Assert.AreEqual("shared cmd value", args.GetSetting("shared.key.1"));

            // 4. Precedence - file wins over env
            Assert.AreEqual("shared file value", args.GetSetting("shared.key.2"));

            // 5. Preprocessor only settings
            Assert.AreEqual(true, args.InstallLoaderTargets);
        }

        [TestMethod]
        public void ProcArgs_TryGetSetting()
        {
            // 1. Missing key -> null
            string result;
            Assert.IsFalse(args.TryGetSetting("missing.property", out result), "Expecting false when the specified key does not exist");
            Assert.IsNull(result, "Expecting the value to be null when the specified key does not exist");

            // 2. Returns existing values
            Assert.IsTrue(args.TryGetSetting("cmd.key.1", out result));
            Assert.AreEqual("cmd value 1", result);

            // 3. Precedence - command line properties should win
            Assert.AreEqual("shared cmd value", args.GetSetting("shared.key.1"));

            // 4. Preprocessor only settings
            Assert.AreEqual(true, args.InstallLoaderTargets);
        }

        [TestMethod]
        public void ProcArgs_GetSettingOrDefault()
        {
            // 1. Missing key -> default returned
            string result = args.GetSetting("missing.property", "default value");
            Assert.AreEqual("default value", result);

            // 2. Returns existing values
            result = args.GetSetting("file.key.1", "default value");
            Assert.AreEqual("file value 1", result);

            // 3. Precedence - command line properties should win
            Assert.AreEqual("shared cmd value", args.GetSetting("shared.key.1", "default ValueType"));

            // 4. Preprocessor only settings
            Assert.AreEqual(true, args.InstallLoaderTargets);
        }

        [TestMethod]
        public void ProcArgs_CmdLinePropertiesOverrideFileSettings()
        {
            // Checks command line properties override those from files

            // Arrange
            // The set of command line properties to supply
            ListPropertiesProvider cmdLineProperties = new ListPropertiesProvider();
            cmdLineProperties.AddProperty("shared.key1", "cmd line value1 - should override server value");
            cmdLineProperties.AddProperty("cmd.line.only", "cmd line value4 - only on command line");
            cmdLineProperties.AddProperty("xxx", "cmd line value XXX - lower case");
            cmdLineProperties.AddProperty(SonarProperties.HostUrl, "http://host");

            // The set of file properties to supply
            ListPropertiesProvider fileProperties = new ListPropertiesProvider();
            fileProperties.AddProperty("shared.key1", "file value1 - should be overridden");
            fileProperties.AddProperty("file.only", "file value3 - only in file");
            fileProperties.AddProperty("XXX", "file line value XXX - upper case");

            // Act
            ProcessedArgs args = new ProcessedArgs("key", "branch", "version", null, false, cmdLineProperties, fileProperties, EmptyPropertyProvider.Instance);

            AssertExpectedValue("shared.key1", "cmd line value1 - should override server value", args);
            AssertExpectedValue("cmd.line.only", "cmd line value4 - only on command line", args);
            AssertExpectedValue("file.only", "file value3 - only in file", args);
            AssertExpectedValue("xxx", "cmd line value XXX - lower case", args);
            AssertExpectedValue("XXX", "file line value XXX - upper case", args);
            AssertExpectedValue(SonarProperties.HostUrl, "http://host", args);
        }

        #endregion

        #region Checks

        private static void AssertExpectedValue(string key, string expectedValue, ProcessedArgs args)
        {
            string actualValue;
            bool found = args.TryGetSetting(key, out actualValue);

            Assert.IsTrue(found, "Expected setting was not found. Key: {0}", key);
            Assert.AreEqual(expectedValue, actualValue, "Setting does not have the expected value. Key: {0}", key);
        }

        #endregion
    }
}
