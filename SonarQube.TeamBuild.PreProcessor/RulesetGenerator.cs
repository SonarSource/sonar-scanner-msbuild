//-----------------------------------------------------------------------
// <copyright file="RulesetGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;

namespace SonarQube.TeamBuild.PreProcessor
{
    public class RulesetGenerator : IRulesetGenerator
    {
        #region Public methods

        public void Generate(ISonarQubeServer server, string requiredPluginKey, string language, string fxcopRepositoryKey, string sonarProjectKey, string outputFilePath)
        {
            if (server == null)
            {
                throw new ArgumentNullException("ws");
            }
            if (string.IsNullOrWhiteSpace(requiredPluginKey))
            {
                throw new ArgumentNullException("requiredPluginKey");
            }
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new ArgumentNullException("language");
            }
            if (string.IsNullOrWhiteSpace(fxcopRepositoryKey))
            {
                throw new ArgumentNullException("fxcopRepositoryKey");
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
                if (server.TryGetQualityProfile(sonarProjectKey, language, out qualityProfile))
                {
                    activeRuleKeys = server.GetActiveRuleKeys(qualityProfile, language, fxcopRepositoryKey);
                }
            }

            if (activeRuleKeys.Any())
            {
                var internalKeys = server.GetInternalKeys(fxcopRepositoryKey);
                var ids = activeRuleKeys.Select(
                    k =>
                    {
                        var fullKey = fxcopRepositoryKey + ':' + k;
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
