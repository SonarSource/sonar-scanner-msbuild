//-----------------------------------------------------------------------
// <copyright file="IPropertiesFetcher.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------


using System.Collections.Generic;

namespace Sonar.TeamBuild.PreProcessor
{
    public interface IPropertiesFetcher
    {
        /// <summary>
        /// Retrieves the effective SonarQube properties of a given (possible not yet existing) project
        /// </summary>
        /// <param name="sonarProjectKey">The key of the Sonar project for which the properties should be fetched</param>
        /// <param name="sonarUrl">The url of the SonarQube server</param>
        /// <param name="userName">The user name for the server (optional)</param>
        /// <param name="password">The password for the server (optional)</param>
        IDictionary<string, string> FetchProperties(string sonarProjectKey, string sonarUrl, string userName, string password);
    }
}
