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
        /// <param name="ws">SonarQube Web Service instance</param>
        /// <param name="sonarProjectKey">The key of the Sonar project for which the properties should be fetched</param>
        IDictionary<string, string> FetchProperties(SonarWebService ws, string sonarProjectKey);
    }
}
