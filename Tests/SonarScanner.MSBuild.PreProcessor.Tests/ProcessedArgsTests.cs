/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Tests
{
    [TestClass]
    public class ProcessedArgsTests
    {
        private ProcessedArgs args;
        private TestLogger logger;

        [TestInitialize]
        public void TestInitialize()
        {
            // 0. Setup
            logger = new TestLogger();
            var cmdLineProps = new ListPropertiesProvider();
            cmdLineProps.AddProperty("cmd.key.1", "cmd value 1");
            cmdLineProps.AddProperty("shared.key.1", "shared cmd value");

            var fileProps = new ListPropertiesProvider();
            fileProps.AddProperty("file.key.1", "file value 1");
            fileProps.AddProperty("shared.key.1", "shared file value");
            fileProps.AddProperty("shared.key.2", "shared file value");

            var envProps = new ListPropertiesProvider();
            envProps.AddProperty("env.key.1", "env value 1");
            envProps.AddProperty("shared.key.1", "shared env value");
            envProps.AddProperty("shared.key.2", "shared env value");

            this.args = new ProcessedArgs("key", "branch", "ver", null, true, cmdLineProps, fileProps, envProps, logger);
        }

        #region Tests

        [TestMethod]
        public void ProcArgs_Organization()
        {
            this.args.Organization.Should().BeNull();
            this.args = new ProcessedArgs("key", "branch", "ver", "organization", true, new ListPropertiesProvider(), new ListPropertiesProvider(), new ListPropertiesProvider(), logger);
            this.args.Organization.Should().Be("organization");
        }

        [TestMethod]
        public void ProcArgs_GetSetting()
        {
            // 1. Throws on missing value
            Action act = () => this.args.GetSetting("missing.property");
            act.Should().ThrowExactly<InvalidOperationException>();

            // 2. Returns existing values
            this.args.GetSetting("cmd.key.1").Should().Be("cmd value 1");
            this.args.GetSetting("file.key.1").Should().Be("file value 1");
            this.args.GetSetting("env.key.1").Should().Be("env value 1");

            // 3. Precedence - command line properties should win
            this.args.GetSetting("shared.key.1").Should().Be("shared cmd value");

            // 4. Precedence - file wins over env
            this.args.GetSetting("shared.key.2").Should().Be("shared file value");

            // 5. Preprocessor only settings
            this.args.InstallLoaderTargets.Should().BeTrue();
        }

        [TestMethod]
        public void ProcArgs_TryGetSetting()
        {
            // 1. Missing key -> null
            this.args.TryGetSetting("missing.property", out string result).Should().BeFalse("Expecting false when the specified key does not exist");
            result.Should().BeNull("Expecting the value to be null when the specified key does not exist");

            // 2. Returns existing values
            this.args.TryGetSetting("cmd.key.1", out result).Should().BeTrue();
            result.Should().Be("cmd value 1");

            // 3. Precedence - command line properties should win
            this.args.GetSetting("shared.key.1").Should().Be("shared cmd value");

            // 4. Preprocessor only settings
            this.args.InstallLoaderTargets.Should().BeTrue();
        }

        [TestMethod]
        public void ProcArgs_GetSettingOrDefault()
        {
            // 1. Missing key -> default returned
            var result = this.args.GetSetting("missing.property", "default value");
            result.Should().Be("default value");

            // 2. Returns existing values
            result = this.args.GetSetting("file.key.1", "default value");
            result.Should().Be("file value 1");

            // 3. Precedence - command line properties should win
            this.args.GetSetting("shared.key.1", "default ValueType").Should().Be("shared cmd value");

            // 4. Preprocessor only settings
            this.args.InstallLoaderTargets.Should().BeTrue();
        }

        [TestMethod]
        public void ProcArgs_CmdLinePropertiesOverrideFileSettings()
        {
            // Checks command line properties override those from files

            // Arrange
            // The set of command line properties to supply
            var cmdLineProperties = new ListPropertiesProvider();
            cmdLineProperties.AddProperty("shared.key1", "cmd line value1 - should override server value");
            cmdLineProperties.AddProperty("cmd.line.only", "cmd line value4 - only on command line");
            cmdLineProperties.AddProperty("xxx", "cmd line value XXX - lower case");
            cmdLineProperties.AddProperty(SonarProperties.HostUrl, "http://host");

            // The set of file properties to supply
            var fileProperties = new ListPropertiesProvider();
            fileProperties.AddProperty("shared.key1", "file value1 - should be overridden");
            fileProperties.AddProperty("file.only", "file value3 - only in file");
            fileProperties.AddProperty("XXX", "file line value XXX - upper case");

            // Act
            var processedArgs = new ProcessedArgs("key", "branch", "version", null, false, cmdLineProperties, fileProperties, EmptyPropertyProvider.Instance, logger);

            AssertExpectedValue("shared.key1", "cmd line value1 - should override server value", processedArgs);
            AssertExpectedValue("cmd.line.only", "cmd line value4 - only on command line", processedArgs);
            AssertExpectedValue("file.only", "file value3 - only in file", processedArgs);
            AssertExpectedValue("xxx", "cmd line value XXX - lower case", processedArgs);
            AssertExpectedValue("XXX", "file line value XXX - upper case", processedArgs);
            AssertExpectedValue(SonarProperties.HostUrl, "http://host", processedArgs);
        }

        #endregion Tests

        #region Checks

        private static void AssertExpectedValue(string key, string expectedValue, ProcessedArgs args)
        {
            var found = args.TryGetSetting(key, out string actualValue);

            found.Should().BeTrue("Expected setting was not found. Key: {0}", key);
            actualValue.Should().Be(expectedValue, "Setting does not have the expected value. Key: {0}", key);
        }

        #endregion Checks
    }
}
