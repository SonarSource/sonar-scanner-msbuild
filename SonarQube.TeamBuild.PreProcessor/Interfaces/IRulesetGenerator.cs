//-----------------------------------------------------------------------
// <copyright file="IRulesetGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarQube.TeamBuild.PreProcessor
{
    public interface IRulesetGenerator
    {
        /// <summary>
        /// Retrieves settings from the SonarQube server and generates a an FxCop file on disc
        /// </summary>
        /// <param name="server">SonarQube server instance</param>
        /// <param name="requiredPluginKey">The plugin key that defines the given language</param>
        /// <param name="language">The language of the FxCop repository</param>
        /// <param name="fxcopRepositoryKey">The key of the FxCop repository</param>
        /// <param name="sonarProjectKey">The key of the SonarQube project for which the ruleset should be generated</param>
        /// <param name="outputFilePath">The full path to the file to be generated</param>
        void Generate(ISonarQubeServer server, string requiredPluginKey, string language, string fxcopRepositoryKey, string sonarProjectKey, string outputFilePath);
    }
}
