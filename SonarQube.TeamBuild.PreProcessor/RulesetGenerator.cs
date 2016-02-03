//-----------------------------------------------------------------------
// <copyright file="RulesetGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor
{
    public static class RulesetGenerator
    {
        #region Public methods

        /// <summary>
        /// Retrieves settings from the SonarQube server and generates a an FxCop file on disc
        /// </summary>
        /// <param name="server">SonarQube server instance</param>
        /// <param name="requiredPluginKey">The plugin key that defines the given language</param>
        /// <param name="language">The language of the FxCop repository</param>
        /// <param name="fxCopRepositoryKey">The key of the FxCop repository</param>
        /// <param name="sonarProjectKey">The key of the SonarQube project for which the ruleset should be generated</param>
        /// <param name="outputFilePath">The full path to the file to be generated</param>
        public static void Generate(ISonarQubeServer server, string requiredPluginKey, string language, string fxCopRepositoryKey, string sonarProjectKey, string sonarProjectBranch, string outputFilePath)
        {
            if (server == null)
            {
                throw new ArgumentNullException("server");
            }
            if (string.IsNullOrWhiteSpace(requiredPluginKey))
            {
                throw new ArgumentNullException("requiredPluginKey");
            }
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentNullException("language");
            }
            if (string.IsNullOrWhiteSpace(fxCopRepositoryKey))
            {
                throw new ArgumentNullException("fxCopRepositoryKey");
            }
            if (string.IsNullOrWhiteSpace(sonarProjectKey))
            {
                throw new ArgumentNullException("sonarProjectKey");
            }
            if (string.IsNullOrWhiteSpace(outputFilePath))
            {
                throw new ArgumentNullException("outputFilePath");
            }

            IEnumerable<string> activeRuleKeys = Enumerable.Empty<string>();
            if (server.GetInstalledPlugins().Contains(requiredPluginKey))
            {
                string qualityProfile;
                if (server.TryGetQualityProfile(sonarProjectKey, sonarProjectBranch, language, out qualityProfile))
                {
                    activeRuleKeys = server.GetActiveRuleKeys(qualityProfile, language, fxCopRepositoryKey);
                }
            }

            if (activeRuleKeys.Any())
            {
                var internalKeys = server.GetInternalKeys(fxCopRepositoryKey);
                var ids = activeRuleKeys.Select(
                    k =>
                    {
                        var fullKey = fxCopRepositoryKey + ':' + k;
                        return internalKeys.ContainsKey(fullKey) ? internalKeys[fullKey] : k;
                    });

                File.WriteAllText(outputFilePath, RulesetWriter.ToString(ids));
            }
            else
            {
                File.Delete(outputFilePath);
            }
        }

        #endregion
    }
}
