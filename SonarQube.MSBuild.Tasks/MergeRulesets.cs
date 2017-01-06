/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

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
            Debug.Assert(this.PrimaryRulesetFilePath != null, "[Required] property PrimaryRulesetFilePath should not be null when the task is called from MSBuild");
            Debug.Assert(this.ProjectDirectoryPath != null, "[Required] property ProjectDirectoryPath should not be null when the task is called from MSBuild");
            Debug.Assert(this.MergedRuleSetFilePath != null, "[Required] property MergedRuleSetFilePath should not be null when the task is called from MSBuild");

            this.Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_MergingRulesets);

            if (!File.Exists(this.PrimaryRulesetFilePath))
            {
                throw new FileNotFoundException(Resources.MergeRulesets_MissingPrimaryRuleset, this.PrimaryRulesetFilePath);
            }
            if (File.Exists(this.MergedRuleSetFilePath))
            {
                throw new InvalidOperationException(
                    string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.MergeRuleset_MergedRulesetAlreadyExists, this.MergedRuleSetFilePath));
            }

            if (this.IncludedRulesetFilePaths == null || this.IncludedRulesetFilePaths.Length == 0)
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.MergeRuleset_NoRulesetsSpecified);
                File.Copy(this.PrimaryRulesetFilePath, this.MergedRuleSetFilePath);
                return true; // nothing to do if there are no rulesets except copy the file
            }

            XDocument ruleset = XDocument.Load(PrimaryRulesetFilePath);
            foreach (string includePath in this.IncludedRulesetFilePaths)
            {
                string resolvedIncludePath = this.TryResolveIncludedRuleset(includePath);
                if (resolvedIncludePath != null)
                {
                    EnsureIncludeExists(ruleset, resolvedIncludePath);
                }
            }
            this.Log.LogMessage(MessageImportance.Low, Resources.MergeRuleset_SavingUpdatedRuleset, this.MergedRuleSetFilePath);
            ruleset.Save(this.MergedRuleSetFilePath);

            return !this.Log.HasLoggedErrors;
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
                this.Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_ResolvingRuleset, includePath);
                resolvedPath = Path.GetFullPath(Path.Combine(this.ProjectDirectoryPath, includePath));
                if (File.Exists(resolvedPath))
                {
                    this.Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_ResolvedRuleset, resolvedPath);
                }
                else
                {
                    this.Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_FailedToResolveRuleset, includePath);
                    resolvedPath = null;
                }
            }

            return resolvedPath;
        }

        private void EnsureIncludeExists(XDocument ruleset, string includePath)
        {
            XElement includeElement = FindExistingInclude(ruleset, includePath);
            if (includeElement != null)
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.MergeRulesets_RulesetAlreadyIncluded, includePath);
            }
            else
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.MergeRuleset_IncludingRuleset, includePath);
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
                    XAttribute attr = e.Attribute(PathAttributeName);
                    return attr != null && string.Equals(includePath, attr.Value, StringComparison.OrdinalIgnoreCase);
                });
        }

        #endregion Private methods
    }
}