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
                IBuildCoverage[] coverages = testProject.CoverageAnalysisManager.QueryBuildCoverage(buildUri, CoverageQueryFlags.Modules);

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
