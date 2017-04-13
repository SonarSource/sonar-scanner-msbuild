/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
 
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
