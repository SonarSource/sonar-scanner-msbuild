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
        #region Public methods

        public IDictionary<string, string> FetchProperties(SonarWebService ws, string sonarProjectKey)
        {
            if (string.IsNullOrWhiteSpace(sonarProjectKey))
            {
                throw new ArgumentNullException("sonarProjectKey");
            }         

            return ws.GetProperties(sonarProjectKey);
        }

        #endregion
    }
}
