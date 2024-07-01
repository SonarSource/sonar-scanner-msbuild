/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using NSubstitute;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
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
            args = CreateDefaultArgs(cmdLineProps, fileProps, envProps, organization: null);
        }

        [TestMethod]
        public void ProcArgs_ParameterThrow_Key()
        {
            var action = () => CreateDefaultArgs(key: null);
            action.Should().Throw<ArgumentNullException>().WithParameterName("key");
        }

        [TestMethod]
        public void ProcArgs_ParameterThrow_CmdLineProperties()
        {
            var action = () => new ProcessedArgs(
                "key",
                "name",
                "version",
                "org",
                true,
                cmdLineProperties: null,
                EmptyPropertyProvider.Instance,
                EmptyPropertyProvider.Instance,
                Substitute.For<IFileWrapper>(),
                Substitute.For<IDirectoryWrapper>(),
                CreateOperatingSystemProvider(),
                logger);
            action.Should().Throw<ArgumentNullException>().WithParameterName("cmdLineProperties");
        }

        [TestMethod]
        public void ProcArgs_ParameterThrow_GlobalFileProperties()
        {
            var action = () => new ProcessedArgs(
                "key",
                "name",
                "version",
                "org",
                true,
                EmptyPropertyProvider.Instance,
                globalFileProperties: null,
                EmptyPropertyProvider.Instance,
                Substitute.For<IFileWrapper>(),
                Substitute.For<IDirectoryWrapper>(),
                CreateOperatingSystemProvider(),
                logger);
            action.Should().Throw<ArgumentNullException>().WithParameterName("globalFileProperties");
        }

        [TestMethod]
        public void ProcArgs_ParameterThrow_ScannerEnvProperties()
        {
            var action = () => new ProcessedArgs(
                "key",
                "name",
                "version",
                "org",
                true,
                EmptyPropertyProvider.Instance,
                EmptyPropertyProvider.Instance,
                scannerEnvProperties: null,
                Substitute.For<IFileWrapper>(),
                Substitute.For<IDirectoryWrapper>(),
                CreateOperatingSystemProvider(),
                logger);
            action.Should().Throw<ArgumentNullException>().WithParameterName("scannerEnvProperties");
        }

        [TestMethod]
        public void ProcArgs_Organization()
        {
            args.Organization.Should().BeNull();
            args = CreateDefaultArgs(organization: "organization");
            args.Organization.Should().Be("organization");
        }

        [TestMethod]
        public void ProcArgs_GetSetting()
        {
            // 1. Throws on missing value
            Action act = () => args.GetSetting("missing.property");
            act.Should().ThrowExactly<InvalidOperationException>();

            // 2. Returns existing values
            args.GetSetting("cmd.key.1").Should().Be("cmd value 1");
            args.GetSetting("file.key.1").Should().Be("file value 1");
            args.GetSetting("env.key.1").Should().Be("env value 1");

            // 3. Precedence - command line properties should win
            args.GetSetting("shared.key.1").Should().Be("shared cmd value");

            // 4. Precedence - file wins over env
            args.GetSetting("shared.key.2").Should().Be("shared file value");

            // 5. Preprocessor only settings
            args.InstallLoaderTargets.Should().BeTrue();
        }

        [TestMethod]
        public void ProcArgs_TryGetSetting()
        {
            // 1. Missing key -> null
            args.TryGetSetting("missing.property", out var result).Should().BeFalse("Expecting false when the specified key does not exist");
            result.Should().BeNull("Expecting the value to be null when the specified key does not exist");

            // 2. Returns existing values
            args.TryGetSetting("cmd.key.1", out result).Should().BeTrue();
            result.Should().Be("cmd value 1");

            // 3. Precedence - command line properties should win
            args.GetSetting("shared.key.1").Should().Be("shared cmd value");

            // 4. Preprocessor only settings
            args.InstallLoaderTargets.Should().BeTrue();
        }

        [TestMethod]
        public void ProcArgs_GetSettingOrDefault()
        {
            // 1. Missing key -> default returned
            var result = args.GetSetting("missing.property", "default value");
            result.Should().Be("default value");

            // 2. Returns existing values
            result = args.GetSetting("file.key.1", "default value");
            result.Should().Be("file value 1");

            // 3. Precedence - command line properties should win
            args.GetSetting("shared.key.1", "default ValueType").Should().Be("shared cmd value");

            // 4. Preprocessor only settings
            args.InstallLoaderTargets.Should().BeTrue();
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
            var processedArgs = CreateDefaultArgs(cmdLineProperties, fileProperties);

            AssertExpectedValue("shared.key1", "cmd line value1 - should override server value", processedArgs);
            AssertExpectedValue("cmd.line.only", "cmd line value4 - only on command line", processedArgs);
            AssertExpectedValue("file.only", "file value3 - only in file", processedArgs);
            AssertExpectedValue("xxx", "cmd line value XXX - lower case", processedArgs);
            AssertExpectedValue("XXX", "file line value XXX - upper case", processedArgs);
            AssertExpectedValue(SonarProperties.HostUrl, "http://host", processedArgs);
            processedArgs.IsValid.Should().BeTrue();
        }

        [TestMethod]
        public void ProcArgs_HostUrl_SonarcloudUrl_HostUrlSet()
        {
            var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.HostUrl, "http://host")]));

            sut.ServerInfo.Should().NotBeNull();
            sut.ServerInfo.IsSonarCloud.Should().BeFalse();
            sut.ServerInfo.ServerUrl.Should().Be("http://host");
            logger.Warnings.Should().BeEmpty();
            logger.Errors.Should().BeEmpty();
            sut.IsValid.Should().BeTrue();
        }

        [TestMethod]
        public void ProcArgs_HostUrl_SonarcloudUrl_SonarcloudUrlSet()
        {
            var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.SonarcloudUrl, "https://sonarcloud.proxy")]));

            sut.ServerInfo.Should().NotBeNull();
            sut.ServerInfo.IsSonarCloud.Should().BeTrue();
            sut.ServerInfo.ServerUrl.Should().Be("https://sonarcloud.proxy");
            logger.Warnings.Should().BeEmpty();
            logger.Errors.Should().BeEmpty();
            sut.IsValid.Should().BeTrue();
        }

        [TestMethod]
        public void ProcArgs_HostUrl_SonarcloudUrl_HostUrlAndSonarcloudUrlAreIdentical()
        {
            var sut = CreateDefaultArgs(new ListPropertiesProvider([
                new Property(SonarProperties.HostUrl, "https://sonarcloud.proxy"), new Property(SonarProperties.SonarcloudUrl, "https://sonarcloud.proxy")
            ]));

            sut.ServerInfo.Should().NotBeNull();
            sut.ServerInfo.IsSonarCloud.Should().BeTrue();
            sut.ServerInfo.ServerUrl.Should().Be("https://sonarcloud.proxy");
            logger.AssertWarningLogged("The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set. Please set only 'sonar.scanner.sonarcloudUrl'.");
            logger.Errors.Should().BeEmpty();
            sut.IsValid.Should().BeTrue();
        }

        [TestMethod]
        public void ProcArgs_HostUrl_SonarcloudUrl_HostUrlAndSonarcloudUrlDiffer()
        {
            var sut = CreateDefaultArgs(new ListPropertiesProvider([
                new Property(SonarProperties.HostUrl, "https://someHost.com"), new Property(SonarProperties.SonarcloudUrl, "https://someOtherHost.org")
            ]));

            sut.ServerInfo.Should().BeNull();
            logger.Warnings.Should().BeEmpty();
            logger.AssertErrorLogged("The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set and are different. " +
                "Please set either 'sonar.host.url' for SonarQube or 'sonar.scanner.sonarcloudUrl' for SonarCloud.");
            sut.IsValid.Should().BeFalse();
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(null)]
        [DataRow("   ")]
        public void ProcArgs_HostUrl_SonarcloudUrl_HostUrlAndSonarcloudUrlEmpty(string empty)
        {
            var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.HostUrl, empty), new Property(SonarProperties.SonarcloudUrl, empty),]));

            sut.ServerInfo.Should().BeNull();
            logger.Warnings.Should().BeEmpty();
            logger.AssertErrorLogged("The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set to an invalid value.");
            sut.IsValid.Should().BeFalse();
        }

        [TestMethod]
        public void ProcArgs_HostUrl_SonarcloudUrl_HostUrlAndSonarcloudUrlMissing()
        {
            var sut = CreateDefaultArgs(EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance);

            sut.ServerInfo.Should().NotBeNull();
            sut.ServerInfo.IsSonarCloud.Should().BeTrue();
            sut.ServerInfo.ServerUrl.Should().Be("https://sonarcloud.io");
            logger.Warnings.Should().BeEmpty();
            logger.Errors.Should().BeEmpty();
            sut.IsValid.Should().BeTrue();
        }

        [TestMethod]
        public void ProcArgs_HostUrl_SonarcloudUrl_HostUrlIsDefaultSonarcloud()
        {
            var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.HostUrl, "https://sonarcloud.io")]));

            sut.ServerInfo.Should().NotBeNull();
            sut.ServerInfo.IsSonarCloud.Should().BeTrue();
            sut.ServerInfo.ServerUrl.Should().Be("https://sonarcloud.io");
            logger.Warnings.Should().BeEmpty();
            logger.Errors.Should().BeEmpty();
            sut.IsValid.Should().BeTrue();
        }

        [TestMethod]
        public void ProcArgs_HostUrl_SonarcloudUrl_PropertyAggregation()
        {
            var sut = CreateDefaultArgs(
                new ListPropertiesProvider([new Property(SonarProperties.HostUrl, "https://localhost")]),
                new ListPropertiesProvider([new Property(SonarProperties.SonarcloudUrl, "https://sonarcloud.io")]));

            sut.ServerInfo.Should().BeNull();
            logger.Warnings.Should().BeEmpty();
            logger.AssertErrorLogged("The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set and are different. " +
                "Please set either 'sonar.host.url' for SonarQube or 'sonar.scanner.sonarcloudUrl' for SonarCloud.");
            sut.IsValid.Should().BeFalse();
        }

        [DataTestMethod]
        [DataRow(true, true, true, 3)]
        [DataRow(false, true, true, 2)]
        [DataRow(true, false, true, 2)]
        [DataRow(true, true, false, 2)]
        [DataRow(false, false, true, 1)]
        [DataRow(false, true, false, 1)]
        [DataRow(true, false, false, 1)]
        [DataRow(false, false, false, 0)]
        public void ProcArgs_ErrorAndIsValid(bool invalidKey, bool invalidOrganization, bool invalidHost, int errors)
        {
            var sut = new ProcessedArgs(invalidKey ? "#" : "key", "name", "version", organization: null, false,
                cmdLineProperties: invalidHost
                    ? new ListPropertiesProvider([new Property(SonarProperties.HostUrl, "hostUrl"), new Property(SonarProperties.SonarcloudUrl, "SonarcloudUrl")])
                    : EmptyPropertyProvider.Instance,
                globalFileProperties: invalidOrganization ? new ListPropertiesProvider([new Property(SonarProperties.Organization, "organization")]) : EmptyPropertyProvider.Instance,
                scannerEnvProperties: EmptyPropertyProvider.Instance,
                Substitute.For<IFileWrapper>(),
                Substitute.For<IDirectoryWrapper>(),
                CreateOperatingSystemProvider(),
                logger);
            logger.Errors.Should().HaveCount(errors);
            sut.IsValid.Should().Be(errors == 0);
        }

        [TestMethod]
        public void ProcArgs_OperatingSystem_ParameterProvided()
        {
            var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.OperatingSystem, "windows")]));
            sut.OperatingSystem.Should().Be("windows");
        }

        [TestMethod]
        public void ProcArgs_UserHome_ParameterProvided()
        {
            var directoryWrapper = Substitute.For<IDirectoryWrapper>();
            directoryWrapper.Exists(@"C:\Users\user\.sonar").Returns(true);
            var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.UserHome, @"C:\Users\user\.sonar")]), directoryWrapper: directoryWrapper);
            sut.UserHome.Should().Be(@"C:\Users\user\.sonar");
        }

        [DataTestMethod]
        [DataRow(PlatformOS.Windows, "windows")]
        [DataRow(PlatformOS.MacOSX, "macos")]
        [DataRow(PlatformOS.Alpine, "alpine")]
        [DataRow(PlatformOS.Linux, "linux")]
        [DataRow(PlatformOS.Unknown, null)]
        public void ProcArgs_OperatingSystem_AutoDetection(PlatformOS platformOS, string expectedOperatingSystem)
        {
            var operatingSystemProvider = Substitute.For<IOperatingSystemProvider>();
            operatingSystemProvider.OperatingSystem().Returns(_ => platformOS);
            var sut = CreateDefaultArgs(operatingSystemProvider: operatingSystemProvider);
            sut.OperatingSystem.Should().Be(expectedOperatingSystem);
        }

        private ProcessedArgs CreateDefaultArgs(IAnalysisPropertyProvider cmdLineProperties = null,
                                                IAnalysisPropertyProvider globalFileProperties = null,
                                                IAnalysisPropertyProvider scannerEnvProperties = null,
                                                IOperatingSystemProvider operatingSystemProvider = null,
                                                IFileWrapper fileWrapper = null,
                                                IDirectoryWrapper directoryWrapper = null,
                                                string key = "key",
                                                string organization = "organization") =>
            new(key,
                "name",
                "version",
                organization,
                true,
                cmdLineProperties: cmdLineProperties ?? EmptyPropertyProvider.Instance,
                globalFileProperties: globalFileProperties ?? EmptyPropertyProvider.Instance,
                scannerEnvProperties: scannerEnvProperties ?? EmptyPropertyProvider.Instance,
                fileWrapper: fileWrapper ?? Substitute.For<IFileWrapper>(),
                directoryWrapper: directoryWrapper ?? Substitute.For<IDirectoryWrapper>(),
                operatingSystemProvider: operatingSystemProvider ?? CreateOperatingSystemProvider(),
                logger);

        private static void AssertExpectedValue(string key, string expectedValue, ProcessedArgs args)
        {
            var found = args.TryGetSetting(key, out var actualValue);

            found.Should().BeTrue("Expected setting was not found. Key: {0}", key);
            actualValue.Should().Be(expectedValue, "Setting does not have the expected value. Key: {0}", key);
        }

        private static IOperatingSystemProvider CreateOperatingSystemProvider() =>
            new OperatingSystemProvider(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>());
    }
}
