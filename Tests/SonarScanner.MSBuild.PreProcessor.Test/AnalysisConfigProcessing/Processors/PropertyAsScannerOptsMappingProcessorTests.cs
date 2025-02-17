using SonarScanner.MSBuild.PreProcessor.AnalysisConfigProcessing.Processors;

namespace SonarScanner.MSBuild.PreProcessor.Test.AnalysisConfigProcessing.Processors;

[TestClass]
public class PropertyAsScannerOptsMappingProcessorTests
{
    [TestMethod]
    public void Update_TrustStorePropertiesNullValue_Mapped()
    {
        // Arrange
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

        // Act
        processor.Update(config);

        config.ScannerOptsSettings.Should().BeEmpty();
        Property.TryGetProperty("javax.net.ssl.trustStore", config.LocalSettings, out _).Should().BeFalse();
    }

    [TestMethod]
    public void Update_TrustStorePropertiesValue_Mapped()
    {
        // Arrange
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty("sonar.scanner.truststorePath", @"C:\path\to\truststore.pfx");
        cmdLineArgs.AddProperty("sonar.scanner.truststorePassword", "changeit");
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: false);
        var config = new AnalysisConfig
        {
            LocalSettings =
            [
                new Property("sonar.scanner.truststorePath", @"C:\path\to\truststore.pfx"),
                new Property("sonar.scanner.truststorePassword", "changeit")
            ]
        };

        // Act
        processor.Update(config);

        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", @"""C:/path/to/truststore.pfx""", config);
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStorePassword", @"""changeit""", config);
        Property.TryGetProperty("javax.net.ssl.trustStore", config.LocalSettings, out _).Should().BeFalse();
        Property.TryGetProperty("javax.net.ssl.trustStorePassword", config.LocalSettings, out _).Should().BeFalse();
    }

    [TestMethod]
    public void Update_DefaultPropertyValues()
    {
        // Arrange
        var sonarUserHome = Path.Combine("~", ".sonar");
        var defaultTruststorePath = Path.Combine(sonarUserHome, SonarPropertiesDefault.TruststorePath);
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty("sonar.userHome", sonarUserHome);
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.Exists(defaultTruststorePath).Returns(true);
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs, fileWrapper), isUnix: false);
        var config = new AnalysisConfig { LocalSettings = [new Property("sonar.userHome", sonarUserHome)] };

        // Act
        processor.Update(config);

        config.LocalSettings.Should().ContainSingle(x => x.Id == "sonar.userHome" && x.Value == sonarUserHome);
        config.ScannerOptsSettings.Should().HaveCount(2)
            .And.Contain(x => x.Id == "javax.net.ssl.trustStore" && x.Value == $"\"{defaultTruststorePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}\"")
            .And.Contain(x => x.Id == "javax.net.ssl.trustStorePassword" && x.Value == $"\"{SonarPropertiesDefault.TruststorePassword}\"");
    }

    [DataTestMethod]
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

    [DataTestMethod]
    [DataRow(@"C:\path\to\truststore.pfx", @"""C:/path/to/truststore.pfx""")]
    [DataRow(@"C:\path\to\My trustore.pfx", @"""C:/path/to/My trustore.pfx""")]
    public void Update_MapsTruststorePathToScannerOpts_Windows(string input, string expected)
    {
        // Arrange
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty("sonar.scanner.truststorePath", input);
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: false);
        var config = new AnalysisConfig { LocalSettings = [new Property("sonar.scanner.truststorePath", input)] };

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().HaveCount(2);
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", expected, config);
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStorePassword", $"\"{SonarPropertiesDefault.TruststorePassword}\"", config);
        Property.TryGetProperty("sonar.scanner.truststorePath", config.LocalSettings, out _).Should().BeFalse();
    }

    [DataTestMethod]
    [DataRow("changeit", @"""changeit""")]
    [DataRow("change it", @"""change it""")]
    [DataRow(@"""changeit""", @"""changeit""")]
    public void Update_MapsTruststorePasswordToScannerOpts_Windows(string input, string expected)
    {
        // Arrange
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty("sonar.scanner.truststorePassword", input);
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: false);
        var config = new AnalysisConfig { LocalSettings = [new Property("sonar.scanner.truststorePassword", input)] };

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().ContainSingle()
            .Which.Should().Match<Property>(x => x.Id == SonarProperties.TruststorePassword && x.Value == input);
        config.ScannerOptsSettings.Should().ContainSingle();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStorePassword", expected, config);
    }

    [DataTestMethod]
    [DataRow("/path/to/truststore.pfx", "/path/to/truststore.pfx")]
    [DataRow("/path/to/my trustore.pfx", "/path/to/my trustore.pfx")]
    public void Update_MapsTruststorePathToScannerOpts_Linux(string input, string expected)
    {
        // Arrange
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty("sonar.scanner.truststorePath", input);
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: true);
        var config = new AnalysisConfig { LocalSettings = [new Property("sonar.scanner.truststorePath", input)] };

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().HaveCount(2);
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", expected, config);
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStorePassword", SonarPropertiesDefault.TruststorePassword, config);
        Property.TryGetProperty("sonar.scanner.truststorePath", config.LocalSettings, out _).Should().BeFalse();
    }

    [DataTestMethod]
    [DataRow("changeit", "changeit")]
    [DataRow("change it", "change it")]
    public void Update_MapsTruststorePasswordToScannerOpts_Linux(string input, string expected)
    {
        // Arrange
        var cmdLineArgs = new ListPropertiesProvider();
        cmdLineArgs.AddProperty("sonar.scanner.truststorePassword", input);
        var processor = CreateProcessor(CreateProcessedArgs(cmdLineArgs), isUnix: true);
        var config = new AnalysisConfig { LocalSettings = [new Property("sonar.scanner.truststorePassword", input)] };

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().ContainSingle()
            .Which.Should().Match<Property>(x => x.Id == SonarProperties.TruststorePassword && x.Value == input);
        config.ScannerOptsSettings.Should().ContainSingle();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStorePassword", expected, config);
    }

    private static void AssertExpectedScannerOptsSettings(string key, string expectedValue, AnalysisConfig actualConfig)
    {
        var found = Property.TryGetProperty(key, actualConfig.ScannerOptsSettings, out var property);
        found.Should().BeTrue("Expected local property was not found. Key: {0}", key);
        property.Value.Should().Be(expectedValue, "Unexpected local value. Key: {0}", key);
    }

    private static PropertyAsScannerOptsMappingProcessor CreateProcessor(ProcessedArgs args, bool isUnix = false)
    {
        var operatingSystemProvider = Substitute.For<IOperatingSystemProvider>();
        operatingSystemProvider.IsUnix().Returns(isUnix);
        return new PropertyAsScannerOptsMappingProcessor(args, null, operatingSystemProvider);
    }

    private static ProcessedArgs CreateProcessedArgs(IAnalysisPropertyProvider cmdLineProvider = null, IFileWrapper fileWrapper = null) =>
        new("valid.key",
            "valid.name",
            "1.0",
            "organization",
            false,
            cmdLineProvider ?? Substitute.For<IAnalysisPropertyProvider>(),
            Substitute.For<IAnalysisPropertyProvider>(),
            EmptyPropertyProvider.Instance,
            fileWrapper ?? Substitute.For<IFileWrapper>(),
            Substitute.For<IDirectoryWrapper>(),
            Substitute.For<IOperatingSystemProvider>(),
            Substitute.For<ILogger>());
}
