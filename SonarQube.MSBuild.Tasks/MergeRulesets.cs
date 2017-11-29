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

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SonarQube.MSBuild.Tasks
{
    /// <summary>
    /// MSBuild task to merge rulesets by adding "Include" references to a
    /// specified ruleset
    /// </summary>
    public class MergeRuleSets : Task
    {
        private static readonly XName IncludeElementName = "Include";
        private static readonly XName PathAttributeName = "Path";
        private static readonly XName ActionAttributeName = "Action";
        private const string IncludeActionValue = "Warning";

        #region Input properties

        /// <summary>
        /// The path to the main ruleset file i.e. the one that will include the others
        /// </summary>
        [Required]
        public string PrimaryRulesetFilePath { get; set; }

        /// <summary>
        /// Full path to the directory containing the project file
        /// </summary>
        /// <remarks>Required to resolve relative ruleset paths</remarks>
        [Required]
        public string ProjectDirectoryPath { get; set; }

        /// <summary>
        /// Full file path for the merged ruleset file. The file should not already exist.
        /// </summary>
        [Required]
        public string MergedRuleSetFilePath { get; set; }

        /// <summary>
        /// The full file paths of any rulesets to include. Can be empty, in which case the task is a no-op.
        /// </summary>
        public string[] IncludedRulesetFilePaths { get; set; }

        #endregion Input properties

        #region Overrides

        public override bool Execute()
        {
            Debug.Assert(PrimaryRulesetFilePath != null, "[Required] property PrimaryRulesetFilePath should not be null when the task is called from MSBuild");
            Debug.Assert(ProjectDirectoryPath != null, "[Required] property ProjectDirectoryPath should not be null when the task is called from MSBuild");
            Debug.Assert(MergedRuleSetFilePath != null, "[Required] property MergedRuleSetFilePath should not be null when the task is called from MSBuild");

            Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_MergingRulesets);

            if (!File.Exists(PrimaryRulesetFilePath))
            {
                throw new FileNotFoundException(Resources.MergeRulesets_MissingPrimaryRuleset, PrimaryRulesetFilePath);
            }
            if (File.Exists(MergedRuleSetFilePath))
            {
                throw new InvalidOperationException(
                    string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.MergeRuleset_MergedRulesetAlreadyExists, MergedRuleSetFilePath));
            }

            if (IncludedRulesetFilePaths == null || IncludedRulesetFilePaths.Length == 0)
            {
                Log.LogMessage(MessageImportance.Low, Resources.MergeRuleset_NoRulesetsSpecified);
                File.Copy(PrimaryRulesetFilePath, MergedRuleSetFilePath);
                return true; // nothing to do if there are no rulesets except copy the file
            }

            var ruleset = XDocument.Load(PrimaryRulesetFilePath);
            foreach (var includePath in IncludedRulesetFilePaths)
            {
                var resolvedIncludePath = TryResolveIncludedRuleset(includePath);
                if (resolvedIncludePath != null)
                {
                    EnsureIncludeExists(ruleset, resolvedIncludePath);
                }
            }
            Log.LogMessage(MessageImportance.Low, Resources.MergeRuleset_SavingUpdatedRuleset, MergedRuleSetFilePath);
            ruleset.Save(MergedRuleSetFilePath);

            return !Log.HasLoggedErrors;
        }

        #endregion Overrides

        #region Private methods

        private string TryResolveIncludedRuleset(string includePath)
        {
            string resolvedPath;

            if (Path.IsPathRooted(includePath))
            {
                resolvedPath = includePath; // assume rooted paths are correctly set
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_ResolvingRuleset, includePath);
                resolvedPath = Path.GetFullPath(Path.Combine(ProjectDirectoryPath, includePath));
                if (File.Exists(resolvedPath))
                {
                    Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_ResolvedRuleset, resolvedPath);
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_FailedToResolveRuleset, includePath);
                    resolvedPath = null;
                }
            }

            return resolvedPath;
        }

        private void EnsureIncludeExists(XDocument ruleset, string includePath)
        {
            var includeElement = FindExistingInclude(ruleset, includePath);
            if (includeElement != null)
            {
                Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_RulesetAlreadyIncluded, includePath);
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, Resources.MergeRuleset_IncludingRuleset, includePath);
                includeElement = new XElement(IncludeElementName);
                includeElement.SetAttributeValue(PathAttributeName, includePath);
                ruleset.Root.AddFirst(includeElement);
            }

            // Ensure the include (new or existing) has the desired Action value
            includeElement.SetAttributeValue(ActionAttributeName, IncludeActionValue);
        }

        private static XElement FindExistingInclude(XDocument ruleset, string includePath)
        {
            return ruleset.Descendants(IncludeElementName).FirstOrDefault(e =>
                {
                    var attr = e.Attribute(PathAttributeName);
                    return attr != null && string.Equals(includePath, attr.Value, StringComparison.OrdinalIgnoreCase);
                });
        }

        #endregion Private methods
    }
}
