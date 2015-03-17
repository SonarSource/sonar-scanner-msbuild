//-----------------------------------------------------------------------
// <copyright file="IRulesetGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Sonar.FxCopRuleset
{
    public interface IRulesetGenerator
    {
        /// <summary>
        /// Retrieves settings from the SonarQube server and generates a an FxCop file on disc
        /// </summary>
        /// <param name="sonarProjectKey">The key of the Sonar project for which the ruleset should be generated</param>
        /// <param name="outputFilePath">The full path to the file to be generated</param>
        /// <param name="sonarUrl">The url of the SonarQube server</param>
        /// <param name="userName">The user name for the server (optional)</param>
        /// <param name="password">The password for the server (optional)</param>
        void Generate(string sonarProjectKey, string outputFilePath, string sonarUrl, string userName, string password);
    }
}
