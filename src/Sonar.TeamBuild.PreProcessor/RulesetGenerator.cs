//-----------------------------------------------------------------------
// <copyright file="RulesetGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Net;

namespace Sonar.TeamBuild.PreProcessor
{
    public class RulesetGenerator : IRulesetGenerator
    {
        private const string Language = "cs";
        private const string Repository = "fxcop";

        #region Public methods

        /// <summary>
        /// Retrieves settings from the SonarQube server and generates a an FxCop file on disc
        /// </summary>
        /// <param name="sonarProjectKey">The key of the Sonar project for which the ruleset should be generated</param>
        /// <param name="outputFilePath">The full path to the file to be generated</param>
        /// <param name="sonarUrl">The url of the SonarQube server</param>
        /// <param name="userName">The user name for the server (optional)</param>
        /// <param name="password">The password for the server (optional)</param>
        public void Generate(string sonarProjectKey, string outputFilePath, string sonarUrl, string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(sonarProjectKey))
            {
                throw new ArgumentNullException("sonarProjectKey");
            }
            if (string.IsNullOrWhiteSpace(outputFilePath))
            {
                throw new ArgumentNullException("outputFilePath");
            }            
            if (string.IsNullOrWhiteSpace(sonarUrl))
            {
                throw new ArgumentNullException("sonarUrl");
            }

            using (SonarWebService ws = new SonarWebService(new WebClientDownloader(new WebClient(), userName, password), sonarUrl, Language, Repository))
            {
                var qualityProfile = ws.GetQualityProfile(sonarProjectKey);
                var activeRuleKeys = ws.GetActiveRuleKeys(qualityProfile);
                if (activeRuleKeys.Any())
                {
                    var internalKeys = ws.GetInternalKeys();
                    var ids = activeRuleKeys.Select(
                        k =>
                        {
                            var fullKey = Repository + ':' + k;
                            return internalKeys.ContainsKey(fullKey) ? internalKeys[fullKey] : k;
                        });

                    File.WriteAllText(outputFilePath, RulesetWriter.ToString(ids));
                }
                else
                {
                    File.Delete(outputFilePath);
                }
            }
        }

        #endregion
    }
}
