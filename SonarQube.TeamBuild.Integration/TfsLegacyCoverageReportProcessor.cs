/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using SonarQube.Common;
using SonarQube.TeamBuild.Integration.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SonarQube.TeamBuild.Integration
{
    public class TfsLegacyCoverageReportProcessor : CoverageReportProcessorBase
    {
        public const string DownloadFileName = "VSCodeCoverageReport.coverage"; // was internal

        private readonly ICoverageUrlProvider urlProvider;
        private readonly ICoverageReportDownloader downloader;

        public TfsLegacyCoverageReportProcessor()
            : this(new CoverageReportUrlProvider(), new CoverageReportDownloader(), new CoverageReportConverter())
        {
        }

        public TfsLegacyCoverageReportProcessor(ICoverageUrlProvider urlProvider, ICoverageReportDownloader downloader, ICoverageReportConverter converter) // was internal
            : base(converter)
        {
            if (urlProvider == null)
            {
                throw new ArgumentNullException("urlProvider");
            }
            if (downloader == null)
            {
                throw new ArgumentNullException("downloader");
            }

            this.urlProvider = urlProvider;
            this.downloader = downloader;
        }

        #region Virtual methods

        protected override bool TryGetBinaryReportFile(AnalysisConfig config, ITeamBuildSettings settings, ILogger logger, out string binaryFilePath)
        {
            IEnumerable<string> urls = this.urlProvider.GetCodeCoverageReportUrls(config.GetTfsUri(), config.GetBuildUri(), logger);
            Debug.Assert(urls != null, "Not expecting the returned list of urls to be null");

            bool continueProcessing = true;
            binaryFilePath = null;

            switch (urls.Count())
            {
                case 0:
                    logger.LogInfo(Resources.PROC_DIAG_NoCodeCoverageReportsFound);
                    break;

                case 1:
                    string url = urls.First();

                    string targetFileName = Path.Combine(config.SonarOutputDir, DownloadFileName);
                    bool result = this.downloader.DownloadReport(config.GetTfsUri(), url, targetFileName, logger);

                    if (result)
                    {
                        binaryFilePath = targetFileName;
                    }
                    else
                    {
                        continueProcessing = false;
                        logger.LogError(Resources.PROC_ERROR_FailedToDownloadReport);
                    }
                    break;

                default: // More than one
                    continueProcessing = false;
                    logger.LogError(Resources.PROC_ERROR_MultipleCodeCoverageReportsFound);
                    break;
            }

            return continueProcessing;
        }

        #endregion
    }
}
