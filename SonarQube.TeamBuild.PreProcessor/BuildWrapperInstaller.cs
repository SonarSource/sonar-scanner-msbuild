//-----------------------------------------------------------------------
// <copyright file="BuildWrapperInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;

namespace SonarQube.TeamBuild.PreProcessor
{
    public class BuildWrapperInstaller : IBuildWrapperInstaller
    {
        private readonly ILogger logger;

        public BuildWrapperInstaller(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            this.logger = logger;
        }

        #region IBuildWrapperInstaller methods

        public void InstallBuildWrapper(ISonarQubeServer server, string binDirectory)
        {
            if (server == null)
            {
                throw new ArgumentNullException("server");
            }
            if (string.IsNullOrWhiteSpace(binDirectory))
            {
                throw new ArgumentNullException("binDirectory");
            }

            // TODO: implementation
        }

        #endregion
    }
}
