//-----------------------------------------------------------------------
// <copyright file="AnalyzerSettings.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class containing the information required to configure
    /// the compiler for Roslyn analysis
    /// </summary>
    /// <remarks>This class is XML-serializable</remarks>
    public class AnalyzerSettings
    {
        public AnalyzerSettings()
        {
        }

        public AnalyzerSettings(string language, string ruleSetFilePath, IEnumerable<string> analyzerAssemblies, IEnumerable<string> additionalFiles)
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

            this.Language = language;
            this.RuleSetFilePath = ruleSetFilePath;
            this.AnalyzerAssemblyPaths = new List<string>(analyzerAssemblies);
            this.AdditionalFilePaths = new List<string>(additionalFiles);
        }

        /// <summary>
        /// Language which this settings refers to
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Path to the ruleset for the Roslyn analyzers
        /// </summary>
        public string RuleSetFilePath { get; set; }

        /// <summary>
        /// File paths for all of the assemblies to pass to the compiler as analyzers
        /// </summary>
        /// <remarks>This includes analyzer assemblies and their dependencies</remarks>
        [XmlArray]
        [XmlArrayItem("Path")]
        public List<string> AnalyzerAssemblyPaths { get; set; }

        /// <summary>
        /// File paths for all files to pass as "AdditionalFiles" to the compiler
        /// </summary>
        [XmlArray]
        [XmlArrayItem("Path")]
        public List<string> AdditionalFilePaths { get; set; }
    }
}
