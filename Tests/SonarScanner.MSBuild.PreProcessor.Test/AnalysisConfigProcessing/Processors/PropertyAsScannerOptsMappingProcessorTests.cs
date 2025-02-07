using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.AnalysisConfigProcessing.Processors;

namespace SonarScanner.MSBuild.PreProcessor.Test.AnalysisConfigProcessing.Processors;

[TestClass]
public class PropertyAsScannerOptsMappingProcessorTests
{
    [TestMethod]
    public void Update_TrustStorePropertiesNullValue_Mapped()
    {
        // Arrange
        var processor = CreateProcessor(isUnix: false);
        var config = new AnalysisConfig { LocalSettings = [new Property("sonar.scanner.truststorePath", null)] };

        // Act
        processor.Update(config);

        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", null, config);
        Property.TryGetProperty("javax.net.ssl.trustStore", config.LocalSettings, out _).Should().BeFalse();
    }

    [TestMethod]
    public void Update_TrustStorePropertiesValue_Mapped()
    {
        // Arrange
        var processor = CreateProcessor(isUnix: false);
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

    [DataTestMethod]
    [DataRow(SonarProperties.Verbose, "true")]
    [DataRow(SonarProperties.Organization, "org")]
    [DataRow(SonarProperties.HostUrl, "http://localhost:9000")]
    [DataRow(SonarProperties.HostUrl, @"http://localhost:9000\")]
    public void Update_UnmappedProperties(string id, string value)
    {
        // Arrange
        var processor = CreateProcessor(isUnix: false);
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
        var processor = CreateProcessor(isUnix: false);
        var config = new AnalysisConfig { LocalSettings = [new Property("sonar.scanner.truststorePath", input)] };

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", expected, config);
        Property.TryGetProperty("sonar.scanner.truststorePath", config.LocalSettings, out _).Should().BeFalse();
    }

    [DataTestMethod]
    [DataRow(@"%%${}?&@", @"""%%${}?&@""")]
    [DataRow(@"\", @"""\\""")]
    [DataRow(@" "" ", @""" \"" """)]
    [DataRow(@"|", @"""|""")]
    [DataRow(@"|""", @"""^|\""""")]
    public void Update_MapsTruststorePasswordToScannerOpts_Windows(string input, string expected)
    {
        // Arrange
        var processor = CreateProcessor(isUnix: false);
        var config = new AnalysisConfig { LocalSettings = [new Property("sonar.scanner.truststorePassword", input)] };

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStorePassword", expected, config);
        Property.TryGetProperty("sonar.scanner.truststorePassword", config.LocalSettings, out _).Should().BeFalse();
    }

    [DataTestMethod]
    [DataRow("/path/to/truststore.pfx", "/path/to/truststore.pfx")]
    [DataRow("/path/to/my trustore.pfx", "/path/to/my trustore.pfx")]
    public void Update_MapsTruststorePathToScannerOpts_Linux(string input, string expected)
    {
        // Arrange
        var processor = CreateProcessor(isUnix: true);
        var config = new AnalysisConfig { LocalSettings = [new Property("sonar.scanner.truststorePath", input)] };

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStore", expected, config);
        Property.TryGetProperty("sonar.scanner.truststorePath", config.LocalSettings, out _).Should().BeFalse();
    }

    [DataTestMethod]
    [DataRow("%|^ \"`&%OS%${PATH}@", "%|^ \"`&%OS%${PATH}@")]
    public void Update_MapsTruststorePasswordToScannerOpts_Linux(string input, string expected)
    {
        // Arrange
        var processor = CreateProcessor(isUnix: true);
        var config = new AnalysisConfig { LocalSettings = [new Property("sonar.scanner.truststorePassword", input)] };

        // Act
        processor.Update(config);

        // Assert
        config.LocalSettings.Should().BeEmpty();
        config.ScannerOptsSettings.Should().ContainSingle();
        AssertExpectedScannerOptsSettings("javax.net.ssl.trustStorePassword", expected, config);
        Property.TryGetProperty("sonar.scanner.truststorePassword", config.LocalSettings, out _).Should().BeFalse();
    }

    private static void AssertExpectedScannerOptsSettings(string key, string expectedValue, AnalysisConfig actualConfig)
    {
        var found = Property.TryGetProperty(key, actualConfig.ScannerOptsSettings, out var property);
        found.Should().BeTrue("Expected local property was not found. Key: {0}", key);
        property.Value.Should().Be(expectedValue, "Unexpected local value. Key: {0}", key);
    }

    private static PropertyAsScannerOptsMappingProcessor CreateProcessor(bool isUnix = false)
    {
        var operatingSystemProvider = Substitute.For<IOperatingSystemProvider>();
        operatingSystemProvider.IsUnix().Returns(isUnix);
        return new PropertyAsScannerOptsMappingProcessor(null, null, operatingSystemProvider);
    }
}
