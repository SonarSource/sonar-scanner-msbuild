//-----------------------------------------------------------------------
// <copyright file="ISonarQubeServerFactory.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarQube.TeamBuild.PreProcessor
{
    public interface ISonarQubeServerFactory
    {
        /// <summary>
        /// Creates and returns the component that interacts with the SonarQube server
        /// </summary>
        /// <param name="args">Validated arguments</param>
        /// <remarks>It is the responsibility of the caller to dispose of the server, if necessary</remarks>
        ISonarQubeServer Create(ProcessedArgs args);
    }
}
