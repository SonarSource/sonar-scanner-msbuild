//-----------------------------------------------------------------------
// <copyright file="CoverageReportUrlProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.TestManagement.Client;
using Sonar.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace Sonar.TeamBuild.Integration
{
    internal class CoverageReportUrlProvider : ICoverageUrlProvider
    {
        /// <summary>
        /// Builds and returns the download URLs for all code coverage reports for the specified build
        /// </summary>
        public IEnumerable<string> GetCodeCoverageReportUrls(string tfsUri, string buildUri, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(tfsUri))
            {
                throw new ArgumentNullException("tfsUri");
            }
            if (string.IsNullOrWhiteSpace(buildUri))
            {
                throw new ArgumentNullException("buildUri");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            List<string> urls = new List<string>();

            logger.LogMessage("Connecting to TFS...");
            using (TfsTeamProjectCollection collection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(tfsUri)))
            {
                IBuildServer buildServer = collection.GetService<IBuildServer>();

                logger.LogMessage("Fetching build information ...");
                IBuildDetail build = buildServer.GetMinimalBuildDetails(new Uri(buildUri));
                string projectName = build.TeamProject;

                logger.LogMessage("Fetching coverage information...");
                ITestManagementService tcm = collection.GetService<ITestManagementService>();
                ITestManagementTeamProject testProject = tcm.GetTeamProject(projectName);

                // TODO: investigate further. It looks as if we might be requesting the coverage reports
                // before the service is able to provide them.
                // For the time being, we're retrying with a time out.
                int timeoutInMs = 10000;
                int retryPeriodinMs = 2000;
                IBuildCoverage[] coverages = null;
                Retry(timeoutInMs, retryPeriodinMs, logger, () => { return TryGetCoverageInfo(testProject, buildUri, out coverages); });

                foreach (IBuildCoverage coverage in coverages)
                {
                    logger.LogMessage("Coverage Id: {0}, Platform {1}, Flavor {2}", coverage.Configuration.Id, coverage.Configuration.BuildPlatform, coverage.Configuration.BuildPlatform);

                    string coverageFileUrl = CoverageReportUrlProvider.GetCoverageUri(build, coverage);
                    Debug.WriteLine(coverageFileUrl);
                    urls.Add(coverageFileUrl);
                }
            }

            logger.LogMessage("...done.");
            return urls;
        }

        private static bool TryGetCoverageInfo(ITestManagementTeamProject testProject, string buildUri, out IBuildCoverage[] coverageInfo)
        {
            coverageInfo = testProject.CoverageAnalysisManager.QueryBuildCoverage(buildUri, CoverageQueryFlags.Modules);

            return coverageInfo != null && coverageInfo.Length > 0;
        }

        private static void Retry(int maxDelayMs, int pauseBetweenTriesMs, ILogger logger, Func<bool> op)
        {
            Stopwatch timer = Stopwatch.StartNew();
            bool succeeded = op();

            while (!succeeded && timer.ElapsedMilliseconds < maxDelayMs)
            {
                System.Threading.Thread.Sleep(pauseBetweenTriesMs);
                succeeded = op();
            }

            timer.Stop();

            if (succeeded)
            {
                logger.LogMessage("Operation succeeded. Elapsed time (ms): {0}", timer.ElapsedMilliseconds);
            }
            else
            {
                logger.LogMessage("Operation timed out, Elapsed time (ms): {0}", timer.ElapsedMilliseconds);
            }
        }

        private static string GetCoverageUri(IBuildDetail buildDetail, IBuildCoverage buildCoverage)
        {
            string serverPath = string.Format(CultureInfo.InvariantCulture, "/BuildCoverage/{0}.{1}.{2}.{3}.coverage",
                                    buildDetail.BuildNumber,
                                    buildCoverage.Configuration.BuildFlavor,
                                    buildCoverage.Configuration.BuildPlatform,
                                    buildCoverage.Configuration.Id);

            string coverageFileUrl = String.Format(CultureInfo.InvariantCulture, "{0}/{1}/_api/_build/ItemContent?buildUri={2}&path={3}",
                                           buildDetail.BuildServer.TeamProjectCollection.Uri.AbsoluteUri,
                                           Uri.EscapeDataString(buildDetail.TeamProject),
                                           Uri.EscapeDataString(buildDetail.Uri.AbsoluteUri),
                                           Uri.EscapeDataString(serverPath));

            return coverageFileUrl;
        }

    }
}
