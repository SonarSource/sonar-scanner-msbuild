//-----------------------------------------------------------------------
// <copyright file="CompilerAnalyzerConfig.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    /// <summary>
    /// Data class containing the information required to configure
    /// the compiler for Roslyn analysis
    /// </summary>
    public class CompilerAnalyzerConfig
    {
        private readonly string ruleSetFilePath;
        private readonly IEnumerable<string> assemblyPaths;
        private readonly IEnumerable<string> additionalFilePaths;

        public CompilerAnalyzerConfig(string ruleSetFilePath, IEnumerable<string> analyzerAssemblies, IEnumerable<string> additionalFiles)
        {
            if (string.IsNullOrWhiteSpace(ruleSetFilePath))
            {
                throw new ArgumentNullException("ruleSetFilePath");
            }
            if (analyzerAssemblies == null)
            {
                throw new ArgumentNullException("analyzerAssemblies");
            }
            if (additionalFiles == null)
            {
                throw new ArgumentNullException("additionalFiles");
            }

            this.ruleSetFilePath = ruleSetFilePath;
            this.assemblyPaths = analyzerAssemblies.ToArray();
            this.additionalFilePaths = additionalFiles.ToArray();
        }

        /// <summary>
        /// Path to the ruleset for the Roslyn analyzers
        /// </summary>
        public string RulesetFilePath { get { return this.ruleSetFilePath; } }

        /// <summary>
        /// File paths for all of the assemblies to pass to the compiler as analyzers
        /// </summary>
        /// <remarks>This includes analyzer assemblies and their dependencies</remarks>
        public IEnumerable<string> AnalyzerAssemblyPaths { get { return this.assemblyPaths; } }

        /// <summary>
        /// File paths for all files to pass as "AdditionalFiles" to the compiler
        /// </summary>
        public IEnumerable<string> AdditionalFilePaths { get { return this.additionalFilePaths; } }
    }
}
