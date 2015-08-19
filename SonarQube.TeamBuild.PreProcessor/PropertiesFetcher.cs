//-----------------------------------------------------------------------
// <copyright file="PropertiesFetcher.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor
{
    public class PropertiesFetcher : IPropertiesFetcher
    {
        #region Public methods

        public IDictionary<string, string> FetchProperties(SonarWebService ws, string sonarProjectKey, ILogger logger)
        {
            if (ws == null)
            {
                throw new ArgumentNullException("ws");
            }    
            if (string.IsNullOrWhiteSpace(sonarProjectKey))
            {
                throw new ArgumentNullException("sonarProjectKey");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            return ws.GetProperties(sonarProjectKey, logger);
        }

        #endregion
    }
}
