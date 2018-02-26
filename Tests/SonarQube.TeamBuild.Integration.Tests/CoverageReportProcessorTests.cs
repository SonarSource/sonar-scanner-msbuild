using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarQube.Common;
using TestUtilities;

namespace SonarQube.TeamBuild.Integration.Tests
{
    [TestClass]
    public class CoverageReportProcessorTests
    {
        private Mock<ICoverageReportConverter> converterMock;
        private Mock<ICoverageReportLocator> locatorMock;
        private TestLogger logger;
        private CoverageReportProcessor processor;

        [TestInitialize]
        public void TestInitialize()
        {
            converterMock = new Mock<ICoverageReportConverter>(MockBehavior.Strict);
            locatorMock = new Mock<ICoverageReportLocator>(MockBehavior.Strict);
            logger = new TestLogger();
            processor = new CoverageReportProcessor(converterMock.Object, locatorMock.Object, logger);
        }

        [TestMethod]
        public void Initialize_Checks_Arguments_For_Null()
        {
            // Arrange
            var analysisConfig = new AnalysisConfig();
            var settingsMock = new Mock<ITeamBuildSettings>();

            // Act
            new Action(() => processor.Initialise(null, settingsMock.Object))
                .ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("config");
            new Action(() => processor.Initialise(analysisConfig, null))
                .ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("settings");
        }

        [TestMethod]
        public void Initialize_Calls_Converter_Initialize()
        {
            // Arrange
            var analysisConfig = new AnalysisConfig();
            var settingsMock = new Mock<ITeamBuildSettings>();

            converterMock.Setup(x => x.Initialize(logger)).Returns(true);

            // Act
            var result = processor.Initialise(analysisConfig, settingsMock.Object);

            // Assert
            result.Should().BeTrue();
            converterMock.Verify(x => x.Initialize(logger), Times.Once());
        }

        [TestMethod]
        public void ProcessCoverageReports_NotInitialized_Throws()
        {
            // Act
            new Action(() => processor.ProcessCoverageReports())
                .ShouldThrow<InvalidOperationException>();
        }

        [TestMethod]
        public void ProcessCoverageReports_NoBinaryCoverage_NoTRX_Returns_True()
        {
            // Arrange
            var analysisConfig = new AnalysisConfig { LocalSettings = new AnalysisProperties() };
            var settingsMock = new Mock<ITeamBuildSettings>();
            converterMock.Setup(x => x.Initialize(logger)).Returns(true);
            processor.Initialise(analysisConfig, settingsMock.Object);

            string binaryPath = "";
            locatorMock
                .Setup(x => x.TryGetBinaryCoveragePath(analysisConfig, settingsMock.Object, out binaryPath))
                .Returns(true);

            string testResultsPath = "";
            locatorMock
                .Setup(x => x.TryGetTestResultsPath(analysisConfig, settingsMock.Object, out testResultsPath))
                .Returns(false);

            // Act
            var result = processor.ProcessCoverageReports();

            // Assert
            result.Should().BeTrue();
            logger.AssertInfoMessageExists("Fetching code coverage report information from TFS...");
        }

        [TestMethod]
        public void ProcessCoverageReports_NoBinaryCoverage_Adds_TRX_Path_To_Settings_Returns_True()
        {
            // Arrange
            var analysisConfig = new AnalysisConfig { LocalSettings = new AnalysisProperties() };
            var settingsMock = new Mock<ITeamBuildSettings>();
            converterMock.Setup(x => x.Initialize(logger)).Returns(true);
            processor.Initialise(analysisConfig, settingsMock.Object);

            string binaryPath = "";
            locatorMock
                .Setup(x => x.TryGetBinaryCoveragePath(analysisConfig, settingsMock.Object, out binaryPath))
                .Returns(true);

            string testResultsPath = "trx path";
            locatorMock
                .Setup(x => x.TryGetTestResultsPath(analysisConfig, settingsMock.Object, out testResultsPath))
                .Returns(true);

            // Act
            var result = processor.ProcessCoverageReports();

            // Assert
            result.Should().BeTrue();
            analysisConfig.LocalSettings[0].Id.Should().Be("sonar.cs.vstest.reportsPaths");
            analysisConfig.LocalSettings[0].Value.Should().Be("trx path");
            logger.AssertInfoMessageExists("Fetching code coverage report information from TFS...");
        }

        [TestMethod]
        public void ProcessCoverageReports_NoBinaryCoverage_TRX_Path_Already_Provided_Returns_True()
        {
            // Arrange
            var analysisConfig = new AnalysisConfig { LocalSettings = new AnalysisProperties() };
            var settingsMock = new Mock<ITeamBuildSettings>();
            converterMock.Setup(x => x.Initialize(logger)).Returns(true);
            processor.Initialise(analysisConfig, settingsMock.Object);

            string binaryPath = "";
            locatorMock
                .Setup(x => x.TryGetBinaryCoveragePath(analysisConfig, settingsMock.Object, out binaryPath))
                .Returns(true);

            analysisConfig.LocalSettings.Add(new Property { Id = "sonar.cs.vstest.reportsPaths", Value = "trx path" });

            // Act
            var result = processor.ProcessCoverageReports();

            // Assert
            result.Should().BeTrue();
            logger.AssertInfoMessageExists("Fetching code coverage report information from TFS...");
            // TryGetTestResultsPath is not called; no need to check because the mock is strict
        }

        [TestMethod]
        public void ProcessCoverageReports_BinaryCoverage_NoTRX_Returns_True()
        {
            // Arrange
            var analysisConfig = new AnalysisConfig { LocalSettings = new AnalysisProperties() };
            var settingsMock = new Mock<ITeamBuildSettings>();
            converterMock.Setup(x => x.Initialize(logger)).Returns(true);
            processor.Initialise(analysisConfig, settingsMock.Object);

            string binaryFileName = "binary path";
            string binaryPath = $"{binaryFileName}.coverage";
            locatorMock
                .Setup(x => x.TryGetBinaryCoveragePath(analysisConfig, settingsMock.Object, out binaryPath))
                .Returns(true);

            string testResultsPath = "";
            locatorMock
                .Setup(x => x.TryGetTestResultsPath(analysisConfig, settingsMock.Object, out testResultsPath))
                .Returns(false);

            string xmlPath = $"{binaryFileName}.coveragexml";
            converterMock
                .Setup(x => x.ConvertToXml(binaryPath, xmlPath, logger))
                .Returns(true);

            // Act
            var result = processor.ProcessCoverageReports();

            // Assert
            result.Should().BeTrue();
            analysisConfig.LocalSettings[0].Id.Should().Be("sonar.cs.vscoveragexml.reportsPaths");
            analysisConfig.LocalSettings[0].Value.Should().Be($"{binaryFileName}.coveragexml");
            logger.AssertInfoMessageExists("Fetching code coverage report information from TFS...");
        }
    }
}
