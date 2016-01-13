//-----------------------------------------------------------------------
// <copyright file="CompilerAnalyzerConfig.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;

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
            this.ruleSetFilePath = ruleSetFilePath;
            this.assemblyPaths = analyzerAssemblies;
            this.additionalFilePaths = additionalFiles;
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
