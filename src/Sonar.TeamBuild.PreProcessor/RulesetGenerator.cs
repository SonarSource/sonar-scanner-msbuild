//-----------------------------------------------------------------------
// <copyright file="RulesetGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
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

        public void Generate(SonarWebService ws, string sonarProjectKey, string outputFilePath)
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

        #endregion
    }
}
