//-----------------------------------------------------------------------
// <copyright file="IPreprocessorObjectFactory.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Factory that creates the various objects required by the pre-processor
    /// </summary>
    public interface IPreprocessorObjectFactory
    {
        /// <summary>
        /// Creates and returns the component that interacts with the SonarQube server
        /// </summary>
        /// <param name="args">Validated arguments</param>
        /// <remarks>It is the responsibility of the caller to dispose of the server, if necessary</remarks>
        ISonarQubeServer CreateSonarQubeServer(ProcessedArgs args, ILogger logger);

        /// <summary>
        /// Creates and returns the component to install the MSBuild targets
        /// </summary>
        ITargetsInstaller CreateTargetInstaller();

        /// <summary>
        /// Creates and returns the component that provisions the Roslyn analyzers
        /// </summary>
        IAnalyzerProvider CreateAnalyzerProvider(ILogger logger);
    }
}
