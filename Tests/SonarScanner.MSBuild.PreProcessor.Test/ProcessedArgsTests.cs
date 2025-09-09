/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class ProcessedArgsTests
{
    private readonly TestRuntime runtime = new();

    private ProcessedArgs args;

    private static IEnumerable<object[]> DirectoryCreateExceptions
    {
        get
        {
            yield return [typeof(IOException)];
            yield return [typeof(UnauthorizedAccessException)];
            yield return [typeof(ArgumentException)];
            yield return [typeof(ArgumentNullException)];
            yield return [typeof(PathTooLongException)];
            yield return [typeof(DirectoryNotFoundException)];
            yield return [typeof(NotSupportedException)];
        }
    }

    [TestInitialize]
    public void TestInitialize()
    {
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
    public void ProcessedArgs_ParameterThrow_Key() =>
        FluentActions.Invoking(() => CreateDefaultArgs(key: null)).Should().Throw<ArgumentNullException>().WithParameterName("key");

    [TestMethod]
    public void ProcessedArgs_ParameterThrow_CmdLineProperties() =>
        FluentActions.Invoking(
            () => new ProcessedArgs(
                    "key",
                    "name",
                    "version",
                    "org",
                    true,
                    cmdLineProperties: null,
                    EmptyPropertyProvider.Instance,
                    EmptyPropertyProvider.Instance,
                    runtime))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("cmdLineProperties");

    [TestMethod]
    public void ProcessedArgs_ParameterThrow_GlobalFileProperties() =>
        FluentActions.Invoking(
            () => new ProcessedArgs(
                    "key",
                    "name",
                    "version",
                    "org",
                    true,
                    EmptyPropertyProvider.Instance,
                    globalFileProperties: null,
                    EmptyPropertyProvider.Instance,
                    runtime))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("globalFileProperties");

    [TestMethod]
    public void ProcessedArgs_ParameterThrow_ScannerEnvProperties() =>
        FluentActions.Invoking(
            () => new ProcessedArgs(
                    "key",
                    "name",
                    "version",
                    "org",
                    true,
                    EmptyPropertyProvider.Instance,
                    EmptyPropertyProvider.Instance,
                    scannerEnvProperties: null,
                    runtime))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("scannerEnvProperties");

    [TestMethod]
    public void ProcessedArgs_Organization()
    {
        args.Organization.Should().BeNull();
        args = CreateDefaultArgs(organization: "organization");
        args.Organization.Should().Be("organization");
    }

    [TestMethod]
    public void ProcessedArgs_Setting()
    {
        // 1. Throws on missing value
        args.Invoking(x => x.Setting("missing.property")).Should().ThrowExactly<InvalidOperationException>();

        // 2. Returns existing values
        args.Setting("cmd.key.1").Should().Be("cmd value 1");
        args.Setting("file.key.1").Should().Be("file value 1");
        args.Setting("env.key.1").Should().Be("env value 1");

        // 3. Precedence - command line properties should win
        args.Setting("shared.key.1").Should().Be("shared cmd value");

        // 4. Precedence - file wins over env
        args.Setting("shared.key.2").Should().Be("shared file value");

        // 5. Preprocessor only settings
        args.InstallLoaderTargets.Should().BeTrue();
    }

    [TestMethod]
    public void ProcessedArgs_TryGetSetting()
    {
        // 1. Missing key -> null
        args.TryGetSetting("missing.property", out var result).Should().BeFalse("Expecting false when the specified key does not exist");
        result.Should().BeNull("Expecting the value to be null when the specified key does not exist");

        // 2. Returns existing values
        args.TryGetSetting("cmd.key.1", out result).Should().BeTrue();
        result.Should().Be("cmd value 1");

        // 3. Precedence - command line properties should win
        args.Setting("shared.key.1").Should().Be("shared cmd value");

        // 4. Preprocessor only settings
        args.InstallLoaderTargets.Should().BeTrue();
    }

    [TestMethod]
    public void ProcessedArgs__SettingOrDefault()
    {
        // 1. Missing key -> default returned
        var result = args.SettingOrDefault("missing.property", "default value");
        result.Should().Be("default value");

        // 2. Returns existing values
        result = args.SettingOrDefault("file.key.1", "default value");
        result.Should().Be("file value 1");

        // 3. Precedence - command line properties should win
        args.SettingOrDefault("shared.key.1", "default ValueType").Should().Be("shared cmd value");

        // 4. Preprocessor only settings
        args.InstallLoaderTargets.Should().BeTrue();
    }

    [TestMethod]
    public void ProcessedArgs_CmdLinePropertiesOverrideFileSettings()
    {
        var cmdLineProperties = new ListPropertiesProvider();
        cmdLineProperties.AddProperty("shared.key1", "cmd line value1 - should override server value");
        cmdLineProperties.AddProperty("cmd.line.only", "cmd line value4 - only on command line");
        cmdLineProperties.AddProperty("xxx", "cmd line value XXX - lower case");
        cmdLineProperties.AddProperty(SonarProperties.HostUrl, "http://host");

        var fileProperties = new ListPropertiesProvider();
        fileProperties.AddProperty("shared.key1", "file value1 - should be overridden");
        fileProperties.AddProperty("file.only", "file value3 - only in file");
        fileProperties.AddProperty("XXX", "file line value XXX - upper case");

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
    public void ProcessedArgs_HostUrl_SonarCloudUrl_HostUrlSet()
    {
        var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.HostUrl, "http://host")]));

        sut.ServerInfo.Should().NotBeNull();
        sut.ServerInfo.IsSonarCloud.Should().BeFalse();
        sut.ServerInfo.ServerUrl.Should().Be("http://host");
        runtime.Logger.Warnings.Should().BeEmpty();
        runtime.Logger.Errors.Should().BeEmpty();
        sut.IsValid.Should().BeTrue();
    }

    [TestMethod]
    public void ProcessedArgs_HostUrl_SonarCloudUrl_SonarCloudUrlSet()
    {
        var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.SonarcloudUrl, "https://sonarcloud.proxy")]));

        sut.ServerInfo.Should().NotBeNull();
        sut.ServerInfo.IsSonarCloud.Should().BeTrue();
        sut.ServerInfo.ServerUrl.Should().Be("https://sonarcloud.proxy");
        runtime.Logger.Warnings.Should().BeEmpty();
        runtime.Logger.Errors.Should().BeEmpty();
        sut.IsValid.Should().BeTrue();
    }

    [TestMethod]
    public void ProcessedArgs_HostUrl_SonarCloudUrl_HostUrlAndSonarcloudUrlAreIdentical()
    {
        var sut = CreateDefaultArgs(new ListPropertiesProvider([
            new Property(SonarProperties.HostUrl, "https://sonarcloud.proxy"), new Property(SonarProperties.SonarcloudUrl, "https://sonarcloud.proxy")
        ]));

        sut.ServerInfo.Should().NotBeNull();
        sut.ServerInfo.IsSonarCloud.Should().BeTrue();
        sut.ServerInfo.ServerUrl.Should().Be("https://sonarcloud.proxy");
        runtime.Should().HaveWarningsLogged("The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set. Please set only 'sonar.scanner.sonarcloudUrl'.");
        runtime.Logger.Errors.Should().BeEmpty();
        sut.IsValid.Should().BeTrue();
    }

    [TestMethod]
    public void ProcessedArgs_HostUrl_SonarCloudUrl_HostUrlAndSonarcloudUrlDiffer()
    {
        var sut = CreateDefaultArgs(new ListPropertiesProvider([
            new Property(SonarProperties.HostUrl, "https://someHost.com"), new Property(SonarProperties.SonarcloudUrl, "https://someOtherHost.org")
        ]));

        sut.ServerInfo.Should().BeNull();
        runtime.Logger.Warnings.Should().BeEmpty();
        runtime.Should().HaveErrorsLogged("The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set and are different. "
            + "Please set either 'sonar.host.url' for SonarQube or 'sonar.scanner.sonarcloudUrl' for SonarCloud.");
        sut.IsValid.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void ProcessedArgs_HostUrl_SonarCloudUrl_HostUrlAndSonarcloudUrlEmpty(string empty)
    {
        var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.HostUrl, empty), new Property(SonarProperties.SonarcloudUrl, empty),]));

        sut.ServerInfo.Should().BeNull();
        runtime.Logger.Warnings.Should().BeEmpty();
        runtime.Should().HaveErrorsLogged("The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set to an invalid value.");
        sut.IsValid.Should().BeFalse();
    }

    [TestMethod]
    public void ProcessedArgs_HostUrl_SonarCloudUrl_HostUrlAndSonarcloudUrlMissing()
    {
        var sut = CreateDefaultArgs(EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance, EmptyPropertyProvider.Instance);

        sut.ServerInfo.Should().NotBeNull();
        sut.ServerInfo.IsSonarCloud.Should().BeTrue();
        sut.ServerInfo.ServerUrl.Should().Be("https://sonarcloud.io");
        runtime.Logger.Warnings.Should().BeEmpty();
        runtime.Logger.Errors.Should().BeEmpty();
        sut.IsValid.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("https://sonarcloud.io", "https://api.sonarcloud.io", "")]
    [DataRow("https://SonarCloud.io", "https://api.sonarcloud.io", "")]
    [DataRow("https://SONARCLOUD.IO", "https://api.sonarcloud.io", "")]
    [DataRow("https://sonarcloud.io/", "https://api.sonarcloud.io", "")]
    [DataRow("https://sonarcloud.io///", "https://api.sonarcloud.io", "")]
    [DataRow("https://sonarqube.us", "https://api.sonarqube.us", "us")]
    [DataRow("https://SonarQube.us", "https://api.sonarqube.us", "us")]
    [DataRow("https://SONARQUBE.US", "https://api.sonarqube.us", "us")]
    [DataRow("https://sonarqube.us/", "https://api.sonarqube.us", "us")]
    [DataRow("https://sonarqube.us///", "https://api.sonarqube.us", "us")]
#pragma warning disable S3994 // we are specifically testing string urls
    public void ProcessedArgs_HostUrl_SonarCloudUrl_HostUrlIsAnySonarCloud(string hostUrl, string expectedApiBaseUrl, string expectedRegion)
#pragma warning restore S3994
    {
        var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.HostUrl, hostUrl)]));

        sut.IsValid.Should().BeTrue();  // Internal property can't be asserted via the anonymous object below
        sut.Should().BeEquivalentTo(new
        {
            ServerInfo = new
            {
                IsSonarCloud = true,
                ServerUrl = hostUrl,
                ApiBaseUrl = expectedApiBaseUrl,
                Region = expectedRegion,
            },
        });
        runtime.Logger.Warnings.Should().BeEmpty();
        runtime.Logger.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void ProcessedArgs_HostUrl_SonarCloudUrl_PropertyAggregation()
    {
        var sut = CreateDefaultArgs(
            new ListPropertiesProvider([new Property(SonarProperties.HostUrl, "https://localhost")]),
            new ListPropertiesProvider([new Property(SonarProperties.SonarcloudUrl, "https://sonarcloud.io")]));

        sut.ServerInfo.Should().BeNull();
        runtime.Logger.Warnings.Should().BeEmpty();
        runtime.Should().HaveErrorsLogged("The arguments 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' are both set and are different. "
            + "Please set either 'sonar.host.url' for SonarQube or 'sonar.scanner.sonarcloudUrl' for SonarCloud.");
        sut.IsValid.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(true, true, true, true, 4)]
    [DataRow(true, true, true, false, 3)]
    [DataRow(true, true, false, true, 3)]
    [DataRow(true, true, false, false, 2)]
    [DataRow(true, false, true, true, 3)]
    [DataRow(true, false, true, false, 2)]
    [DataRow(true, false, false, true, 2)]
    [DataRow(true, false, false, false, 1)]
    [DataRow(false, true, true, true, 3)]
    [DataRow(false, true, true, false, 2)]
    [DataRow(false, true, false, true, 2)]
    [DataRow(false, true, false, false, 1)]
    [DataRow(false, false, true, true, 2)]
    [DataRow(false, false, true, false, 1)]
    [DataRow(false, false, false, true, 1)]
    [DataRow(false, false, false, false, 0)]
    public void ProcessedArgs_ErrorAndIsValid(bool invalidKey, bool invalidOrganization, bool invalidHost, bool invalidUserHome, int errors)
    {
        runtime.Directory.When(x => x.CreateDirectory("NotADirectory")).Do(x => _ = invalidUserHome ? throw new IOException("Invalid Directory") : 1);
        var sut = new ProcessedArgs(
            invalidKey ? "#" : "key",
            "name",
            "version",
            organization: null,
            installLoaderTargets: false,
            cmdLineProperties: invalidHost
            ? new ListPropertiesProvider([new Property(SonarProperties.HostUrl, "hostUrl"), new Property(SonarProperties.SonarcloudUrl, "SonarcloudUrl")])
            : EmptyPropertyProvider.Instance,
            globalFileProperties: invalidOrganization ? new ListPropertiesProvider([new Property(SonarProperties.Organization, "organization")]) : EmptyPropertyProvider.Instance,
            scannerEnvProperties: new ListPropertiesProvider([new Property(SonarProperties.UserHome, "NotADirectory")]),
            runtime);
        runtime.Logger.Errors.Should().HaveCount(errors);
        sut.IsValid.Should().Be(errors == 0);
    }

    [TestMethod]
    public void ProcessedArgs_OperatingSystem_ParameterProvided()
    {
        var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.OperatingSystem, "windows")]));
        sut.OperatingSystem.Should().Be("windows");
    }

    [TestMethod]
    [DataRow(PlatformOS.Windows, "windows")]
    [DataRow(PlatformOS.MacOSX, "macos")]
    [DataRow(PlatformOS.Alpine, "alpine")]
    [DataRow(PlatformOS.Linux, "linux")]
    [DataRow(PlatformOS.Unknown, null)]
    public void ProcessedArgs_OperatingSystem_AutoDetection(PlatformOS platformOS, string expectedOperatingSystem)
    {
        runtime.OperatingSystem.OperatingSystem().Returns(_ => platformOS);
        var sut = CreateDefaultArgs();
        sut.OperatingSystem.Should().Be(expectedOperatingSystem);
        sut.IsValid.Should().BeTrue();
    }

    [TestMethod]
    public void ProcessedArgs_UserHome_ParameterProvided()
    {
        runtime.Directory.Exists(@"C:\Users\user\.sonar").Returns(true);
        var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.UserHome, @"C:\Users\user\.sonar")]));
        sut.UserHome.Should().Be(@"C:\Users\user\.sonar");
        sut.IsValid.Should().BeTrue();
        runtime.Should().HaveNoErrorsLogged()
            .And.HaveNoWarningsLogged();
    }

    [TestMethod]
    public void ProcessedArgs_UserHome_ParameterProvided_DoesNotExists_CanBeCreated()
    {
        runtime.Directory.Exists(@"C:\Users\user\.sonar").Returns(false);
        var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.UserHome, @"C:\Users\user\.sonar")]));
        sut.UserHome.Should().Be(@"C:\Users\user\.sonar");
        sut.IsValid.Should().BeTrue();
        runtime.Should().HaveDebugsLogged(@"Created the sonar.userHome directory at 'C:\Users\user\.sonar'.")
            .And.HaveNoErrorsLogged()
            .And.HaveNoWarningsLogged();
    }

    [TestMethod]
    public void ProcessedArgs_UserHome_ParameterProvided_DoesNotExists_CanNotBeCreated()
    {
        runtime.Directory.Exists(@"C:\Users\user\.sonar").Returns(false);
        runtime.Directory.When(x => x.CreateDirectory(@"C:\Users\user\.sonar")).Do(_ => throw new IOException("Directory can not be created."));
        var sut = CreateDefaultArgs(new ListPropertiesProvider([new Property(SonarProperties.UserHome, @"C:\Users\user\.sonar")]));
        sut.UserHome.Should().BeNull();
        sut.IsValid.Should().BeFalse();
        runtime.Should().HaveErrorsLogged(@"The attempt to create the directory specified by 'sonar.userHome' at 'C:\Users\user\.sonar' failed with error 'Directory can not be created.'. "
            + @"Provide a valid path for 'sonar.userHome' to a directory that can be created.")
            .And.HaveNoWarningsLogged();
    }

    [TestMethod]
    public void ProcessedArgs_UserHome_Default()
    {
        runtime.OperatingSystem.FolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.None).Returns(Path.Combine(TestUtils.DriveRoot(), "Users", "user"));
        runtime.Directory.Exists(Path.Combine(TestUtils.DriveRoot(), "Users", "user", ".sonar")).Returns(true);
        var sut = CreateDefaultArgs();
        sut.UserHome.Should().Be(Path.Combine(TestUtils.DriveRoot(), "Users", "user", ".sonar"));
        sut.IsValid.Should().BeTrue();
        runtime.Should().HaveNoErrorsLogged()
            .And.HaveNoWarningsLogged();
    }

    [TestMethod]
    public void ProcessedArgs_UserHome_Default_CreatedOnDemand()
    {
        runtime.OperatingSystem.FolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.None).Returns(Path.Combine(TestUtils.DriveRoot(), "Users", "user"));
        runtime.Directory.Exists(Path.Combine(TestUtils.DriveRoot(), "Users", "user", ".sonar")).Returns(false);
        var sut = CreateDefaultArgs();
        sut.UserHome.Should().Be(Path.Combine(TestUtils.DriveRoot(), "Users", "user", ".sonar"));
        sut.IsValid.Should().BeTrue();
        runtime.Directory.Received().CreateDirectory(Path.Combine(TestUtils.DriveRoot(), "Users", "user", ".sonar"));
        runtime.Should().HaveNoErrorsLogged()
            .And.HaveNoWarningsLogged();
    }

    [TestMethod]
    [DynamicData(nameof(DirectoryCreateExceptions))]
    public void ProcessedArgs_UserHome_Default_CreationFails(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType);
        runtime.OperatingSystem.FolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.None).Returns(Path.Combine(TestUtils.DriveRoot(), "Users", "user"));
        runtime.Directory.Exists(Path.Combine(TestUtils.DriveRoot(), "Users", "user", ".sonar")).Returns(false);
        runtime.Directory.When(x => x.CreateDirectory(Path.Combine(TestUtils.DriveRoot(), "Users", "user", ".sonar"))).Throw(exception);
        var sut = CreateDefaultArgs();
        sut.UserHome.Should().BeNull();
        sut.IsValid.Should().BeTrue();
        runtime.Directory.Received().CreateDirectory(Path.Combine(TestUtils.DriveRoot(), "Users", "user", ".sonar"));
        runtime.Should().HaveWarningsLogged("Failed to create the default user home directory "
            + $"'{Path.Combine(TestUtils.DriveRoot(), "Users", "user", ".sonar")}' with exception '{exception.Message}'.")
            .And.HaveNoErrorsLogged();
    }

    [TestMethod]
    [DynamicData(nameof(ProcessedArgs_SourcesOrTests_Warning_DataSource), DynamicDataSourceType.Method)]
    public void ProcessedArgs_SourcesOrTests_Warning(params Property[] properties)
    {
        var expectedMessage = "The sonar.sources and sonar.tests properties are not supported by the Scanner for .NET and are ignored. "
            + "They are automatically computed based on your repository. You can fine-tune the analysis and exclude some files by using the sonar.exclusions, "
            + "sonar.inclusions, sonar.test.exclusions, and sonar.test.inclusions properties.";

        var sut = CreateDefaultArgs(new ListPropertiesProvider(properties));

        sut.IsValid.Should().BeTrue();
        runtime.Logger.Errors.Should().BeEmpty();
        runtime.Logger.Warnings.Should().ContainSingle(expectedMessage);
        runtime.Logger.UIWarnings.Should().ContainSingle(expectedMessage);
    }

    private static IEnumerable<object[]> ProcessedArgs_SourcesOrTests_Warning_DataSource() =>
    [
        [new Property(SonarProperties.Sources, "src")],
        [new Property(SonarProperties.Tests, "tests")],
        [new Property(SonarProperties.Sources, "src"), new Property(SonarProperties.Tests, "tests")]
    ];

    private ProcessedArgs CreateDefaultArgs(IAnalysisPropertyProvider cmdLineProperties = null,
                                            IAnalysisPropertyProvider globalFileProperties = null,
                                            IAnalysisPropertyProvider scannerEnvProperties = null,
                                            string key = "key",
                                            string organization = "organization") =>
        new(
            key,
            "name",
            "version",
            organization,
            true,
            cmdLineProperties: cmdLineProperties ?? EmptyPropertyProvider.Instance,
            globalFileProperties: globalFileProperties ?? EmptyPropertyProvider.Instance,
            scannerEnvProperties: scannerEnvProperties ?? EmptyPropertyProvider.Instance,
            runtime);

    private static void AssertExpectedValue(string key, string expectedValue, ProcessedArgs args)
    {
        var found = args.TryGetSetting(key, out var actualValue);

        found.Should().BeTrue("Expected setting was not found. Key: {0}", key);
        actualValue.Should().Be(expectedValue, "Setting does not have the expected value. Key: {0}", key);
    }
}
