//-----------------------------------------------------------------------
// <copyright file="PreprocessorObjectFactory.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;

namespace SonarQube.TeamBuild.PreProcessor
{
    internal class PreprocessorObjectFactory : IPreprocessorObjectFactory
    {
        private static readonly IPreprocessorObjectFactory instance = new PreprocessorObjectFactory(); 

        public static IPreprocessorObjectFactory Instance {  get { return instance; } }

        private PreprocessorObjectFactory()
        {
        }

        #region IPreprocessorObjectFactory methods

        public ISonarQubeServer CreateSonarQubeServer(ProcessedArgs args, ILogger logger)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            string username = args.GetSetting(SonarProperties.SonarUserName, null);
            string password = args.GetSetting(SonarProperties.SonarPassword, null);
            string hostUrl = args.GetSetting(SonarProperties.HostUrl, null);

            return new SonarWebService(new WebClientDownloader(username, password), hostUrl, logger);
        }

        public ITargetsInstaller CreateTargetInstaller()
        {
            return new TargetsInstaller();
        }

        public IAnalyzerProvider CreateAnalyzerProvider(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            //TODO - return embedded analyzer installer
            throw new NotImplementedException();
        }

        #endregion

    }
}
