//-----------------------------------------------------------------------
// <copyright file="IAnalzyerInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.TeamBuild.PreProcessor.Roslyn;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor
{
    public interface IAnalyzerInstaller
    {
        /// <summary>
        /// Provisions the analyzer assemblies belonging to the specified packages
        /// </summary>
        /// <returns>Paths to the analyzer assemblies and their dependencies</returns>
        IEnumerable<string> InstallAssemblies(IEnumerable<NuGetPackageInfo> packages);
    }
}
