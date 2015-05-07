//-----------------------------------------------------------------------
// <copyright file="TfsLegacyCoverageReportProcessorTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.Integration.Tests.Infrastructure;
using SonarQube.Common;
using System.IO;
using TestUtilities;

namespace SonarQube.TeamBuild.Integration.Tests
{
    /*
     * Scenarios:
     * - happy path: one report url, downloads ok, converted ok
     * - no report urls -> success
     * - multiple report urls -> warning, only one downloaded
     * - can't convert files -> no download
     * - failures - exceptions at each stage
     */

    /// <summary>
    /// Unit tests for the orchestration of the code coverage handling
    /// </summary>
    [TestClass]
    public class TfsLegacyCoverageReportProcessorTests
    {
        private const string ValidUrl1 = "vstsf:///foo";
        private const string ValidUrl2 = "vstsf:///foo2";

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [TestCategory("CodeCoverage")]
        [Description("Should early out if the files can't be converted")]
        public void ReportProcessor_CannotConvertFiles()
        {
            // Arrange
            MockReportUrlProvider urlProvider = new MockReportUrlProvider() { UrlsToReturn = new string[] { ValidUrl1 } };
            MockReportDownloader downloader = new MockReportDownloader();
            MockReportConverter converter = new MockReportConverter() { CanConvert = false };
            AnalysisConfig context = this.CreateValidContext();
            TeamBuildSettings settings = this.CreateValidSettings();
            TestLogger logger = new TestLogger();

            TfsLegacyCoverageReportProcessor processor = new TfsLegacyCoverageReportProcessor(urlProvider, downloader, converter);

            // Act
            bool result = processor.ProcessCoverageReports(context, settings, logger);

            // Assert
            urlProvider.AssertGetUrlsNotCalled();
            downloader.AssertDownloadNotCalled();
            converter.AssertConvertNotCalled();
            Assert.IsFalse(result, "Expecting result to be false as files could not be converted");

            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        [TestCategory("CodeCoverage")]
        public void ReportProcessor_NoUrlsFound()
        {
            // Arrange
            MockReportUrlProvider urlProvider = new MockReportUrlProvider() { UrlsToReturn = new string[] { } };
            MockReportDownloader downloader = new MockReportDownloader();
            MockReportConverter converter = new MockReportConverter() { CanConvert = true };
            AnalysisConfig context = this.CreateValidContext();
            TeamBuildSettings settings = this.CreateValidSettings();
            TestLogger logger = new TestLogger();

            TfsLegacyCoverageReportProcessor processor = new TfsLegacyCoverageReportProcessor(urlProvider, downloader, converter);

            // Act
            bool result = processor.ProcessCoverageReports(context, settings, logger);

            // Assert
            urlProvider.AssertGetUrlsCalled();
            downloader.AssertDownloadNotCalled(); // no urls returned, so should go any further
            converter.AssertConvertNotCalled();
            Assert.IsTrue(result, "Expecting true: no coverage reports is a valid scenario");

            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        [TestCategory("CodeCoverage")]
        [Description("Should early out if multiple reports are found")]
        public void ReportProcessor_MultipleUrlsFound()
        {
            // Arrange
            MockReportUrlProvider urlProvider = new MockReportUrlProvider() { UrlsToReturn = new string[] { ValidUrl1, ValidUrl2 } };
            MockReportDownloader downloader = new MockReportDownloader();
            MockReportConverter converter = new MockReportConverter() { CanConvert = true };
            AnalysisConfig context = this.CreateValidContext();
            TeamBuildSettings settings = this.CreateValidSettings();
            TestLogger logger = new TestLogger();

            TfsLegacyCoverageReportProcessor processor = new TfsLegacyCoverageReportProcessor(urlProvider, downloader, converter);

            // Act
            bool result = processor.ProcessCoverageReports(context, settings, logger);

            // Assert
            urlProvider.AssertGetUrlsCalled();
            downloader.AssertDownloadNotCalled(); // Multiple urls so should early out
            converter.AssertConvertNotCalled();
            Assert.IsFalse(result, "Expecting false: can't process multiple coverage reports");

            logger.AssertErrorsLogged(1);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        [TestCategory("CodeCoverage")]
        public void ReportProcessor_SingleUrlFound_NotDownloaded()
        {
            // Arrange
            MockReportUrlProvider urlProvider = new MockReportUrlProvider() { UrlsToReturn = new string[] { ValidUrl1} };
            MockReportDownloader downloader = new MockReportDownloader();
            MockReportConverter converter = new MockReportConverter() { CanConvert = true };
            AnalysisConfig context = this.CreateValidContext();
            TeamBuildSettings settings = this.CreateValidSettings();
            TestLogger logger = new TestLogger();

            TfsLegacyCoverageReportProcessor processor = new TfsLegacyCoverageReportProcessor(urlProvider, downloader, converter);

            // Act
            bool result = processor.ProcessCoverageReports(context, settings, logger);

            // Assert
            urlProvider.AssertGetUrlsCalled();
            downloader.AssertExpectedDownloads(1);
            converter.AssertConvertNotCalled();

            downloader.AssertExpectedUrlsRequested(ValidUrl1);

            Assert.IsFalse(result, "Expecting false: report could not be downloaded");
            logger.AssertErrorsLogged(1);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        [TestCategory("CodeCoverage")]
        public void ReportProcessor_SingleUrlFound_DownloadedOk()
        {
            // Arrange
            MockReportUrlProvider urlProvider = new MockReportUrlProvider() { UrlsToReturn = new string[] { ValidUrl2 } };
            MockReportDownloader downloader = new MockReportDownloader();
            MockReportConverter converter = new MockReportConverter() { CanConvert = true };
            AnalysisConfig context = this.CreateValidContext();
            TeamBuildSettings settings = this.CreateValidSettings();
            TestLogger logger = new TestLogger();

            downloader.CreateFileOnDownloadRequest = true;

            TfsLegacyCoverageReportProcessor processor = new TfsLegacyCoverageReportProcessor(urlProvider, downloader, converter);

            // Act
            bool result = processor.ProcessCoverageReports(context, settings, logger);

            // Assert
            urlProvider.AssertGetUrlsCalled();
            downloader.AssertExpectedDownloads(1);
            converter.AssertExpectedNumberOfConversions(1);

            downloader.AssertExpectedUrlsRequested(ValidUrl2);
            downloader.AssertExpectedTargetFileNamesSupplied(Path.Combine(context.SonarOutputDir, TfsLegacyCoverageReportProcessor.DownloadFileName));
            Assert.IsTrue(result, "Expecting true: happy path");

            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);
        }

        #endregion

        #region Private methods

        private AnalysisConfig CreateValidContext()
        {
            AnalysisConfig context = new AnalysisConfig()
            {
                SonarOutputDir = this.TestContext.DeploymentDirectory, // tests can write to this directory
                SonarConfigDir = this.TestContext.TestRunResultsDirectory, // we don't read anything from this directory, we just want it to be different from the output directory
            };
            return context;
        }

        private TeamBuildSettings CreateValidSettings()
        {
            TeamBuildSettings settings = new TeamBuildSettings();
            
            return settings;
        }

        #endregion
    }
}
