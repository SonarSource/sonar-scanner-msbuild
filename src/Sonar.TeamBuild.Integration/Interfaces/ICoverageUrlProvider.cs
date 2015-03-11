//-----------------------------------------------------------------------
// <copyright file="ICoverageUrlProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using System.Collections.Generic;

namespace Sonar.TeamBuild.Integration
{
    public interface ICoverageUrlProvider
    {
        /// <summary>
        /// Builds and returns the download URLs for all code coverage reports for the specified build
        /// </summary>
        /// <param name="tfsUri">The URI of the TFS collection</param>
        /// <parparam name="buildUri">The URI of the build for which data should be retrieved</parparam>
        IEnumerable<string> GetCodeCoverageReportUrls(string tfsUri, string buildUri, ILogger logger);
    }
}
