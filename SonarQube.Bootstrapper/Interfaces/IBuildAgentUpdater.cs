//-----------------------------------------------------------------------
// <copyright file="IBuildAgentUpdater.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;

namespace SonarQube.Bootstrapper
{
    /// <summary>
    /// Component that prepares the machine for the analysis. 
    /// It copies targets files, downloads the MSBuild.SonarQube.Runner binaries from the SonarQube server, checks versions 
    /// </summary>
    /// <remarks>This interface was introduced to support testing</remarks>
    public interface IBuildAgentUpdater
    {
        /// <summary>
        /// Attempts to update the MSBuild.SonarQube.Runner binaries on the local machine
        /// </summary>
        /// <param name="hostUrl">Address of the SonarQube server</param>
        /// <param name="targetDir">Directory to which the new binaries to copied</param>
        /// <returns>True if the updated succeeded, otherwise false</returns>
        bool TryUpdate(string hostUrl, string targetDir, ILogger logger);

        /// <summary>
        /// Verifies that the pre/post-processors are compatible with this version of the bootstrapper
        /// </summary>
        bool CheckBootstrapperApiVersion(string versionFilePath, Version bootstrapperVersion);
    }
}
