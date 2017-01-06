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

using Microsoft.TeamFoundation.Client;
using Microsoft.VisualStudio.Services.Common;
using SonarQube.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace SonarQube.TeamBuild.Integration
{
    internal class CoverageReportDownloader : ICoverageReportDownloader
    {
        public bool DownloadReport(string tfsUri, string reportUrl, string newFullFileName, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(tfsUri))
            {
                throw new ArgumentNullException("tfsUri");
            }
            if (string.IsNullOrWhiteSpace(reportUrl))
            {
                throw new ArgumentNullException("reportUrl");
            }
            if (string.IsNullOrWhiteSpace(newFullFileName))
            {
                throw new ArgumentNullException("newFullFileName");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            string downloadDir = Path.GetDirectoryName(newFullFileName);
            Utilities.EnsureDirectoryExists(downloadDir, logger);

            InternalDownloadReport(tfsUri, reportUrl, newFullFileName, logger);

            return true;
        }

        private void InternalDownloadReport(string tfsUri, string reportUrl, string reportDestinationPath, ILogger logger)
        {
            VssHttpMessageHandler vssHttpMessageHandler = GetHttpHandler(tfsUri, logger);

            logger.LogInfo(Resources.DOWN_DIAG_DownloadCoverageReportFromTo, reportUrl, reportDestinationPath);

            using (HttpClient httpClient = new HttpClient(vssHttpMessageHandler))
            using (HttpResponseMessage response = httpClient.GetAsync(reportUrl).Result)
            {
                if (response.IsSuccessStatusCode)
                {
                    using (FileStream fileStream = new FileStream(reportDestinationPath, FileMode.Create, FileAccess.Write))
                    {
                        response.Content.CopyToAsync(fileStream).Wait();
                    }
                }
                else
                {
                    logger.LogError(Resources.PROC_ERROR_FailedToDownloadReportReason, reportUrl, response.StatusCode, response.ReasonPhrase);
                }
            }
        }

        private VssHttpMessageHandler GetHttpHandler(string tfsUri, ILogger logger)
        {
            VssCredentials vssCreds;
            Uri tfsCollectionUri = new Uri(tfsUri);

            using (TfsTeamProjectCollection collection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(tfsCollectionUri))
            {
                // Build agents run non-attended and most often non-interactive so make sure not to create a credential prompt
                collection.ClientCredentials.AllowInteractive = false;
                collection.EnsureAuthenticated();

                logger.LogInfo(Resources.DOWN_DIAG_ConnectedToTFS, tfsUri);

                // We need VSS credentials that encapsulate all types of credentials (NetworkCredentials for TFS, OAuth for VSO)
                TfsConnection connection = collection as TfsConnection;
                vssCreds = connection.ClientCredentials.ConvertToVssCredentials(tfsCollectionUri);
            }

            Debug.Assert(vssCreds != null, "Not expecting ConvertToVssCredentials ");
            VssHttpMessageHandler vssHttpMessageHandler = new VssHttpMessageHandler(vssCreds, new VssHttpRequestSettings());

            return vssHttpMessageHandler;
        }
    }
}