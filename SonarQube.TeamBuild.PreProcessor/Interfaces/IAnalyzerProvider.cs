//-----------------------------------------------------------------------
// <copyright file="IAnalyzerProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.PreProcessor.Roslyn;

namespace SonarQube.TeamBuild.PreProcessor
{
    public interface IAnalyzerProvider
    {
        /// <summary>
        /// Sets up the client to run the Roslyn analyzers as part of the build
        /// i.e. creates the Roslyn ruleset and provisions the analyzer assemblies
        /// and rule parameter files
        /// </summary>
        /// <param name="projectKey">Identifier for the project being analyzed</param>
        CompilerAnalyzerConfig SetupAnalyzers(ISonarQubeServer server, TeamBuildSettings settings, string projectKey);
    }
}
