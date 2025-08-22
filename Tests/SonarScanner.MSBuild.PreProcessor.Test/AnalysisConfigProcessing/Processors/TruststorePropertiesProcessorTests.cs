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

using NSubstitute.ReceivedExtensions;
using SonarScanner.MSBuild.PreProcessor.AnalysisConfigProcessing.Processors;

namespace SonarScanner.MSBuild.PreProcessor.Test.AnalysisConfigProcessing.Processors;

[TestClass]
public class TruststorePropertiesProcessorTests
{
    [TestMethod]
    public void Update_TrustStorePropertiesNullValue_NotMapped_Unix()
    {
        // See also UT Update_TrustedByTheSystem_Windows for the Windows equivalent which adds "javax.net.ssl.trustStoreType=Windows-ROOT"
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        cmdLineArgs.AddProperty("sonar.scanner.truststorePath", null);
        cmdLineArgs.AddProperty("sonar.scanner.truststorePassword", null);
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: true);
        var config = new AnalysisConfig
        {
            LocalSettings =
            [
                new Property("sonar.scanner.truststorePath", null),
                new Property("sonar.scanner.truststorePassword", null)
            ]
        };

        processor.Update(config);

        config.ScannerOptsSettings.Should().BeEmpty();
        Property.TryGetProperty("javax.net.ssl.trustStore", config.LocalSettings, out _).Should().BeFalse();
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void Update_TrustStorePropertiesNullValue_NotMapped_Windows()
    {
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty("sonar.scanner.truststorePath", null);
        cmdLineArgs.AddProperty("sonar.scanner.truststorePassword", null);
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: false);
        var config = new AnalysisConfig
        {
            LocalSettings =
            [
                new Property("sonar.scanner.truststorePath", null),
                new Property("sonar.scanner.truststorePassword", null)
            ]
        };

        processor.Update(config);

        config.ScannerOptsSettings.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Id = "javax.net.ssl.trustStoreType", Value = "Windows-ROOT" });
        Property.TryGetProperty("javax.net.ssl.trustStore", config.LocalSettings, out _).Should().BeFalse();
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void Update_TrustStorePropertiesValue_Mapped()
    {
        // Arrange
        var trustorePath = @"C:\path\to\truststore.pfx";
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.TruststorePath, trustorePath);
        cmdLineArgs.AddProperty(SonarProperties.TruststorePassword, "itchange");
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: false);
        var config = new AnalysisConfig
        {
            LocalSettings =
            [
                new Property(SonarProperties.TruststorePath, trustorePath),
                new Property(SonarProperties.TruststorePassword, "itchange")
            ]
        };

        // Act
        processor.Update(config);

        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", @"""C:/path/to/truststore.pfx""", config);
        config.HasBeginStepCommandLineTruststorePassword.Should().BeTrue();
        Property.TryGetProperty("javax.net.ssl.trustStore", config.LocalSettings, out _).Should().BeFalse();
        Property.TryGetProperty("javax.net.ssl.trustStorePassword", config.LocalSettings, out _).Should().BeFalse();
        Property.TryGetProperty("javax.net.ssl.trustStorePassword", config.ScannerOptsSettings, out _).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(null, null, null)]
    [DataRow("https://sonarcloud.io", null, null)]
    [DataRow("https://SonarCloud.io", null, null)]
    [DataRow("https://sonarqube.us", null, null)]
    [DataRow("https://SonarQube.us", null, null)]
    [DataRow(null, "https://sonarqube.us", null)]
    [DataRow(null, "https://sonarcloud.io", null)]
    [DataRow(null, "https://sonarqube-staging.us", null)]
    [DataRow(null, "https://test.sonarcloud.io", null)]
    [DataRow(null, null, "us")]
    public void Update_TrustStoreProperties_SonarCloud_Mapped(string hostUrl, string sonarCloudUrl, string region)
    {
        var cmdLineArgs = new ListPropertiesProvider();
        var truststorePath = Path.Combine("C:/", "path", "to", "truststore.pfx");
        cmdLineArgs.AddProperty("sonar.scanner.truststorePath", truststorePath);
        cmdLineArgs.AddProperty("sonar.scanner.truststorePassword", "itchange");
        if (hostUrl is not null)
        {
            cmdLineArgs.AddProperty(SonarProperties.HostUrl, hostUrl);
        }
        if (sonarCloudUrl is not null)
        {
            cmdLineArgs.AddProperty(SonarProperties.SonarcloudUrl, sonarCloudUrl);
        }
        if (region is not null)
        {
            cmdLineArgs.AddProperty(SonarProperties.Region, region);
        }
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: false);
        var config = new AnalysisConfig
        {
            LocalSettings =
            [
                new Property("sonar.scanner.truststorePath", truststorePath),
                new Property("sonar.scanner.truststorePassword", "itchange")
            ]
        };

        processor.Update(config);
        config.ScannerOptsSettings.Should().ContainSingle().Which.Should().BeEquivalentTo(
        new
        {
            Id = "javax.net.ssl.trustStore",
            Value = @"""C:/path/to/truststore.pfx""",
        });
        Property.TryGetProperty("javax.net.ssl.trustStore", config.LocalSettings, out _).Should().BeFalse();
        Property.TryGetProperty("javax.net.ssl.trustStorePassword", config.LocalSettings, out _).Should().BeFalse();
        Property.TryGetProperty("javax.net.ssl.trustStorePassword", config.ScannerOptsSettings, out _).Should().BeFalse();
    }

    [TestMethod]
    public void Update_DefaultPropertyValues()
    {
        // Arrange
        var sonarUserHome = Path.Combine("~", ".sonar");
        var defaultTruststorePath = Path.Combine(sonarUserHome, SonarPropertiesDefault.TruststorePath);
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.UserHome, sonarUserHome);
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(defaultTruststorePath).Returns(true);
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs, fileWrapper), isUnix: false);
        var config = new AnalysisConfig { LocalSettings = [new Property(SonarProperties.UserHome, sonarUserHome)] };

        // Act
        processor.Update(config);

        config.LocalSettings.Should().ContainSingle(x => x.Id == SonarProperties.UserHome && x.Value == sonarUserHome);
        config.ScannerOptsSettings.Should().ContainSingle()
            .Which.Should().Match<Property>(x => x.Id == "javax.net.ssl.trustStore" && x.Value == $"\"{defaultTruststorePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}\"");
        config.HasBeginStepCommandLineTruststorePassword.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(SonarProperties.Verbose, "true")]
    [DataRow(SonarProperties.Organization, "org")]
    [DataRow(SonarProperties.HostUrl, "http://localhost:9000")]
    [DataRow(SonarProperties.HostUrl, @"http://localhost:9000\")]
    public void Update_UnmappedProperties(string id, string value)
    {
        // Arrange
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(id, value);
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: false);
        var config = new AnalysisConfig { LocalSettings = [new Property(id, value)] };

        // Act
        processor.Update(config);

        config.LocalSettings.Should().ContainSingle(x => x.Id == id && x.Value == value);
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    [DataRow(@"C:\path\to\truststore.pfx", @"""C:/path/to/truststore.pfx""")]
    [DataRow(@"C:\path\to\My trustore.pfx", @"""C:/path/to/My trustore.pfx""")]
    public void Update_MapsTruststorePathToScannerOpts_Windows(string input, string expected)
    {
        // Arrange
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        cmdLineArgs.AddProperty(SonarProperties.TruststorePath, input);
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: false);
        var config = new AnalysisConfig { LocalSettings = [new Property(SonarProperties.TruststorePath, input)] };

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        config.HasBeginStepCommandLineTruststorePassword.Should().BeFalse();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", expected, config);
        Property.TryGetProperty(SonarProperties.TruststorePath, config.LocalSettings, out _).Should().BeFalse();
        Property.TryGetProperty(SonarProperties.TruststorePassword, config.LocalSettings, out _).Should().BeFalse();
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    [DataRow("itchange", @"""itchange""")]
    [DataRow("it change", @"""it change""")]
    [DataRow(@"""itchange""", @"""itchange""")]
    public void Update_MapsTruststorePasswordToScannerOpts_Windows(string input, string expected)
    {
        // Arrange
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.TruststorePath, "some/path");
        cmdLineArgs.AddProperty(SonarProperties.TruststorePassword, input);
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: false);
        var config = new AnalysisConfig
        {
            LocalSettings =
            [
                new Property(SonarProperties.TruststorePath, "some/path"),
                new Property(SonarProperties.TruststorePassword, input)
            ]
        };

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        config.HasBeginStepCommandLineTruststorePassword.Should().BeTrue();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", @"""some/path""", config);
    }

    [TestMethod]
    [DataRow("/path/to/truststore.pfx", "/path/to/truststore.pfx")]
    [DataRow("/path/to/my trustore.pfx", "/path/to/my trustore.pfx")]
    public void Update_MapsTruststorePathToScannerOpts_Linux(string input, string expected)
    {
        // Arrange
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        cmdLineArgs.AddProperty(SonarProperties.TruststorePath, input);
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: true);
        var config = new AnalysisConfig { LocalSettings = [new Property(SonarProperties.TruststorePath, input)] };

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        config.HasBeginStepCommandLineTruststorePassword.Should().BeFalse();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", expected, config);
        Property.TryGetProperty(SonarProperties.TruststorePath, config.LocalSettings, out _).Should().BeFalse();
        Property.TryGetProperty(SonarProperties.TruststorePassword, config.LocalSettings, out _).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("itchange", "itchange")]
    [DataRow("it change", "it change")]
    public void Update_MapsTruststorePasswordToScannerOpts_Linux(string input, string expected)
    {
        // Arrange
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        cmdLineArgs.AddProperty(SonarProperties.TruststorePassword, input);
        var javaHome = Path.Combine("/home", "user", "java");
        var javaHomeCacerts = Path.Combine(javaHome, "lib", "security", "cacerts");
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(javaHomeCacerts).Returns(true);
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(javaHome).Returns(true);
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), fileWrapper, directoryWrapper, isUnix: true);
        var config = new AnalysisConfig { LocalSettings = [new Property(SonarProperties.TruststorePassword, input)] };
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", javaHome);

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        config.HasBeginStepCommandLineTruststorePassword.Should().BeTrue();
        fileWrapper.Received(Quantity.Exactly(1)).Exists(javaHomeCacerts);
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", javaHomeCacerts.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), config);
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestMethod]
    public void Update_TrustedByTheSystem_Windows()
    {
        // Arrange
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: false);
        var config = new AnalysisConfig { LocalSettings = [] };

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStoreType", "Windows-ROOT", config);
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestMethod]
    public void Update_TrustedByTheSystemPasswordProvided_Windows()
    {
        // Arrange
        var javaHome = Path.Combine("C:", "Program Files", "Java", "jre");
        var javaHomeCacerts = Path.Combine(javaHome, "lib", "security", "cacerts");
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(javaHomeCacerts).Returns(true);
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), fileWrapper, isUnix: false);
        var config = new AnalysisConfig { LocalSettings = [] };
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", javaHome);
        envScope.SetVariable("SONAR_SCANNER_OPTS", "-Xmx2048m -Djavax.net.ssl.trustStorePassword=itchange");

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        fileWrapper.Received(Quantity.None()).Exists(javaHomeCacerts);
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStoreType", "Windows-ROOT", config);
    }

    [TestMethod]
    public void Update_TrustedByTheSystem_Linux()
    {
        // Arrange
        var javaHome = Path.Combine("/home", "user", "java");
        var javaHomeCacerts = Path.Combine(javaHome, "lib", "security", "cacerts");
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(javaHomeCacerts).Returns(true);
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(javaHome).Returns(true);
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), fileWrapper, directoryWrapper, isUnix: true);
        var config = new AnalysisConfig { LocalSettings = [] };
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", javaHome);

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", javaHomeCacerts.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), config);
    }

    [TestMethod]
    public void Update_TrustedByTheSystemCacertNotFound_Linux()
    {
        // Arrange
        var javaHome = Path.Combine("/home", "user", "java");
        var javaHomeCacerts = Path.Combine(javaHome, "lib", "security", "cacerts");
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(javaHomeCacerts).Returns(false);
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(javaHome).Returns(true);
        var logger = new TestLogger();
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), fileWrapper, directoryWrapper, logger: logger, isUnix: true);
        var config = new AnalysisConfig { LocalSettings = [] };
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", javaHome);

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().BeEmpty();
        logger.AssertDebugLogged($"Unable to find Java Keystore file. Lookup path: '{javaHomeCacerts}'.");
    }

    [TestMethod]
    public void Update_TrustedByTheSystemNoJavaHome_Linux()
    {
        // Arrange
        var logger = new TestLogger();
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Execute(Arg.Is<ProcessRunnerArguments>(x => x.CmdLineArgs.Contains("command -v java")))
            .Returns(new ProcessResult(true, "/usr/bin/java", string.Empty));
        processRunner.Execute(Arg.Is<ProcessRunnerArguments>(x => x.CmdLineArgs.Contains("readlink -f /usr/bin/java")))
            .Returns(new ProcessResult(true, "/java/home/bin/java", string.Empty));
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(Arg.Any<string>()).Returns(true);
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(Arg.Any<string>()).Returns(true);
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), fileWrapper, directoryWrapper, processRunner, logger, isUnix: true);
        var config = new AnalysisConfig { LocalSettings = [] };
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", null);

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", "/java/home/lib/security/cacerts", config);
        logger.AssertDebugLogged("JAVA_HOME environment variable not set. Try to infer Java home from Java executable.");
        logger.AssertDebugLogged("Java executable located at: '/usr/bin/java'.");
        logger.AssertDebugLogged("Java executable symbolic link resolved to: '/java/home/bin/java'.");
    }

    [TestMethod]
    public void Update_TrustedByTheSystemPasswordProvided_Linux()
    {
        // Arrange
        var javaHome = Path.Combine("/home", "user", "java");
        var javaHomeCacerts = Path.Combine(javaHome, "lib", "security", "cacerts");
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(javaHomeCacerts).Returns(true);
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(javaHome).Returns(true);
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), fileWrapper, directoryWrapper, isUnix: true);
        var config = new AnalysisConfig { LocalSettings = [] };
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", javaHome);
        envScope.SetVariable("SONAR_SCANNER_OPTS", "-Xmx2048m -Djavax.net.ssl.trustStorePassword=itchange");

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        config.HasBeginStepCommandLineTruststorePassword.Should().BeFalse();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", javaHomeCacerts.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), config);
    }

    [TestMethod]
    public void Update_TrustedByTheSystemSonarOptsSet_Linux()
    {
        // Arrange
        var javaHome = Path.Combine("/home", "user", "java");
        var javaHomeCacerts = Path.Combine(javaHome, "lib", "security", "cacerts");
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(javaHomeCacerts).Returns(true);
        var directoryWrapper = Substitute.For<IDirectoryWrapper>();
        directoryWrapper.Exists(javaHome).Returns(true);
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty(SonarProperties.HostUrl, "https://localhost:9000");
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), fileWrapper, directoryWrapper, isUnix: true);
        var config = new AnalysisConfig { LocalSettings = [] };
        using var envScope = new EnvironmentVariableScope();
        envScope.SetVariable("JAVA_HOME", javaHome);
        envScope.SetVariable("SONAR_SCANNER_OPTS", "-Xmx2048m");

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", javaHomeCacerts.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), config);
    }

    private static void AssertExpectedScannerOptsSettings(string key, string expectedValue, AnalysisConfig actualConfig)
    {
        var found = Property.TryGetProperty(key, actualConfig.ScannerOptsSettings, out var property);
        found.Should().BeTrue("Expected local property was not found. Key: {0}", key);
        property.Value.Should().Be(expectedValue, "Unexpected local value. Key: {0}", key);
    }

    private static TruststorePropertiesProcessor CreateProcessor(
        ProcessedArgs args,
        IFileWrapper fileWrapper = null,
        IDirectoryWrapper directoryWrapper = null,
        IProcessRunner processRunner = null,
        ILogger logger = null,
        bool isUnix = false)
    {
        var operatingSystemProvider = Substitute.For<OperatingSystemProvider>(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>());
        operatingSystemProvider.IsUnix().Returns(isUnix);
        return new TruststorePropertiesProcessor(
            args,
            null,
            fileWrapper ?? Substitute.For<IFileWrapper>(),
            directoryWrapper ?? Substitute.For<IDirectoryWrapper>(),
            processRunner ?? Substitute.For<IProcessRunner>(),
            logger ?? Substitute.For<ILogger>(),
            operatingSystemProvider);
    }

    private static ProcessedArgs CreateProcessedArgs(IAnalysisPropertyProvider cmdLineProvider = null, IFileWrapper fileWrapper = null) =>
        new("valid.key",
            "valid.name",
            "1.0",
            "organization",
            false,
            cmdLineProvider ?? EmptyPropertyProvider.Instance,
            Substitute.For<IAnalysisPropertyProvider>(),
            EmptyPropertyProvider.Instance,
            fileWrapper ?? Substitute.For<IFileWrapper>(),
            Substitute.For<IDirectoryWrapper>(),
            Substitute.For<OperatingSystemProvider>(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()),
            Substitute.For<ILogger>());
}
