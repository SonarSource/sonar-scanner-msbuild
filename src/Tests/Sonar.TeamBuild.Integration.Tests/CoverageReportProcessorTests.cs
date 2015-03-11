//-----------------------------------------------------------------------
// <copyright file="CoverageReportProcessorTests.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sonar.TeamBuild.Integration.Tests.Infrastructure;
using Sonar.Common;

namespace Sonar.TeamBuild.Integration.Tests
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
    public class CoverageReportProcessorTests
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
            AnalysisContext context = this.CreateValidContext();

            CoverageReportProcessor processor = new CoverageReportProcessor(urlProvider, downloader, converter);

            // Act
            bool result = processor.ProcessCoverageReports(context);

            // Assert
            urlProvider.AssertGetUrlsNotCalled();
            downloader.AssertDownloadNotCalled();
            converter.AssertConvertNotCalled();
            Assert.IsFalse(result, "Expecting result to be false as files could not be converted");
        }

        [TestMethod]
        [TestCategory("CodeCoverage")]
        public void ReportProcessor_NoUrlsFound()
        {
            // Arrange
            MockReportUrlProvider urlProvider = new MockReportUrlProvider() { UrlsToReturn = new string[] { } };
            MockReportDownloader downloader = new MockReportDownloader();
            MockReportConverter converter = new MockReportConverter() { CanConvert = true };
            AnalysisContext context = this.CreateValidContext();

            CoverageReportProcessor processor = new CoverageReportProcessor(urlProvider, downloader, converter);

            // Act
            bool result = processor.ProcessCoverageReports(context);

            // Assert
            urlProvider.AssertGetUrlsCalled();
            downloader.AssertDownloadNotCalled(); // no urls returned, so should go any further
            converter.AssertConvertNotCalled();
            Assert.IsTrue(result, "Expecting true: no coverage reports is a valid scenario");
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
            AnalysisContext context = this.CreateValidContext();

            CoverageReportProcessor processor = new CoverageReportProcessor(urlProvider, downloader, converter);

            // Act
            bool result = processor.ProcessCoverageReports(context);

            // Assert
            urlProvider.AssertGetUrlsCalled();
            downloader.AssertDownloadNotCalled(); // Multiple urls so should early out
            converter.AssertConvertNotCalled();
            Assert.IsFalse(result, "Expecting false: can't process multiple coverage reports");
        
            // TODO: check a warning is emitted
        }

        [TestMethod]
        [TestCategory("CodeCoverage")]
        public void ReportProcessor_SingleUrlFound_NotDownloaded()
        {
            // Arrange
            MockReportUrlProvider urlProvider = new MockReportUrlProvider() { UrlsToReturn = new string[] { ValidUrl1} };
            MockReportDownloader downloader = new MockReportDownloader();
            MockReportConverter converter = new MockReportConverter() { CanConvert = true };
            AnalysisContext context = this.CreateValidContext();

            CoverageReportProcessor processor = new CoverageReportProcessor(urlProvider, downloader, converter);

            // TODO - download failure should raise an exception

            // Act
            bool result = processor.ProcessCoverageReports(context);

            // Assert
            urlProvider.AssertGetUrlsCalled();
            downloader.AssertExpectedDownloads(1);
            converter.AssertConvertNotCalled();
            Assert.IsFalse(result, "Expecting false: report could not be downloaded");
        }

        [TestMethod]
        [TestCategory("CodeCoverage")]
        public void ReportProcessor_SingleUrlFound_DownloadedOk()
        {
            // Arrange
            MockReportUrlProvider urlProvider = new MockReportUrlProvider() { UrlsToReturn = new string[] { ValidUrl1 } };
            MockReportDownloader downloader = new MockReportDownloader();
            MockReportConverter converter = new MockReportConverter() { CanConvert = true };
            AnalysisContext context = this.CreateValidContext();

            downloader.CreateFileOnDownloadRequest = true;

            CoverageReportProcessor processor = new CoverageReportProcessor(urlProvider, downloader, converter);

            // Act
            bool result = processor.ProcessCoverageReports(context);

            // Assert
            urlProvider.AssertGetUrlsCalled();
            downloader.AssertExpectedDownloads(1);
            converter.AssertExpectedNumberOfConversions(1);
            Assert.IsTrue(result, "Expecting true: happy path");
        }

        #endregion

        #region Private methods

        private AnalysisContext CreateValidContext()
        {
            AnalysisContext context = new AnalysisContext()
            {
                Logger = new ConsoleLogger(),
                SonarOutputDir = this.TestContext.DeploymentDirectory, // tests can write to this directory
                SonarConfigDir = this.TestContext.TestRunResultsDirectory, // we don't read anything from this directory, we just want it to be different from the output directory
            };
            return context;
        }

        #endregion
    }
}
