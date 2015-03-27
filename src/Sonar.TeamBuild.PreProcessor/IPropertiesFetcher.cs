//-----------------------------------------------------------------------
// <copyright file="IPropertiesFetcher.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor
{
    public interface IPropertiesFetcher
    {
        /// <summary>
        /// Retrieves the effective SonarQube properties of a given (possible not yet existing) project
        /// </summary>
        /// <param name="ws">SonarQube Web Service instance</param>
        /// <param name="sonarProjectKey">The key of the SonarQube project for which the properties should be fetched</param>
        IDictionary<string, string> FetchProperties(SonarWebService ws, string sonarProjectKey);
    }
}
