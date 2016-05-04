//-----------------------------------------------------------------------
// <copyright file="IBuildWrapperInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Downloads and installs the C++ build wrapper components if they are available on the SonarQube server
    /// </summary>
    public interface IBuildWrapperInstaller
    {
        /// <summary>
        /// Attempts to install the C++ build wrapper by downloading it from the specified
        /// server. Does nothing if the C++ plugin is not installed.
        /// </summary>
        /// <param name="server">The SonarQube server to use</param>
        /// <param name="binDirectory">The location into with the embedded zip should be unzipped</param>
        void InstallBuildWrapper(ISonarQubeServer server, string binDirectory);
    }
}
