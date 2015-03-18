//-----------------------------------------------------------------------
// <copyright file="PropertiesFetcher.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Collections.Generic;

namespace Sonar.TeamBuild.PreProcessor
{
    public class PropertiesFetcher : IPropertiesFetcher
    {
        // TODO: Remove
        private const string Language = "cs";
        private const string Repository = "fxcop";

        #region Public methods

        public IDictionary<string, string> FetchProperties(string sonarProjectKey, string sonarUrl, string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(sonarProjectKey))
            {
                throw new ArgumentNullException("sonarProjectKey");
            }         
            if (string.IsNullOrWhiteSpace(sonarUrl))
            {
                throw new ArgumentNullException("sonarUrl");
            }

            using (SonarWebService ws = new SonarWebService(new WebClientDownloader(new WebClient(), userName, password), sonarUrl, Language, Repository))
            {
                return ws.GetProperties(sonarProjectKey);
            }
        }

        #endregion
    }
}
