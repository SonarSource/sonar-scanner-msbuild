//-----------------------------------------------------------------------
// <copyright file="ITargetsInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using SonarQube.Common;

namespace SonarQube.TeamBuild.PostProcessor
{
    /// <summary>
    /// Deletes the loader targets file - SonarQube.Integration.ImportBefore.targets - from the user specific location
    /// </summary>
    public interface ITargetsUninstaller
    {
        void UninstallTargets(ILogger logger);
    }
}
