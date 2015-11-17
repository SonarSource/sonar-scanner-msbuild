//-----------------------------------------------------------------------
// <copyright file="SonarQubeServerFactory.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;

namespace SonarQube.TeamBuild.PreProcessor
{
    internal class SonarQubeServerFactory : ISonarQubeServerFactory
    {
        public ISonarQubeServer Create(ProcessedArgs args, ILogger logger)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            return new SonarWebService(GetDownloader(args), args.GetSetting(SonarProperties.HostUrl), logger);
        }

        private static IDownloader GetDownloader(ProcessedArgs args)
        {
            string username = args.GetSetting(SonarProperties.SonarUserName, null);
            string password = args.GetSetting(SonarProperties.SonarPassword, null);

            return new WebClientDownloader(username, password);
        }

    }
}
