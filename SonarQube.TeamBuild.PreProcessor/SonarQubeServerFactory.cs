//-----------------------------------------------------------------------
// <copyright file="SonarQubeServerFactory.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Net;

namespace SonarQube.TeamBuild.PreProcessor
{
    internal class SonarQubeServerFactory : ISonarQubeServerFactory
    {
        public ISonarQubeServer Create(ProcessedArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }

            return new SonarWebService(GetDownloader(args), args.GetSetting(SonarProperties.HostUrl));
        }

        private static IDownloader GetDownloader(ProcessedArgs args)
        {
            string username = args.GetSetting(SonarProperties.SonarUserName, null);
            string password = args.GetSetting(SonarProperties.SonarPassword, null);

            return new WebClientDownloader(new WebClient(), username, password);
        }

    }
}
