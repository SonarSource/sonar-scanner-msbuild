//-----------------------------------------------------------------------
// <copyright file="RulesetGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.TeamBuild.PreProcessor.Interfaces;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor
{
    public sealed class RulesetGenerator : IRulesetGenerator
    {
        #region #region IRulesetGenerator interface

        /// <summary>
        /// Generates an FxCop file on disc containing all internalKeys from rules belonging to the given repo.
        /// </summary>
        /// <param name="fxCopRepositoryKey">The key of the FxCop repository</param>
        /// <param name="outputFilePath">The full path to the file to be generated</param>
        public void Generate(string fxCopRepositoryKey, IList<ActiveRule> activeRules, string outputFilePath)
        {
            if (string.IsNullOrWhiteSpace(fxCopRepositoryKey))
            {
                throw new ArgumentNullException("fxCopRepositoryKey");
            }
            if (activeRules == null)
            {
                throw new ArgumentNullException("activeRules");
            }

            IEnumerable<ActiveRule> fxCopActiveRules = activeRules.Where(r => r.RepoKey.Equals(fxCopRepositoryKey));

            if (fxCopActiveRules.Any())
            {
                var ids = fxCopActiveRules.Select(r => r.InternalKeyOrKey);
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
