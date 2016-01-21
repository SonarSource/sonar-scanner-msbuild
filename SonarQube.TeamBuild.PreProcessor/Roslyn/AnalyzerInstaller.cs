//-----------------------------------------------------------------------
// <copyright file="AnalyzerInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    public class AnalyzerInstaller : IAnalyzerInstaller
    {
        /// <summary>
        /// Installs the specified packages and the dependencies needed to run them
        /// </summary>
        /// <param name="packages">The list of packages to install</param>
        /// <returns>The list of paths of the installed assemblies</returns>
        public IEnumerable<string> InstallAssemblies(IEnumerable<NuGetPackageInfo> packages)
        {
            return null;
        }
    }
}
