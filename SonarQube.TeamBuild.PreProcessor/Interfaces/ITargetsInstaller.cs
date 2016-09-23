//-----------------------------------------------------------------------
// <copyright file="ITargetsInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Copies the loader targets file - SonarQube.Integration.ImportBefore.targets - to a user specific location 
    /// from where MsBuild can automatically import it. Also gives a warning if such a file is present in the non-user specific directory.
    /// </summary>
    public interface ITargetsInstaller
    {
        void InstallLoaderTargets(ILogger logger, string workDirectory);
    }
}
